namespace Api.Contracts.Models.V1;

public record UploadUrlsResponse(Dictionary<Guid, string> Map)
{
    public Dictionary<Guid, string> Map { get; init; } = Map ?? [];
}