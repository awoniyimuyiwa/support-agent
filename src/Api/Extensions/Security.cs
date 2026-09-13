using Api.Contracts.Models.V1;
using Api.Contracts.Options;
using Api.Options;
using Api.Utils;
using Azure.Core;
using Azure.Security.KeyVault.Keys.Cryptography;
using Domain;
using Infrastructure.EntityFrameworkCore.Extensions;
using Microsoft.AspNetCore.DataProtection;
using System.Buffers.Text;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace Api.Extensions;

public static class Security
{
    extension(ClaimsPrincipal user)
    {
        // var id = User.UserId;
        public long UserId
        {
            get
            {
                var value = user.FindFirstValue(ClaimTypes.NameIdentifier);
                return value is not null
                    ? long.Parse(value)
                    : throw new UnauthorizedAccessException();
            }
        }
    }

    extension(IServiceCollection services)
    {
        /// <summary>
        /// Configures .NET Data Protection to persist keys in SQL Server and encrypt them using a certificate.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Verification (SQL Server Management Studio):</b><br />
        /// To verify that .NET Data Protection keys are successfully stored and encrypted in the database:
        /// </para>
        /// <para>
        /// 1. Query: <code>SELECT FriendlyName, Xml FROM [dbo].[DataProtectionKeys]</code>
        /// </para>
        /// <para>
        /// 2. Confirm Encryption:<br />
        /// [SUCCESS]: The <b>Xml</b> column contains an <b>&lt;encryptedSecret&gt;</b> tag with a long Base64 string.<br />
        /// [RISK]: If <b>&lt;masterKey&gt;</b> is visible in plaintext, certificate protection is NOT active.
        /// </para>
        /// <para>
        /// <b>Certificate Rotation:</b><br />
        /// To avoid invalidating user sessions during rotation, do NOT swap the primary certificate immediately. 
        /// Follow the 3-step <b>Zero-Downtime Rotation Workflow</b> documented in the <b>README.md</b>.
        /// </para>
        /// <para>
        /// <b>Why this is necessary:</b><br />
        /// Simply replacing a certificate makes existing database keys unreadable instantly, 
        /// causing all current user sessions (cookies) and protected data to be invalidated.
        /// </para>
        /// <para>
        /// <b>Azure Key Vault Automation:</b><br />
        /// Using [Azure Key Vault](https://learn.microsoft.com) automates this process via versionless key URIs:
        /// <br />• <b>Key Identification:</b> AKV embeds the version ID in the database metadata.
        /// <br />• <b>Automatic Decryption:</b> Nodes request specific versions from AKV for decryption.
        /// <br />• <b>No "Split-Brain":</b> AKV handles versioning transparently, allowing nodes with different configurations to decrypt each other's keys.
        /// </para>
        /// </remarks>
        public IServiceCollection AddCustomDataProtection(
            string appName,
            CustomDataProtectionOptions customDataProtectionOptions,
            ILogger logger,
            TokenCredential tokenCredential,
            bool isAspireEmulator = false,
            TimeProvider? timeProvider = null)
        {
            X509KeyStorageFlags certStorageFlag = X509KeyStorageFlags.EphemeralKeySet;
            if (customDataProtectionOptions.LocalFallback?.StorageFlag != null &&
                Enum.TryParse<X509KeyStorageFlags>(customDataProtectionOptions.LocalFallback.StorageFlag, true, out var parsedFlag))
            {
                certStorageFlag = parsedFlag;
            }
            else if (!string.IsNullOrWhiteSpace(customDataProtectionOptions?.LocalFallback?.StorageFlag))
            {
                logger.LogWarning(
                    "Invalid StorageFlag '{Flag}'. Falling back to EphemeralKeySet.", 
                    customDataProtectionOptions.LocalFallback.StorageFlag);
            }

            var certs = new List<X509Certificate2>();
            if (customDataProtectionOptions?.LocalFallback?.Certificates?.Count > 0)
            {
                foreach (var c in customDataProtectionOptions.LocalFallback.Certificates)
                {
                    var cert = X509CertificateLoader.LoadPkcs12(
                        Convert.FromBase64String(c.Base64), 
                        c.Password, 
                        certStorageFlag);

                    if (!cert.HasPrivateKey)
                    {
                        throw new InvalidOperationException($"Certificate with thumbprint {cert.Thumbprint} does not have a private key.");
                    }

                    ValidateCertificateExpiration(
                        cert, 
                        logger, 
                        timeProvider ??= TimeProvider.System);

                    services.AddSingleton(cert);
                    certs.Add(cert);
                }
            }

            var dataProtectionBuilder = services.AddDataProtection()
                .SetApplicationName(appName)
                // Global persistence strategy remains PostgreSQL DbContext
                .CustomPersistKeysToDbContext();

            if (!string.IsNullOrWhiteSpace(customDataProtectionOptions?.KeyIdentifier))
            {
                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation(
                        "🚀 Data Protection Primary Path Active: Encrypting keys using Azure Key Vault at {Uri}.", 
                        customDataProtectionOptions.KeyIdentifier);
                }

                // Aure key vault protects master keys via the versionless Key Identifier URL. 
                // Both encryption and decryption operations are managed automatically by the Azure SDK.
                //
                // Whether the SDK utilizes a FIPS 140-2 Level 3 Hardware Security Module (HSM)
                // depends entirely on the Azure Key Vault tier and the Key Type generated in Azure:
                //   - Standard Tier (Key Type: 'RSA'): Software-protected cryptographic wrapper.
                //   - Premium Tier/Managed HSM (Key Type: 'RSA-HSM'): Pure hardware-isolated HSM enforcement.
                //
                // Application is completely decoupled from the cryptographic tier and works identically for both software and hardware-backed keys.
                var keyUri = new Uri(customDataProtectionOptions.KeyIdentifier);
                dataProtectionBuilder.ProtectKeysWithAzureKeyVault(
                    keyUri.AbsoluteUri,
                    _ => new KeyResolver(tokenCredential, new CryptographyClientOptions
                    {
                        DisableChallengeResourceVerification = isAspireEmulator
                    }));

                // If database contains legacy rows or failover keys encrypted with local certificates, 
                // this allows the app to decrypt them smoothly, preventing split-brain session crashes.
                if (certs.Count > 0)
                {
                    dataProtectionBuilder.UnprotectKeysWithAnyCertificate([.. certs]);
                }
            }
            else
            {
                logger.LogWarning("⚠️ Data Protection Fallback Path Active: Azure Key Vault URL missing. Using local certificates.");

                if (certs.Count == 0)
                {
                    throw new InvalidOperationException("Cannot initialize Data Protection. Both Azure Key Vault URI and fallback certificates are missing.");
                }

                var primaryCert = certs.First();
                dataProtectionBuilder.ProtectKeysWithCertificate(primaryCert)
                    .UnprotectKeysWithAnyCertificate([.. certs]);
            }

