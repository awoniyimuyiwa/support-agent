using Api.Contracts.Attributes;
using Domain.Shared;
using System.ComponentModel.DataAnnotations;

namespace Api.Contracts.Models.V1;

public record TicketSyncRequest (
    [UniqueMax(maxLength: Constants.MaxLengthOfList, UniquePropertyName = nameof(TicketSyncRequestItem.Id))] List<TicketSyncRequestItem> Items,
    [EnumDataType(typeof(SyncBehavior))] SyncBehavior Behavior = SyncBehavior.Default)
{
    public List<TicketSyncRequestItem> Items { get; init; } = Items ?? [];
}

public record TicketSyncRequestItem(
    [RequiredNotDefault] Guid Id,
    [MinLength(Domain.Shared.Constants.StringMinLength)][MaxLength(Constants.TextMaxLength)] string Description,
    [MinLength(Domain.Shared.Constants.StringMinLength)][MaxLength(Domain.Shared.Constants.StringMaxLength)] string Title,
    [RequiredNotDefault] DateTimeOffset CreatedAt,
    [RequiredNotDefault] DateTimeOffset UpdatedAt,
    [MaxLength(Domain.Shared.Constants.StringMaxLength)] string? ConcurrencyStamp,
    bool IsDeleted = false);
