using Domain;
using Domain.Shared;
using Infrastructure.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using System.Collections.Immutable;
using System.Linq.Expressions;

namespace Infrastructure.EntityFrameworkCore.Repositories;

class TicketRepository(
    ApplicationDbContext context) : BaseRepository<Ticket>(context), ITicketRepository
{
    public async Task<TicketDto?> Get(
        Expression<Func<Ticket, bool>> expression,
        CancellationToken cancellationToken = default)
    {
        return await _context.Set<Ticket>()
            .Where(expression)
            .ToInternalDto()
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<long>> GetIds(
        Expression<Func<Ticket, bool>> expression,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        return await _context.Set<Ticket>()
            .Where(expression)
            .Select(t => t.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    public Task<PaginatedList<TicketDto>> Paginate(
        TicketFilterDto? filter,
        int pageNumber = Constants.MinPageNumber,
        int pageSize = Constants.MaxPageSize,
        CancellationToken cancellationToken = default) =>
       
        _context.Set<Ticket>()
        .ToPaginatedList<
            Ticket, 
            TicketDto, 
            TicketDto>(
            pageNumber,
            pageSize,
            q => q.ToInternalDto(),
            t => t.CreatedAt,
            filter: q => filter != null ? ApplyFilter(filter) : q,
            cancellationToken);

    public Task<CursorList<TicketDto>> Stream(
        TicketFilterDto? filter,
        Cursor? cursor,
        int pageSize = Constants.MaxPageSize,
        CancellationToken cancellationToken = default) =>

        _context.Set<Ticket>()
        .ToCursorList<
            Ticket,
            InternalTicketDto, 
            TicketDto>(
            cursor,
            pageSize,
            q => q.ToInternalDto(),
            filter: q => filter != null ? ApplyFilter(filter) : q,
            cancellationToken);

    public Task<CursorList<TicketDto>> StreamDeltas(
        Cursor? cursor,
        DateTimeOffset until,
        long? userId = null,
        int pageSize = Constants.MaxPageSize,
        CancellationToken cancellationToken = default) =>

        _context.Set<Ticket>()
        .WhereIf(userId != null, t => t.UserId == userId)
        .ToDeltaCursorList<
            Ticket,
            InternalTicketDto,
            TicketDto>(
            cursor,
            until,
            pageSize,
            q => q.ToInternalDto(),
            cancellationToken);

    private IQueryable<Ticket> ApplyFilter(TicketFilterDto filter)
    {
        return _context.Set<Ticket>()
            .WhereIf(filter.CreatedFrom != null, t => t.CreatedAt >= filter!.CreatedFrom)
            .WhereIf(filter.CreatedTo != null, t => t.CreatedAt <= filter!.CreatedTo)
            .WhereIf(filter.Priorities != null && filter.Priorities.Length > 0, t => filter.Priorities!.Contains(t.Priority))         
            .WhereIf(filter.Statuses != null && filter.Statuses.Length > 0, t => filter.Statuses!.Contains(t.Status ))
            .WhereIf(filter.UpdatedFrom != null, t => t.UpdatedAt >= filter!.UpdatedFrom)
            .WhereIf(filter.UpdatedTo != null, t => t.UpdatedAt <= filter!.UpdatedTo);
    }
}
