using Domain.Shared;
using System.Linq.Expressions;

namespace Domain;

public interface IDocumentRepository : IRepository<Document>
{
    Task<DocumentDto?> Get(
        Expression<Func<Document, bool>> expression,
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
        int pageSize = 100,
        CancellationToken cancellationToken = default);
}
