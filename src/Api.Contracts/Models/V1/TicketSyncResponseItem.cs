using Domain.Shared;
using System.Text.Json.Serialization;

namespace Api.Contracts.Models.V1;

public record TicketSyncResponseItem(
    Guid Id,
    Guid CreatedByUserId,
    string Description,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] TicketPriority Priority,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] TicketStatus Status,
    string Title,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? ConcurrencyStamp,
    bool IsDeleted) : TicketSyncRequestItem(
        Id,
        Description,
        Title,
        CreatedAt,
        UpdatedAt,
        ConcurrencyStamp,
        IsDeleted), ISyncableModel;
