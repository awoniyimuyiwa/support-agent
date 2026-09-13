using Domain.Shared;

namespace Api.Contracts.Models.V1;

public record TicketModel(
    Guid Id,
    string Description,
    TicketPriority Priority,
    TicketStatus Status,
    string Title,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? ConcurrencyStamp,
    bool IsDeleted = false) : ISyncableModel;

public record TicketSyncModel(
    List<TicketModel> Items,
    SyncBehavior Behavior = SyncBehavior.Default);