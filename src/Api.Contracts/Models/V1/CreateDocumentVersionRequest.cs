using Api.Contracts.Attributes;
using System.ComponentModel.DataAnnotations;

namespace Api.Contracts.Models.V1;

public record CreateDocumentVersionRequest(
    [Required]
    [UniqueMax(maxLength: Constants.MaxLengthOfList, UniquePropertyName = nameof(CreateDocumentVersionRequestItem.Id))]
    [UniqueMax(maxLength: Constants.MaxLengthOfList, UniquePropertyName = nameof(CreateDocumentVersionRequestItem.ContentHash))]
    [UniqueMax(maxLength: Constants.MaxLengthOfList, UniquePropertyName = nameof(CreateDocumentVersionRequestItem.FileId))]
    List<CreateDocumentVersionRequestItem> Items)
{
    public List<CreateDocumentVersionRequestItem> Items { get; init; } = Items ?? [];
}

public record CreateDocumentVersionRequestItem(
    [RequiredNotDefault] Guid Id,
    [Required][MinLength(64)][MaxLength(64)] string ContentHash,
    [RequiredNotDefault] Guid DocumentId,
    [RequiredNotDefault] Guid FileId,
    [Required][AllowedFileTypes] string FileType,
    [RequiredNotDefault] DateTimeOffset CreatedAt);
