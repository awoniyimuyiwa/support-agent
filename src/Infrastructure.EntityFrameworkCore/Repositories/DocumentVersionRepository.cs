using Domain;
using Domain.Shared;
using Infrastructure.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Infrastructure.EntityFrameworkCore.Repositories;

class DocumentVersionRepository(
    ApplicationDbContext context) : BaseRepository<DocumentVersion>(context), IDocumentVersionRepository
{
    public async Task<DocumentVersionDto?> Get(
        Expression<Func<DocumentVersion, bool>> expression,
        CancellationToken cancellationToken = default)
    {
        return await _context.Set<DocumentVersion>()
            .Where(expression)
            .ToDto()
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<PaginatedList<DocumentVersionDto>> Paginate(
        DocumentVersionFilterDto? filter,
        int pageNumber = Constants.MinPageNumber,
        int pageSize = Constants.MaxPageSize,
        CancellationToken cancellationToken = default) =>

        _context.Set<DocumentVersion>()
        .ToPaginatedList<
            DocumentVersion,
            DocumentVersionDto,
            DocumentVersionDto>(
            pageNumber,
            pageSize,
            q => q.ToInternalDto(),
            t => t.CreatedAt,
            filter: q => filter != null ? ApplyFilter(filter) : q,
            cancellationToken);

    public Task<CursorList<DocumentVersionDto>> Stream(
        DocumentVersionFilterDto? filter,
        Cursor? cursor,
        int pageSize = Constants.MaxPageSize,
        CancellationToken cancellationToken = default) =>

        _context.Set<DocumentVersion>()
        .ToCursorList<
            DocumentVersion,
            InternalDocumentVersionDto,
            DocumentVersionDto>(
            cursor,
            pageSize,
            q => q.ToInternalDto(),
            filter: q => filter != null ? ApplyFilter(filter) : q,
            cancellationToken);

    public Task<CursorList<DocumentVersionDto>> StreamDeltas(
        Cursor? cursor,
        DateTimeOffset until,
        int pageSize = Constants.MaxPageSize,
        CancellationToken cancellationToken = default) =>

        _context.Set<DocumentVersion>()
        .ToDeltaCursorList<
            DocumentVersion,
            InternalDocumentVersionDto,
            DocumentVersionDto>(
            cursor,
            until,
            pageSize,
            q => q.ToInternalDto(),
            cancellationToken);

    private IQueryable<DocumentVersion> ApplyFilter(DocumentVersionFilterDto filter)
    {
        return _context.Set<DocumentVersion>()
            .Where(dv => dv.ExternalId == filter.DocumentId)
            .WhereIf(filter.CreatedFrom != null, dv => dv.CreatedAt >= filter!.CreatedFrom)
            .WhereIf(filter.CreatedTo != null, dv => dv.CreatedAt <= filter!.CreatedTo)      
            .WhereIf(filter.UpdatedFrom != null, dv => dv.UpdatedAt >= filter!.UpdatedFrom)
            .WhereIf(filter.UpdatedTo != null, dv => dv.UpdatedAt <= filter!.UpdatedTo);
    }
}