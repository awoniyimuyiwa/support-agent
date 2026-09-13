using Domain;
using Domain.Shared;
using System.Collections.Immutable;

namespace Application.Contracts;

public interface ITicketService
{
    /// <summary>
    /// Synchronizes tickets between client and server.
    /// </summary>
    /// <param name="input"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>Tickets with conflicts</returns>
    Task<ImmutableList<TicketDto>> Sync(
        TicketSyncDto input,
        CancellationToken cancellationToken = default);
}

public record TicketSyncDto(
    ImmutableList<TicketDto> Tickets,
    SyncBehavior Behavior = SyncBehavior.Default);
