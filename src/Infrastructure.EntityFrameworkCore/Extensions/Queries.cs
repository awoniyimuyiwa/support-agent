using Domain;
using Domain.BackgroundJobs;
using Domain.Shared;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using static System.Net.Mime.MediaTypeNames;
using static System.Net.WebRequestMethods;

namespace Infrastructure.EntityFrameworkCore.Extensions;

static class Queries
{
    extension<T>(IQueryable<T> query)
    {
        /// <summary>
        /// Conditionally applies a filter predicate to the query pipeline if the condition is met.
        /// </summary>
        public IQueryable<T> WhereIf(bool condition, Expression<Func<T, bool>> predicate)
        {
            return condition ? query.Where(predicate) : query;
        }
    }

  
    extension<TEntity>(IQueryable<TEntity> query) where TEntity : class, IHasCursor
    {
        /// <summary>
        /// Executes an asynchronous database compilation pass, projecting data into a paged cursor envelope.
        /// </summary>
        public async Task<CursorList<TResultDto>> ToCursorList<TInternalDto, TResultDto>(
            Cursor? cursor,
            int pageSize,
            Func<IQueryable<TEntity>, IQueryable<TInternalDto>> projector,
            Func<IQueryable<TEntity>, IQueryable<TEntity>>? filter = null,
            CancellationToken cancellationToken = default)
            where TInternalDto : class, IHasInternalCursor, TResultDto
            where TResultDto : class
        {
            var activeQuery = filter != null ? filter(query) : query;

            pageSize = Math.Clamp(
                pageSize,
                Constants.MinPageSize,
                Constants.MaxPageSize);

            // Filter and Page on the DB side
            var items = await projector(activeQuery
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.Id)
                .WhereIf(cursor != null,
                    x => x.CreatedAt < cursor!.Timestamp
                         || (x.CreatedAt == cursor.Timestamp && x.Id < cursor!.LastId))
                .Take(pageSize))
                .ToListAsync(cancellationToken);

            // Generate the next cursor from the last item
            var last = items.LastOrDefault();

            // Only append token if data matches full pageSize limits to skip empty page requests
            var next = (last != null && items.Count == pageSize)
                ? new Cursor(last.InternalId, last.CreatedAt)
                : null;

            // Return as the public DTO type (TResult)
            return new CursorList<TResultDto>(
                [.. items.Cast<TResultDto>()],
                next?.ToJson(),
                pageSize);
        }

        /// <summary>
        /// Executes an asynchronous database compilation pass, projecting data into an offset paginated envelope.
        /// </summary>
        public async Task<PaginatedList<TResultDto>> ToPaginatedList<TInternalDto, TResultDto>(
            int pageNumber,
            int pageSize,
            Func<IQueryable<TEntity>, IQueryable<TInternalDto>> projector,
            Expression<Func<TEntity, object>> orderBy,
            Func<IQueryable<TEntity>, IQueryable<TEntity>>? filter = null,
            CancellationToken cancellationToken = default)
            where TInternalDto : class, TResultDto
            where TResultDto : class
        {
            var activeQuery = filter != null ? filter(query) : query;

            pageSize = Math.Clamp(
                pageSize,
                Constants.MinPageSize,
                Constants.MaxPageSize);

            int maxPageNumber = (Constants.MaxRowsToSkip / pageSize) + 1;
            pageNumber = Math.Clamp(
                pageNumber,
                Constants.MinPageNumber,
                maxPageNumber);

            var count = await activeQuery
                .Take(Constants.MaxRowsToSkip + 1)
                .CountAsync(cancellationToken);

            // Apply sorting and paging on the Entity, then project
            var items = await projector(activeQuery
                .OrderByDescending(orderBy)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize))
                .ToListAsync(cancellationToken);

