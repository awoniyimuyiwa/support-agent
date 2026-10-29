using Api.Contracts.Attributes;
using Domain.Shared;
using System.ComponentModel.DataAnnotations;

namespace Api.Contracts.Models.V1;

public record FullTicketSyncRequest(
    [UniqueMax(maxLength: Constants.MaxLengthOfList, UniquePropertyName = nameof(TicketSyncRequestItem.Id))] List<FullTicketSyncRequestItem> Items,
    [EnumDataType(typeof(SyncBehavior))] SyncBehavior Behavior = SyncBehavior.Default)
{
    public List<FullTicketSyncRequestItem> Items { get; init; } = Items ?? [];
}

public record FullTicketSyncRequestItem(
    [RequiredNotDefault] Guid Id,
    [MinLength(Domain.Shared.Constants.StringMinLength)][MaxLength(Constants.TextMaxLength)] string Description,
    [EnumDataType(typeof(TicketPriority))] TicketPriority Priority,
    [EnumDataType(typeof(TicketStatus))] TicketStatus Status,
    [MinLength(Domain.Shared.Constants.StringMinLength)][MaxLength(Domain.Shared.Constants.StringMaxLength)] string Title,
    [RequiredNotDefault] DateTimeOffset CreatedAt,
    [RequiredNotDefault] DateTimeOffset UpdatedAt,
    [MaxLength(Domain.Shared.Constants.StringMaxLength)] string? ConcurrencyStamp,
    bool IsDeleted = false) : TicketSyncRequestItem(
        Id,
        Description,
        Title,
        CreatedAt,
        UpdatedAt,
        ConcurrencyStamp,
        IsDeleted);
