using Api.Contracts.Options;
using Azure.Core;
using Azure.Security.KeyVault.Keys.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pipelines.Sockets.Unofficial.Arenas;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;

namespace Web.Extensions;

public static class Security
{
    public static IServiceCollection AddCustomDataProtection(
        this IServiceCollection services,
        string appName,
        CustomDataProtectionOptions customDataProtectionOptions,
        ILogger logger,
        TokenCredential tokenCredential,
        bool isAspireEmulator = false,
        TimeProvider? timeProvider = null)
    {
        var dataProtectionBuilder = services.AddDataProtection()
            .SetApplicationName(appName);

        // Use the options container to configure Redis key persistence dynamically at boot!
        // This defers execution until the DI container is fully built, natively resolving the registered 
        // IConnectionMultiplexer singleton instance with zero lifecycle leaks.
        services.AddOptions<KeyManagementOptions>()
            .Configure<IConnectionMultiplexer>((options, redis) =>
            {
                options.XmlRepository = new RedisXmlRepository(
                    () => redis.GetDatabase(),
                    $"{appName}-Antiforgery-Keys");
            });

        X509KeyStorageFlags certStorageFlag = X509KeyStorageFlags.EphemeralKeySet;
        if (customDataProtectionOptions?.LocalFallback?.StorageFlag != null &&
            Enum.TryParse<X509KeyStorageFlags>(customDataProtectionOptions.LocalFallback.StorageFlag, true, out var parsedFlag))
        {
            certStorageFlag = parsedFlag;
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

                ValidateCertificateExpiration(cert, logger, timeProvider ??= TimeProvider.System);
                services.AddSingleton(cert);
                certs.Add(cert);
            }
        }

        if (!string.IsNullOrWhiteSpace(customDataProtectionOptions?.KeyIdentifier))
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(
                    "🚀 UI Data Protection Primary Path Active: Encrypting keys using Azure Key Vault at {Uri}.",
                    customDataProtectionOptions.KeyIdentifier);
            }
            var keyUri = new Uri(customDataProtectionOptions.KeyIdentifier);
            dataProtectionBuilder.ProtectKeysWithAzureKeyVault(
                  keyUri.AbsoluteUri,
                  _ => new KeyResolver(tokenCredential, new CryptographyClientOptions
                  {
                      DisableChallengeResourceVerification = isAspireEmulator
                  }));

            if (certs.Count > 0)
            {
                dataProtectionBuilder.UnprotectKeysWithAnyCertificate([.. certs]);
            }
        }
        else
        {
            logger.LogWarning("⚠️ UI Data Protection Fallback Path Active: Azure Key Vault URL missing. Using local certificates.");

            if (certs.Count == 0)
            {
                throw new InvalidOperationException("Cannot initialize UI Data Protection. Both Azure Key Vault URI and fallback certificates are missing.");
            }

            var primaryCert = certs.First();
            dataProtectionBuilder.ProtectKeysWithCertificate(primaryCert)
                .UnprotectKeysWithAnyCertificate([.. certs]);
        }

        return services;
    }

    private static void ValidateCertificateExpiration(
        X509Certificate2 cert,
        ILogger logger,
        TimeProvider timeProvider)
    {
        var now = timeProvider.GetUtcNow();
        if (now > cert.NotAfter)
        {
            logger.LogError("❌ The UI Data Protection certificate with thumbprint {Thumbprint} has EXPIRED as of {ExpiryDate}.", cert.Thumbprint, cert.NotAfter);
            throw new InvalidOperationException($"The UI Data Protection certificate has expired: {cert.Thumbprint}");
        }

        var remainingTime = cert.NotAfter - now;
        if (remainingTime.TotalDays < 30)
        {
            logger.LogWarning("⚠️ The UI Data Protection certificate with thumbprint {Thumbprint} expires in {Days} days. Plan rotation sequence.", cert.Thumbprint, Math.Ceiling(remainingTime.TotalDays));
        }
    }
}
