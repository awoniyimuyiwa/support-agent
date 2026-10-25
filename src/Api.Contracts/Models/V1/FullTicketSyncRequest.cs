using Api.Contracts.Attributes;
using Domain.Shared;
using System.ComponentModel.DataAnnotations;

namespace Api.Contracts.Models.V1;

public record FullTicketSyncRequest(
    [UniqueMax(maxLength: Constants.MaxLengthOfList, UniquePropertyName = nameof(TicketSyncItem.Id))] List<FullTicketSyncItem> Items,
    [EnumDataType(typeof(SyncBehavior))] SyncBehavior Behavior = SyncBehavior.Default)
{
    public List<FullTicketSyncItem> Items { get; init; } = Items ?? [];
}

public record FullTicketSyncItem(
    [RequiredNotDefault] Guid Id,
    [MinLength(Domain.Shared.Constants.StringMinLength)][MaxLength(Constants.TextMaxLength)] string Description,
    [EnumDataType(typeof(TicketPriority))] TicketPriority Priority,
    [EnumDataType(typeof(TicketStatus))] TicketStatus Status,
    [MinLength(Domain.Shared.Constants.StringMinLength)][MaxLength(Domain.Shared.Constants.StringMaxLength)] string Title,
    [RequiredNotDefault] DateTimeOffset CreatedAt,
    [RequiredNotDefault] DateTimeOffset UpdatedAt,
    [MaxLength(Domain.Shared.Constants.StringMaxLength)] string? ConcurrencyStamp,
    bool IsDeleted = false) : TicketSyncItem(
        Id,
        Description,
        Title,
        CreatedAt,
        UpdatedAt,
        ConcurrencyStamp,
        IsDeleted);
