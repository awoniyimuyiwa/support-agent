using System.ComponentModel.DataAnnotations;

namespace Api.Contracts.Attributes;


[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class RequiredNotDefaultAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null)
        {
            return new ValidationResult(ErrorMessage ?? $"{validationContext.DisplayName} is required.");
        }

        if (value is Guid guidValue && guidValue == Guid.Empty)
        {
            return new ValidationResult(ErrorMessage ?? $"{validationContext.DisplayName} cannot be an empty GUID.");
        }

        if (value is DateTimeOffset dateValue && dateValue == DateTimeOffset.MinValue)
        {
            return new ValidationResult(ErrorMessage ?? $"{validationContext.DisplayName} cannot be the minimum default date.");
        }

        // Catch int/long default 0 explicitly (Only if your business logic demands IDs start at 1)
        if ((value is int intValue && intValue == 0) || (value is long longValue && longValue == 0))
        {
            return new ValidationResult(ErrorMessage ?? $"{validationContext.DisplayName} cannot be 0.");
        }

        return ValidationResult.Success;
    }
}

