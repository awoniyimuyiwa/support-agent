using Api.Utils;
using Microsoft.AspNetCore.DataProtection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Api.Extensions;

public static class Serialization
{
    extension(JsonSerializerOptions options)
    {
        public JsonSerializerOptions PostConfigure(IDataProtector protector)
        {
            options.Converters.Add(new JsonStringEnumConverter());

            var resolver = options.TypeInfoResolver as DefaultJsonTypeInfoResolver ?? new DefaultJsonTypeInfoResolver();

            resolver.Modifiers.Add(typeInfo => SecurityJsonModifier.Modify(typeInfo, protector));

            options.TypeInfoResolver = resolver;

            return options;
        }
    }
}

