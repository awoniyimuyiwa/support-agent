using System.Collections;
using System.ComponentModel.DataAnnotations;

namespace Api.Contracts.Attributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter, AllowMultiple = true)]
public sealed class UniqueMaxAttribute(int maxLength = 100) : ValidationAttribute
{
    /// <summary>
    /// Optional property name for targeting sub-fields
    /// </summary>
    public string? UniquePropertyName { get; set; }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not IEnumerable enumerable)
            return ValidationResult.Success;

        var list = enumerable.Cast<object>().ToList();
        if (list.Count == 0)
            return ValidationResult.Success;

        var memberType = validationContext.ObjectType
            .GetProperty(validationContext.MemberName ?? string.Empty)?
            .PropertyType ?? value.GetType();

        Type? elementType = null;
        if (memberType.IsArray)
        {
            elementType = memberType.GetElementType();
        }
        else if (memberType.IsGenericType)
        {
            elementType = memberType.GetGenericArguments().FirstOrDefault();
        }

        int effectiveMax = (elementType is { IsEnum: true })
            ? Enum.GetValues(elementType).Length
            : maxLength;

        if (list.Count > effectiveMax)
        {
            return new ValidationResult($"{validationContext.DisplayName} cannot exceed {effectiveMax} items.");
        }

        if (!string.IsNullOrWhiteSpace(UniquePropertyName))
        {
            // Target sub-property via reflection for complex object arrays
            var propInfo = list.FirstOrDefault()?.GetType().GetProperty(UniquePropertyName);
            if (propInfo == null)
                return ValidationResult.Success;

            var valuesToCompare = list
                .Select(item => propInfo.GetValue(item, null))
                .Where(val => val != null)
                .ToList();

            // Case-Insensitive evaluation check specifically tailored for String property keys
            if (propInfo.PropertyType == typeof(string))
            {
                var stringValues = valuesToCompare.Cast<string>().ToList();
                if (stringValues.Distinct(StringComparer.OrdinalIgnoreCase).Count() != stringValues.Count)
                {
                    return new ValidationResult($"{validationContext.DisplayName} must contain unique {UniquePropertyName} entries.");
                }
            }
            else
            {
                // Fallback for standard primitives (Guid, int, long, etc.) inside object list
                if (valuesToCompare.Distinct().Count() != valuesToCompare.Count)
                {
                    return new ValidationResult($"{validationContext.DisplayName} must contain unique {UniquePropertyName} entries.");
                }
            }
        }
        else
        {
            // Default primitive value equality if no property name is specified
            if (list.Distinct().Count() != list.Count)
                return new ValidationResult($"{validationContext.DisplayName} must contain unique items.");
        }

        return ValidationResult.Success;
    }
}
