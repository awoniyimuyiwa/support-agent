using Domain;
using System.Text.Json.Serialization.Metadata;

namespace Api.Utils;

public static class AuditLogJsonModifier
{
    public static void Modify(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

        foreach (var property in typeInfo.Properties)
        {
            // Ignore large binary data (byte arrays)
            if (property.PropertyType == typeof(byte[]) || property.PropertyType == typeof(Stream))
            {
                property.Get = _ => Domain.Shared.Constants.BinaryDataMask;
                continue;
            }

            // Mask sensitive fields and fields marked as not audited
            var hasAttribute = property.AttributeProvider?.IsDefined(typeof(NotAuditedAttribute), true) ?? false;
            var isSensitive = Domain.Shared.Constants.SensitiveFieldNames.Contains(property.Name);
            if (hasAttribute || isSensitive)
            {
                property.Get = _ => Domain.Shared.Constants.Mask;
            }

            // Truncate long strings
            if (property.PropertyType == typeof(string))
            {
                var originalGetter = property.Get;
                if (originalGetter != null)
                {
                    property.Get = obj =>
                    {
                        var value = originalGetter(obj) as string;
                        if (value?.Length > Domain.Shared.Constants.StringMaxLength)
                        {
                            return string.Concat(
                                value.AsSpan(0, Domain.Shared.Constants.StringMaxLength - Domain.Shared.Constants.TruncationIndicator.Length),
                                Domain.Shared.Constants.TruncationIndicator);
                        }
                        return value;
                    };
                }
            }
        }
    }
}


