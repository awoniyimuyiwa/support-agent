using System.Linq.Expressions;

namespace Domain;

public interface IRepository<T> where T : Entity
{
    void Add(T entity);

    Task<T?> Find(
        Expression<Func<T, bool>> expression,
        bool forUpdate = false,
        string[]? includes = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<T>> GetAll(
        string[]? includes = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, T>> GetByIds(
        IReadOnlyCollection<Guid> ids,
        string[]? includes = null,
        bool ignoreQueryFilters = false,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, long>> MapIds(
        IReadOnlyCollection<Guid> ids,
        bool ignoreQueryFilters = false,
        CancellationToken cancellationToken = default);

    void Update(T entity);

    Task Reload(
        T entity,
        CancellationToken cancellationToken = default);

    void Remove(T entity);

    Task<int> ExecuteDelete(
        Expression<Func<T, bool>> expression,
        int batchSize = 1000,
        Expression<Func<T, object>>? orderBy = null,
        CancellationToken cancellationToken = default);    
}
