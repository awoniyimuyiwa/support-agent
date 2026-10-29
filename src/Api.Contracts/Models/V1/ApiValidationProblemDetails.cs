
using System.Text.Json.Serialization;

namespace Api.Contracts.Models.V1;

/// <summary>
/// Representation of the RFC 7807/9110 'application/problem+json' specification.
/// </summary>
/// <remarks>
/// Required because the official framework-supplied 
/// <c>ValidationProblemDetails</c> and <c>ProblemDetails</c> classes reside inside server-side ASP.NET Core assemblies 
/// (<c>Microsoft.AspNetCore.Mvc</c>). 
/// 
/// Referencing those heavy web-framework packages here would pollute the contracts layer and cause 
/// compilation failures or massive bundle size inflation inside the single-threaded Blazor WebAssembly client. 
/// 
/// This structure mirrors <c>ValidationProblemDetails</c>
/// </remarks>
public class ApiValidationProblemDetails
{
    [JsonPropertyName("type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Type { get; set; } = "https://ietf.org";

    [JsonPropertyName("title")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Title { get; set; } = "One or more validation errors occurred.";

    [JsonPropertyName("status")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Status { get; set; } = 400;

    [JsonPropertyName("detail")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Detail { get; set; }

    [JsonPropertyName("instance")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Instance { get; set; }

    [JsonPropertyName("errors")]
    public Dictionary<string, string[]> Errors { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public ApiValidationProblemDetails() { }

    public ApiValidationProblemDetails(Dictionary<string, string[]> errors)
    {
        Errors = errors;
    }
}