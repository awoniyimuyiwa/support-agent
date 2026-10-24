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

public record TicketSyncDto(
    IReadOnlyCollection<TicketDto> Items,
    SyncBehavior Behavior = SyncBehavior.Default)
{
    public IReadOnlyCollection<TicketDto> Items { get; init; } = Items ?? [];
}

