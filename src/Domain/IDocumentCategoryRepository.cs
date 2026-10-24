using Domain.Shared;
using System.Linq.Expressions;

namespace Domain;

public interface IDocumentCategoryRepository : IRepository<DocumentCategory>
{
    Task<DocumentCategoryDto?> Get(
      Expression<Func<DocumentCategory, bool>> expression,
      CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, DocumentCategoryDto>> GetByNames(
        IReadOnlyCollection<string> names,
        CancellationToken cancellationToken = default);

    Task<PaginatedList<DocumentCategoryDto>> Paginate(
        DocumentCategoryFilterDto? filter,
        int pageNumber = Constants.MinPageNumber,
        int pageSize = Constants.MaxPageSize,
        CancellationToken cancellationToken = default);

    Task<bool> Restore(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<CursorList<DocumentCategoryDto>> Stream(
        DocumentCategoryFilterDto? filter,
        Cursor? cursor,
        int pageSize = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves historical modifications and tombstones that fall within a strict server-controlled timeframe.
    /// </summary>
    public Task<CursorList<DocumentCategoryDto>> StreamDeltas(
        Cursor? cursor,
        DateTimeOffset until,
        int pageSize = Constants.MaxPageSize,
        CancellationToken cancellationToken = default);
}
