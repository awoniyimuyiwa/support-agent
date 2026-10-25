using Api.Contracts.Attributes;
using Domain.Shared;
using System.ComponentModel.DataAnnotations;

namespace Api.Contracts.Models.V1;

public record DocumentCategorySyncRequest(
    [Required] [UniqueMax(maxLength: Constants.MaxLengthOfList, UniquePropertyName = nameof(DocumentCategorySyncItem.Id))] [UniqueMax(maxLength: Constants.MaxLengthOfList, UniquePropertyName = nameof(DocumentCategorySyncItem.Name))] List<DocumentCategorySyncItem> Items,
    [EnumDataType(typeof(SyncBehavior))] SyncBehavior Behavior = SyncBehavior.Default)
{
    public List<DocumentCategorySyncItem> Items { get; init; } = Items ?? [];
}

public record DocumentCategorySyncItem(
    [RequiredNotDefault] Guid Id,
    [MinLength(Domain.Shared.Constants.StringMinLength)][MaxLength(Constants.TextMaxLength)] string? Description,
    [Required][MinLength(Domain.Shared.Constants.StringMinLength)][MaxLength(Domain.Shared.Constants.StringMaxLength)] string Name,
    [RequiredNotDefault] DateTimeOffset CreatedAt,
    [RequiredNotDefault] DateTimeOffset UpdatedAt,
    [MaxLength(Domain.Shared.Constants.StringMaxLength)] string? ConcurrencyStamp,
    bool IsDeleted = false) : ISyncableModel;
