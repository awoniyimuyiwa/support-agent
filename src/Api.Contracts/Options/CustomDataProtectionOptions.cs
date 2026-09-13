using System.ComponentModel.DataAnnotations;

namespace Api.Contracts.Options;

public class CustomDataProtectionOptions : IValidatableObject
{
    public const string SectionName = "DataProtectionOptions";

    public string? KeyIdentifier { get; set; }

    public CertificateFallbackOptions? LocalFallback { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        bool hasKeyIdentifier = !string.IsNullOrWhiteSpace(KeyIdentifier);
        bool hasLocalFallback = LocalFallback != null;

        if (!hasKeyIdentifier && !hasLocalFallback)
        {
            yield return new ValidationResult(
                $"Invalid Configuration: You must provide either an Azure Key Vault '{nameof(KeyIdentifier)}' OR configure the nested '{nameof(LocalFallback)}' block.",
                [nameof(KeyIdentifier), nameof(LocalFallback)]);
        }
    }
}

public class CertificateFallbackOptions
{
    public string StorageFlag { get; set; } = "EphemeralKeySet";

    [Required(ErrorMessage = "At least one certificate is required.")]
    [MinLength(1, ErrorMessage = "The Certificates list cannot be empty.")]
    public List<CertOptions> Certificates { get; set; } = [];
}

public class CertOptions
{
    /// <summary>
    /// The cert should be stored as base64 in config to avoid file management issues across different environments (local dev, docker, cloud).
    /// </summary>
    [Required]
    public string Base64 { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

