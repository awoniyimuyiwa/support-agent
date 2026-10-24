using Api.Contracts.Attributes;
using Domain.Shared;
using System.ComponentModel.DataAnnotations;

namespace Api.Contracts.Models.V1;

public record DocumentVersionModel(
    [RequiredNotDefault] Guid Id,
    [Required] [MinLength(64)] [MaxLength(64)] string ContentHash,
    [RequiredNotDefault] Guid DocumentId,
    [RequiredNotDefault] Guid FileId,
    [AllowedValues(
    "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
    "text/html",
    "text/markdown",
    "application/pdf",
    "text/plain")] string FileType,
    [RequiredNotDefault] DateTimeOffset CreatedAt,
    [RequiredNotDefault] DateTimeOffset UpdatedAt,
    [MaxLength(Domain.Shared.Constants.StringMaxLength)] string? ConcurrencyStamp,
    bool IsDeleted = false) : ISyncableModel;

public record DocumentVersionSyncModel(
    [Required] 

    [UniqueMax(maxLength: Constants.MaxLengthOfList, UniquePropertyName = nameof(DocumentVersionModel.Id))] 
    [UniqueMax(maxLength: Constants.MaxLengthOfList, UniquePropertyName = nameof(DocumentVersionModel.FileId))]
    [UniqueMax(maxLength: Constants.MaxLengthOfList, UniquePropertyName = nameof(DocumentVersionModel.ContentHash))]
    List<DocumentVersionModel> Items,
    
    [EnumDataType(typeof(SyncBehavior))] SyncBehavior Behavior = SyncBehavior.Default)
{
    public List<DocumentVersionModel> Items { get; init; } = Items ?? [];
}
