using Domain;
using Domain.Shared;

namespace Application.Contracts;

public interface ITicketService
{
    /// <summary>
    /// Synchronizes tickets between client and server.
    /// </summary>
    /// <param name="input"></param>
    /// <param name="userId"></param>
    /// <param name="isAdmin"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>Tickets with conflicts</returns>
    Task<IReadOnlyCollection<TicketDto>> Sync(
        TicketSyncDto input,
        long userId,
        bool isAdmin,
        CancellationToken cancellationToken = default);
}

public record TicketSyncItemDto(
    Guid Id,
    string Description,
    TicketPriority Priority,
    TicketStatus Status,
    string Title,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? ConcurrencyStamp,
    bool IsDeleted = false);

public record TicketSyncDto(
    IReadOnlyCollection<TicketSyncItemDto> Items,
    SyncBehavior Behavior = SyncBehavior.Default)
{
    public IReadOnlyCollection<TicketSyncItemDto> Items { get; init; } = Items ?? [];
}