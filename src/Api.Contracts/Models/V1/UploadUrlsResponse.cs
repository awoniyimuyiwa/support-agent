namespace Api.Contracts.Models.V1;

public record UploadUrlsResponse(IReadOnlyDictionary<Guid, UploadUrlsResponseItem> Map);

public record UploadUrlsResponseItem(Guid FileId, string Url);