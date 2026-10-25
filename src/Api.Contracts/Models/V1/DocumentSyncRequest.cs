using Api.Contracts.Attributes;
using Domain.Shared;
using System.ComponentModel.DataAnnotations;

namespace Api.Contracts.Models.V1;

public record DocumentSyncRequest(
    [Required][UniqueMax(maxLength: Constants.MaxLengthOfList, UniquePropertyName = nameof(DocumentSyncItem.Id))][UniqueMax(maxLength: Constants.MaxLengthOfList, UniquePropertyName = nameof(DocumentSyncItem.Name))] List<DocumentSyncItem> Items,
    [EnumDataType(typeof(SyncBehavior))] SyncBehavior Behavior = SyncBehavior.Default)
{
    public List<DocumentSyncItem> Items { get; init; } = Items ?? [];
}

public record DocumentSyncItem(
    [RequiredNotDefault] Guid Id,
    [RequiredNotDefault] Guid CategoryId,
    [Required][MinLength(Domain.Shared.Constants.StringMinLength)][MaxLength(Domain.Shared.Constants.StringMaxLength)] string Name,
    [RequiredNotDefault] DateTimeOffset CreatedAt,
    [RequiredNotDefault] DateTimeOffset UpdatedAt,
    [MaxLength(Domain.Shared.Constants.StringMaxLength)] string? ConcurrencyStamp,
    bool IsDeleted = false) : ISyncableModel;