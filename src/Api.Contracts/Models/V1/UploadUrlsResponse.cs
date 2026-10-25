namespace Api.Contracts.Models.V1;

public record UploadUrlsResponse(IReadOnlyDictionary<Guid, string> Map) {}