using Api.Contracts.Attributes;
using Domain.Shared;
using System.ComponentModel.DataAnnotations;

namespace Api.Contracts.Models.V1;

public record TicketSyncRequest (
    [UniqueMax(maxLength: Constants.MaxLengthOfList, UniquePropertyName = nameof(TicketSyncItem.Id))] List<TicketSyncItem> Items,
    [EnumDataType(typeof(SyncBehavior))] SyncBehavior Behavior = SyncBehavior.Default)
{
    public List<TicketSyncItem> Items { get; init; } = Items ?? [];
}

public record TicketSyncItem(
    [RequiredNotDefault] Guid Id,
    [MinLength(Domain.Shared.Constants.StringMinLength)][MaxLength(Constants.TextMaxLength)] string Description,
    [MinLength(Domain.Shared.Constants.StringMinLength)][MaxLength(Domain.Shared.Constants.StringMaxLength)] string Title,
    [RequiredNotDefault] DateTimeOffset CreatedAt,
    [RequiredNotDefault] DateTimeOffset UpdatedAt,
    [MaxLength(Domain.Shared.Constants.StringMaxLength)] string? ConcurrencyStamp,
    bool IsDeleted = false) : ISyncableModel;
