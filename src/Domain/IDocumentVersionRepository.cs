using Domain.Shared;

namespace Domain;

public interface IDocumentVersionRepository : IRepository<DocumentVersion>
{
    Task<PaginatedList<DocumentVersionDto>> Paginate(
        DocumentVersionFilterDto? filter,
        int pageNumber = Constants.MinPageNumber,
        int pageSize = Constants.MaxPageSize,
        CancellationToken cancellationToken = default);

    Task<CursorList<DocumentVersionDto>> Stream(
        DocumentVersionFilterDto? filter,
        Cursor? cursor,
        int pageSize = Constants.MaxPageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves historical modifications and tombstones that fall within a strict server-controlled timeframe.
    /// </summary>
    public Task<CursorList<DocumentVersionDto>> StreamDeltas(
        Cursor? cursor,
        DateTimeOffset until,
        int pageSize = Constants.MaxPageSize,
        CancellationToken cancellationToken = default);
}