            // Cast the list of internal records to the public record type
            return new PaginatedList<TResultDto>(
                [.. items.Cast<TResultDto>()],
                count,
                pageNumber,
                pageSize);
        }
    }

    extension<TEntity>(IQueryable<TEntity> query) where TEntity : class, IHasDeltaCursor
    {
        /// <summary>
        /// Executes an asynchronous database compilation pass, projecting data into a paged cursor envelope.
        /// Supports nullable cursors natively for fresh start baseline initialization passes.
        /// </summary>
        public async Task<CursorList<TResultDto>> ToDeltaCursorList<TInternalDto, TResultDto>(
            Cursor? cursor,
            DateTimeOffset until,
            int pageSize,
            Func<IQueryable<TEntity>, IQueryable<TInternalDto>> projector,
            CancellationToken cancellationToken = default)
            where TInternalDto : class, IHasInternalDeltaCursor, TResultDto
            where TResultDto : class
        {
            pageSize = Math.Clamp(
                pageSize,
                Constants.MinPageSize,
                Constants.MaxPageSize);

            var activeQuery = query.IgnoreQueryFilters();

            var lastId = cursor?.LastId ?? 0L;
            var since = cursor?.Timestamp ?? DateTimeOffset.MinValue;

            // Compile the composite tie-breaker keyset cursor conditions cleanly using safe locals
            if (lastId > 0L)
            {
                activeQuery = activeQuery.Where(x =>
                    (x.UpdatedAt > since && x.UpdatedAt <= until)
                    || (x.UpdatedAt == since && x.Id > lastId && x.UpdatedAt <= until));
            }
            else
            {
                activeQuery = activeQuery.Where(x =>
                    x.UpdatedAt > since && x.UpdatedAt <= until);
            }

            // Force definitive Ascending B-Tree index seek ordering and stream page slice
            var items = await projector(activeQuery
                .OrderBy(x => x.UpdatedAt)
                .ThenBy(x => x.Id)
                .Take(pageSize))
                .ToListAsync(cancellationToken);
          
            var last = items.LastOrDefault();

             var next = (last != null && items.Count == pageSize)
                ? new Cursor(last.InternalId, last.UpdatedAt)
                : null;

            return new CursorList<TResultDto>(
                [.. items.Cast<TResultDto>()],
                next?.ToJson(),
                pageSize);
        }
    }

    extension(IQueryable<AuditLog> query)
    {
        public IQueryable<InternalAuditLogDto> ToInternalDto()
        {
            return query.Select(al => new InternalAuditLogDto(
                al.Id,
                al.ExternalId,
                al.BrowserInfo,
                al.Duration,
                al.Exception,
                al.HttpMethod,
                al.IpAddress,
                al.RequestId,
                al.StatusCode,
                al.CreatedAt,
                al.Url,
                al.User.ExternalId));
        }
    }

    extension(IQueryable<BackgroundJob> query)
    {
        public IQueryable<InternalBackgroundJobDto> ToInternalDto()
        {
            return query.Select(bj => new InternalBackgroundJobDto(
                bj.Id,
                bj.ExternalId,
                bj.Attempts,
                bj.Error,
                bj.MaxRetries,
                bj.Payload,
                bj.Priority,
                bj.ScheduledFor,
                bj.SourceId,
                bj.Status,
                bj.TraceId,
                bj.Type,
                bj.CreatedAt,
                bj.UpdatedAt,
                bj.ConcurrencyStamp));
        }
    }

    extension(IQueryable<Ticket> query)
    {
        public IQueryable<TicketDto> ToDto()
        {
            return query.Select(t => new TicketDto(
                t.ExternalId,
                t.Description,
                t.Priority,
                t.Status,
                t.Title,
                t.CreatedAt,
                t.UpdatedAt,
                t.ConcurrencyStamp,
                t.IsDeleted));
        }
    }

    extension(IQueryable<Ticket> query)
    {
        public IQueryable<InternalTicketDto> ToInternalDto()
        {
            return query.Select(t => new InternalTicketDto(
                t.Id,
                t.ExternalId,
                t.Description,
                t.Priority,
                t.Status,
                t.Title,
                t.CreatedAt,
                t.UpdatedAt,
                t.ConcurrencyStamp,
                t.IsDeleted));
        }
    }

    extension(IQueryable<ApplicationUser> query)
    {
        public IQueryable<UserDto> ToDto()
        {
            return query.Select(u => new UserDto(
                u.Email,
                u.FirstName,
                u.Id,
                u.EmailConfirmed,
                u.LastName,
                u.LockoutEnd,
                u.UserName,
                u.CreatedAt,
                u.UpdatedAt,
                u.ConcurrencyStamp));
        }
    }
}
