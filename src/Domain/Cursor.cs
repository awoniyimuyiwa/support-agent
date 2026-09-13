using System.Text.Json;

namespace Domain;

public record Cursor(
    long LastId,
    DateTimeOffset Timestamp) 
{
    public string ToJson() => JsonSerializer.Serialize(this, JsonSerializerOptions.Web);
}
