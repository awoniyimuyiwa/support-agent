using Domain.Shared;
using System.Linq.Expressions;

namespace Domain;

public interface IDocumentRepository : IRepository<Document>
{
    Task<DocumentDto?> Get(
        Expression<Func<Document, bool>> expression,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, DocumentDto>> GetByNames(
        IReadOnlyCollection<string> names,
        CancellationToken cancellationToken = default);

    Task<PaginatedList<DocumentDto>> Paginate(
        DocumentFilterDto? filter,
        int pageNumber = Constants.MinPageNumber,
        int pageSize = Constants.MaxPageSize,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<DocumentChunkDto>> SemanticSearch(
        float[] prompt,
        int maxResults = 5,
        CancellationToken cancellationToken = default);

    Task<CursorList<DocumentDto>> Stream(
        DocumentFilterDto? filter,
        Cursor? cursor,
        int pageSize = Constants.MaxPageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves historical modifications and tombstones that fall within a strict server-controlled timeframe.
    /// </summary>
    public Task<CursorList<DocumentDto>> StreamDeltas(
        Cursor? cursor,
        DateTimeOffset until,
        int pageSize = Constants.MaxPageSize,
        CancellationToken cancellationToken = default);
}

