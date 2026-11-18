using Api.Contracts.Attributes;
using Domain.Shared;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Api.Contracts.Models.V1;

public record FullTicketSyncRequest(
    [UniqueMax(maxLength: Constants.MaxLengthOfList, UniquePropertyName = nameof(TicketSyncRequestItem.Id))] List<FullTicketSyncRequestItem> Items,
    [EnumDataType(typeof(SyncBehavior))][property: JsonConverter(typeof(JsonStringEnumConverter))] SyncBehavior Behavior = SyncBehavior.Default)
{
    public List<FullTicketSyncRequestItem> Items { get; init; } = Items ?? [];
}

public record FullTicketSyncRequestItem(
    [RequiredNotDefault] Guid Id,
    [MinLength(Domain.Shared.Constants.StringMinLength)][MaxLength(Constants.TextMaxLength)] string Description,
    [EnumDataType(typeof(TicketPriority))][property: JsonConverter(typeof(JsonStringEnumConverter))] TicketPriority Priority,
    [EnumDataType(typeof(TicketStatus))][property: JsonConverter(typeof(JsonStringEnumConverter))] TicketStatus Status,
    [MinLength(Domain.Shared.Constants.StringMinLength)][MaxLength(Domain.Shared.Constants.StringMaxLength)] string Title,
    [RequiredNotDefault] DateTimeOffset CreatedAt,
    [RequiredNotDefault] DateTimeOffset UpdatedAt,
    [MaxLength(Domain.Shared.Constants.StringMaxLength)] string? ConcurrencyStamp,
    bool IsDeleted) : TicketSyncRequestItem(
        Id,
        Description,
        Title,
        CreatedAt,
        UpdatedAt,
        ConcurrencyStamp,
        IsDeleted);
