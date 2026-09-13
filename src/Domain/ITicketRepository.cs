using Domain.Shared;
using System.Collections.Immutable;
using System.Linq.Expressions;

namespace Domain;

public interface ITicketRepository : IRepository<Ticket>
{
    Task<TicketDto?> Get(
        Expression<Func<Ticket, bool>> expression,
        CancellationToken cancellationToken = default);

    Task<ImmutableDictionary<Guid, Ticket>> GetByIds(
        ImmutableList<Guid> ids,
        CancellationToken cancellationToken = default);

    Task<PaginatedList<TicketDto>> Paginate(
        TicketFilterDto? filter,
        int pageNumber = Constants.MinPageNumber,
        int pageSize = Constants.MaxPageSize,
        CancellationToken cancellationToken = default);

    Task<CursorList<TicketDto>> Stream(
        TicketFilterDto? filter,
        Cursor? cursor,
        int pageSize = Constants.MaxPageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves historical modifications and tombstones that fall within a strict server-controlled timeframe.
    /// </summary>
    public Task<CursorList<TicketDto>> StreamDeltas(
        Cursor? cursor,
        DateTimeOffset until,
        int pageSize = Constants.MaxPageSize,
        CancellationToken cancellationToken = default);
}

