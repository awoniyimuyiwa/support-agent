using Api.Contracts.Attributes;
using Domain.Shared;
using System.ComponentModel.DataAnnotations;

namespace Api.Contracts.Models.V1;

public record DocumentVersionSyncRequest(
    [Required] 
    [UniqueMax(maxLength: Constants.MaxLengthOfList, UniquePropertyName = nameof(DocumentVersionSyncRequestItem.Id))] List<DocumentVersionSyncRequestItem> Items,
    [EnumDataType(typeof(SyncBehavior))] SyncBehavior Behavior = SyncBehavior.Default)
{
    public List<DocumentVersionSyncRequestItem> Items { get; init; } = Items ?? [];
}

public record DocumentVersionSyncRequestItem(
     [RequiredNotDefault] Guid Id,
     [RequiredNotDefault] DateTimeOffset UpdatedAt,
     string? ConcurrencyStamp,
     bool IsDeleted = false) : ISyncableModel;

