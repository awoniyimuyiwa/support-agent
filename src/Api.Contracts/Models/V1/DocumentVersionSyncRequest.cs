using Api.Contracts.Attributes;
using Domain.Shared;
using System.ComponentModel.DataAnnotations;

namespace Api.Contracts.Models.V1;

public record DocumentVersionSyncRequest(
    [Required] 

    [UniqueMax(maxLength: Constants.MaxLengthOfList, UniquePropertyName = nameof(DocumentVersionSyncItem.Id))] 
    [UniqueMax(maxLength: Constants.MaxLengthOfList, UniquePropertyName = nameof(DocumentVersionSyncItem.FileId))]
    [UniqueMax(maxLength: Constants.MaxLengthOfList, UniquePropertyName = nameof(DocumentVersionSyncItem.ContentHash))]
    List<DocumentVersionSyncItem> Items,
    
    [EnumDataType(typeof(SyncBehavior))] SyncBehavior Behavior = SyncBehavior.Default)
{
    public List<DocumentVersionSyncItem> Items { get; init; } = Items ?? [];
}

public record DocumentVersionSyncItem(
    [RequiredNotDefault] Guid Id,
    [Required][MinLength(64)][MaxLength(64)] string ContentHash,
    [RequiredNotDefault] Guid DocumentId,
    [RequiredNotDefault] Guid FileId,
    [Required][AllowedFileTypes] string FileType,
    [RequiredNotDefault] DateTimeOffset CreatedAt,
    [RequiredNotDefault] DateTimeOffset UpdatedAt,
    [MaxLength(Domain.Shared.Constants.StringMaxLength)] string? ConcurrencyStamp,
    bool IsDeleted = false) : ISyncableModel;

