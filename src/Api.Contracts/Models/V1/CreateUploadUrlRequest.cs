using Api.Contracts.Attributes;
using System.ComponentModel.DataAnnotations;

namespace Api.Contracts.Models.V1;

/// <summary>
/// Request contract used to generate cloud storage upload urls.
/// </summary>
public record CreateUploadUrlRequest(
    [RequiredNotDefault]
    [UniqueMax(maxLength: Constants.MaxLengthOfList, UniquePropertyName = nameof(CreateUploadUrlRequestItem.ItemId))]
    List<CreateUploadUrlRequestItem> Items)
{
    public List<CreateUploadUrlRequestItem> Items { get; init; } = Items ?? [];
}

/// <summary>
/// Represents an individual file payload metadata assignment within a batch upload initialization request.
/// </summary>
/// <param name="FileType">The explicit MIME target format of the file to be uploaded. Supported types are restricted strictly to "application/pdf" or "application/vnd.openxmlformats-officedocument.wordprocessingml.document".</param>
/// <param name="ItemId">The unique client-side identifier (e.g., TicketId, DocumentId) associated with this specific upload context tracking row.</param>
public record CreateUploadUrlRequestItem(
    [Required][AllowedFileTypes] string FileType,
    [RequiredNotDefault] Guid ItemId);
