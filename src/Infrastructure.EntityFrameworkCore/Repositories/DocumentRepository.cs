using Domain;
using Domain.Shared;
using Infrastructure.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Infrastructure.EntityFrameworkCore.Repositories;

class DocumentRepository(
    ApplicationDbContext context) : BaseRepository<Document>(context), IDocumentRepository
{
    public async Task<DocumentDto?> Get(
        Expression<Func<Document, bool>> expression,
        CancellationToken cancellationToken = default)
    {
        return await _context.Set<Document>()
            .Where(expression)
            .ToInternalDto()
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, DocumentDto>> GetByNames(
        IReadOnlyCollection<string> names,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Document> query = _context.Set<Document>();

        return await query
            .IgnoreQueryFilters()
            .Where(x => names.Contains(x.Name))
            .ToDto()
            .ToDictionaryAsync(x => x.Name, cancellationToken);
    }

    public Task<PaginatedList<DocumentDto>> Paginate(
        DocumentFilterDto? filter,
        int pageNumber = Constants.MinPageNumber,
        int pageSize = Constants.MaxPageSize,
        CancellationToken cancellationToken = default) =>
       
        _context.Set<Document>()
        .ToPaginatedList<
            Document, 
            DocumentDto, 
            DocumentDto>(
            pageNumber,
            pageSize,
            q => q.ToInternalDto(),
            t => t.CreatedAt,
            filter: q => filter != null ? ApplyFilter(filter) : q,
            cancellationToken);

    public async Task<IReadOnlyCollection<DocumentChunkDto>> SemanticSearch(
        float[] prompt,
        int maxResults = 5,
        CancellationToken cancellationToken = default)
    {
        // Temporarily increase dynamic search window evaluation sizes (Default is 40) 
        // Highly recommended for accuracy.
        await _context.Database.ExecuteSqlRawAsync(
             "SET local pgvector.ef_search = 100;", 
             cancellationToken);
    
        var results = await _context.DocumentChunks
           .Where(dc => dc.Vector != null
                        // The version & parent document IsDeleted checks are injected automatically by EF Core
                        && dc.DocumentVersion.CreatedAt == _context.DocumentVersions
                        .Where(dv => dv.DocumentId == dc.DocumentVersion.DocumentId)
                        .Max(dv => dv.CreatedAt))
           .Select(dc => new
            {
               Distance = dc.Vector!.CosineDistance(prompt),
               dc.DocumentVersion.DocumentId,
               dc.DocumentVersionId,
               dc.Content,
               dc.Index
           }).OrderBy(dc => dc.Distance)
           .Take(maxResults)
           .ToListAsync(cancellationToken);

        return [.. results
            .Where(r => r.Distance < 0.6)
            .Select(r => new DocumentChunkDto(
                r.Distance,
                r.DocumentId,
                r.DocumentVersionId,
                r.Content,
                r.Index))];
    }

    public Task<CursorList<DocumentDto>> Stream(
        DocumentFilterDto? filter,
        Cursor? cursor,
        int pageSize = Constants.MaxPageSize,
        CancellationToken cancellationToken = default) =>

        _context.Set<Document>()
        .ToCursorList<
            Document,
            InternalDocumentDto, 
            DocumentDto>(
            cursor,
            pageSize,
            q => q.ToInternalDto(),
            filter: q => filter != null ? ApplyFilter(filter) : q,
            cancellationToken);

    public Task<CursorList<DocumentDto>> StreamDeltas(
        Cursor? cursor,
        DateTimeOffset until,
        int pageSize = Constants.MaxPageSize,
        CancellationToken cancellationToken = default) =>

        _context.Set<Document>()
        .ToDeltaCursorList<
            Document,
            InternalDocumentDto,
            DocumentDto>(
            cursor,
            until,
            pageSize,
            q => q.ToInternalDto(),
            cancellationToken);

    private IQueryable<Document> ApplyFilter(DocumentFilterDto filter)
    {
        return _context.Set<Document>()
            .WhereIf(filter.Categories != null && filter.Categories.Length > 0, d => filter.Categories!.Contains(d.DocumentCategory.ExternalId))
            .WhereIf(filter.CreatedFrom != null, d => d.CreatedAt >= filter!.CreatedFrom)
            .WhereIf(filter.CreatedTo != null, d => d.CreatedAt <= filter!.CreatedTo)           
            .WhereIf(filter.UpdatedFrom != null, d => d.UpdatedAt >= filter!.UpdatedFrom)
            .WhereIf(filter.UpdatedTo != null, d => d.UpdatedAt <= filter!.UpdatedTo)
            .WhereIf(filter.SearchTerm != null, d => EF.Functions.ILike(d.Name, $"%{filter!.SearchTerm}%")); // Case-insensitive partial matching;
    }
}