            return services;
        }
    }

    extension(string? text)
    {
        /// <summary>
        /// Generates a secure, non-reversible hash of the provided string to prevent plain-text exposure in storage.
        /// </summary>
        /// <returns>A URL-safe Base64 encoded SHA256 hash.</returns>
        public string Hash()
        {
            ArgumentNullException.ThrowIfNull(text);

            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
            return Base64Url.EncodeToString(bytes);
        }

        /// <summary>
        /// Cryptographically protects the string and returns a URL-safe encoded result.
        /// </summary>
        /// <param name="protector">The base data protector.</param>
        /// <param name="purpose">An optional specific purpose string for additional cryptographic isolation.</param>
        /// <returns>An encrypted, URL-safe Base64 string.</returns>
        public string Protect(IDataProtector protector, string? purpose)
        {
            ArgumentNullException.ThrowIfNull(text);
            ArgumentNullException.ThrowIfNull(protector);

            var purposeProtector = string.IsNullOrWhiteSpace(purpose)
                ? protector
                : protector.CreateProtector(purpose);

            var unprotectedBytes = Encoding.UTF8.GetBytes(text);
            var protectedBytes = purposeProtector.Protect(unprotectedBytes);

            return Base64Url.EncodeToString(protectedBytes);
        }

        /// <summary>
        /// Decodes and decrypts a URL-safe protected string back to its original value.
        /// </summary>
        /// <param name="protector">The <see cref="IDataProtector"/> used for cryptographic operations.</param>
        /// <param name="purpose">The optional runtime purpose provided by the framework.</param>
        /// <returns>The original raw string, or null if decryption or decoding fails.</returns>
        public string? Unprotect(IDataProtector protector, string? purpose)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;

            try
            {
                var protectedBytes = Base64Url.DecodeFromChars(text);
                if (protectedBytes == null) return null;

                var purposeProtector = string.IsNullOrWhiteSpace(purpose)
                    ? protector
                    : protector.CreateProtector(purpose);

                var unprotectedBytes = purposeProtector.Unprotect(protectedBytes);
                return Encoding.UTF8.GetString(unprotectedBytes);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Decrypts the protected text to create a masked version (e.g., ••••1234) for UI display.
        /// </summary>
        /// <param name="protector">The <see cref="IDataProtector"/> used to decrypt the text.</param>
        /// <param name="purpose">The specific purpose string used when the text was originally encrypted.</param>
        /// <returns>A masked string if decryption succeeds; otherwise, a generic mask or null.</returns>
        public string? Mask(IDataProtector protector, string? purpose)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;

            const string mask = "••••";

            try
            {
                // Notice we can invoke sibling extensions directly on 'text'
                var unProtectedText = text.Unprotect(protector, purpose);

                if (string.IsNullOrWhiteSpace(unProtectedText) || unProtectedText.Length <= 4)
                {
                    return mask;
                }

                return $"{mask}{unProtectedText[^4..]}";
            }
            catch
            {
                return mask;
            }
        }

        /// <summary>
        /// Decrypts and deserializes a protected string into a <see cref="Cursor"/>.
        /// </summary>
        /// <param name="protector">The data protector used for decryption.</param>
        /// <returns>A <see cref="Cursor"/> instance, or null if the input is invalid or empty.</returns>
        public Cursor? ToCursor<T>(IDataProtector protector) where T : class
        {
            if (string.IsNullOrWhiteSpace(text)) return null;

            var purpose = EncryptionUtils.GetPurpose(
                typeof(CursorListModel<T>),
                nameof(CursorListModel<>.Next));

            try
            {
                var json = text.Unprotect(protector, purpose);
                if (string.IsNullOrWhiteSpace(json)) return null;

                return JsonSerializer.Deserialize<Cursor>(json, JsonSerializerOptions.Web);
            }
            catch (Exception ex) when (ex is CryptographicException or JsonException)
            {
                return null;
            }
        }

        /// <summary>
        /// Decrypts and deserializes a protected string into a synchronization <see cref="Cursor"/> 
        /// utilizing the specific cryptographic purpose of the DeltaModel payload wrapper.
        /// </summary>
        /// <param name="protector">The data protector used for decryption.</param>
        /// <returns>A valid <see cref="Cursor"/> instance, or null if the input is invalid or tampered with.</returns>
        public Cursor? ToDeltaCursor<T>(IDataProtector protector) where T : class, ISyncableModel
        {
            if (string.IsNullOrWhiteSpace(text)) return null;

            var purpose = EncryptionUtils.GetPurpose(
                typeof(DeltaModel<T>),
                nameof(DeltaModel<>.Next));

            try
            {
                var json = text.Unprotect(protector, purpose);
                if (string.IsNullOrWhiteSpace(json)) return null;

                return JsonSerializer.Deserialize<Cursor>(json, JsonSerializerOptions.Web);
            }
            catch (Exception ex) when (ex is CryptographicException or JsonException)
            {
                return null;
            }
        }
    }

    /// <summary>
    /// By default, Data Protection generates a new master key every 90 days.
    /// When it attempts to "roll" the key: It tries to encrypt the new key using the certificate.
    /// If the certificate is expired, the encryption process will throw an exception during the key generation phase.
    /// Unlike web browsers, Data Protection doesn't usually perform a full revocation check or chain validation when encrypting;
    /// it primarily cares that it has a valid X509Certificate2 object with a private key.
    /// However, if the OS marks the certificate as "Invalid," the .ProtectKeysWithCertificate() call will fail.
    /// 
    /// Data Protection allows keys to "expire" (default 90 days), but it never deletes them. 
    /// It just creates a new one for new encryption. As long as the old XML keys remain in storage (database or Redis), 
    /// IDataProtector can still decrypt data created 5 years ago.
    /// </summary>
    private static void ValidateCertificateExpiration(
        X509Certificate2 cert,
        ILogger logger,
        TimeProvider timeProvider)
    {
        // Certificates are stored in UTC, so we convert NotAfter to UTC for a valid comparison
        DateTime expirationUtc = cert.NotAfter.ToUniversalTime();
        DateTime nowUtc = timeProvider.GetUtcNow().UtcDateTime;

        if (expirationUtc < nowUtc)
        {
            if (logger.IsEnabled(LogLevel.Critical))
            {
                logger.LogCritical(
                    "❌ DATA PROTECTION CERTIFICATE EXPIRED! Thumbprint: {Thumbprint}. Expiration: {ExpireDate} UTC. New keys CANNOT be generated.",
                    cert.Thumbprint,
                    expirationUtc);
            }

            // Proactively stop startup instead of failing silently later
            throw new InvalidOperationException($"The Data Protection certificate {cert.Thumbprint} has expired.");
        }
        else if (expirationUtc < nowUtc.AddDays(30))
        {
            var daysRemaining = (expirationUtc - nowUtc).Days;
            if (logger.IsEnabled(LogLevel.Warning))
            {
                logger.LogWarning(
                    "⚠️ DATA PROTECTION CERTIFICATE EXPIRES SOON! Thumbprint: {Thumbprint}. Expires in {Days} days ({ExpireDate} UTC). Prepare for rotation.",
                    cert.Thumbprint,
                    daysRemaining,
                    expirationUtc);
            }
        }
        else
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(
                    "✅ Data Protection Certificate is valid until {ExpireDate} UTC.",
                    expirationUtc);
            }
        }
    }
}

