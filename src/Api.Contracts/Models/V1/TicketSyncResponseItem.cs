using Domain.Shared;

namespace Api.Contracts.Models.V1;

public record TicketSyncResponseItem(
    Guid Id,
    Guid CreatedByUserId,
    string Description,
    TicketPriority Priority,
    TicketStatus Status,
    string Title,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? ConcurrencyStamp,
    bool IsDeleted = false) : TicketSyncRequestItem(
        Id,
        Description,
        Title,
        CreatedAt,
        UpdatedAt,
        ConcurrencyStamp,
        IsDeleted), ISyncableModel;
