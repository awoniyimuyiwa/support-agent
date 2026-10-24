using Domain;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Infrastructure.EntityFrameworkCore.Repositories;

abstract class BaseRepository<T>(ApplicationDbContext context) : IRepository<T> where T : Entity
{
    protected readonly ApplicationDbContext _context = context;

    public virtual void Add(T entity) => _context.Add(entity);

    public virtual async Task<T?> Find(
        Expression<Func<T, bool>> expression,
        bool forUpdate = false,
        string[]? includes = null,
        CancellationToken cancellationToken = default)
    {
        IQueryable<T> query = _context.Set<T>();
        if (!forUpdate) query = query.AsNoTracking();
        if (includes != null)
            foreach (var path in includes) query = query.Include(path);

        return await query.FirstOrDefaultAsync(expression, cancellationToken);
    }

    public virtual async Task<IReadOnlyCollection<T>> GetAll(
        string[]? includes = null,
        CancellationToken cancellationToken = default)
    {
        IQueryable<T> query = _context.Set<T>();
        if (includes != null)
            foreach (var path in includes) query = query.Include(path);

        return await query
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public virtual async Task<IReadOnlyDictionary<Guid, T>> GetByIds(
        IReadOnlyCollection<Guid> ids,
        string[]? includes = null,
        bool ignoreQueryFilters = false,
        CancellationToken cancellationToken = default)
    {
        IQueryable<T> query = _context.Set<T>();
        if (includes != null)
        {
            foreach (var path in includes) query = query.Include(path);
        }

        if (ignoreQueryFilters)
        {
            query = query.IgnoreQueryFilters();
        }

        return await query
            .Where(x => ids.Contains(x.ExternalId))
            .ToDictionaryAsync(x => x.ExternalId, cancellationToken);
    }

    public virtual async Task<IReadOnlyDictionary<Guid, long>> MapIds(
        IReadOnlyCollection<Guid> ids,
        bool ignoreQueryFilters = false,
        CancellationToken cancellationToken = default)
    {
        IQueryable<T> query = _context.Set<T>();
      
        if (ignoreQueryFilters)
        {
            query = query.IgnoreQueryFilters();
        }

        return await query
            .Where(x => ids.Contains(x.ExternalId))
            .ToDictionaryAsync(x => x.ExternalId, x => x.Id, cancellationToken);
    }

    public virtual void Update(T entity) 
    {
        _context.Update(entity);
    }

    public Task Reload(
        T entity,
        CancellationToken cancellationToken = default) =>
        _context.Entry(entity)
        .ReloadAsync(cancellationToken);

    public virtual void Remove(T entity) => _context.Remove(entity);

    public Task<int> ExecuteDelete(
        Expression<Func<T, bool>> expression,
        int batchSize = 1000,
        Expression<Func<T, object>>? orderBy = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Set<T>().Where(expression);

        // Apply ordering only if a sorting selector was explicitly provided
        if (orderBy is not null)
        {
            query = query.OrderBy(orderBy);
        }

        return query
            .Take(batchSize)
            .ExecuteDeleteAsync(cancellationToken);

    }
}
