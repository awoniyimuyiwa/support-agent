using Api.Contracts.Attributes;
using Domain.Shared;
using System.ComponentModel.DataAnnotations;

namespace Api.Contracts.Models.V1;

public record DocumentModel(
    [RequiredNotDefault] Guid Id,
    [RequiredNotDefault] Guid CategoryId,
    [Required] [MinLength(Domain.Shared.Constants.StringMinLength)] [MaxLength(Domain.Shared.Constants.StringMaxLength)] string Name,
    [RequiredNotDefault] DateTimeOffset CreatedAt,
    [RequiredNotDefault] DateTimeOffset UpdatedAt,
    [MaxLength(Domain.Shared.Constants.StringMaxLength)] string? ConcurrencyStamp,
    bool IsDeleted = false) : ISyncableModel;

public record DocumentSyncModel(
    [Required] [UniqueMax(maxLength: Constants.MaxLengthOfList, UniquePropertyName = nameof(DocumentModel.Id))] [UniqueMax(maxLength: Constants.MaxLengthOfList, UniquePropertyName = nameof(DocumentModel.Name))] List<DocumentModel> Items,
    [EnumDataType(typeof(SyncBehavior))] SyncBehavior Behavior = SyncBehavior.Default)
{
    public List<DocumentModel> Items { get; init; } = Items ?? [];
}
