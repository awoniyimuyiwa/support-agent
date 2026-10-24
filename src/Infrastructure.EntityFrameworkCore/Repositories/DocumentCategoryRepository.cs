using Domain;
using Domain.Shared;
using Infrastructure.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using System.Collections.Immutable;
using System.Linq.Expressions;

namespace Infrastructure.EntityFrameworkCore.Repositories;

class DocumentCategoryRepository(
    ApplicationDbContext context) : BaseRepository<DocumentCategory>(context), IDocumentCategoryRepository
{
    public async Task<DocumentCategoryDto?> Get(
        Expression<Func<DocumentCategory, bool>> expression,
        CancellationToken cancellationToken = default)
    {
        return await _context.Set<DocumentCategory>()
            .Where(expression)
            .ToInternalDto()
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, DocumentCategoryDto>> GetByNames(
        IReadOnlyCollection<string> names,
        CancellationToken cancellationToken = default)
    {
        IQueryable<DocumentCategory> query = _context.Set<DocumentCategory>();

        return await query
            .IgnoreQueryFilters()
            .Where(x => names.Contains(x.Name))
            .ToDto()
            .ToDictionaryAsync(x => x.Name, cancellationToken);
    }

    public Task<PaginatedList<DocumentCategoryDto>> Paginate(
        DocumentCategoryFilterDto? filter,
        int pageNumber = Constants.MinPageNumber,
        int pageSize = Constants.MaxPageSize,
        CancellationToken cancellationToken = default) =>
       
        _context.Set<DocumentCategory>()
        .ToPaginatedList<
            DocumentCategory, 
            DocumentCategoryDto, 
            DocumentCategoryDto>(
            pageNumber,
            pageSize,
            q => q.ToInternalDto(),
            t => t.CreatedAt,
            filter: q => filter != null ? ApplyFilter(filter) : q,
            cancellationToken);

    public async Task<bool> Restore(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        // Fetch the soft-deleted target category row by ignoring global query filters
        var categoryToRestore = await _context.DocumentCategories
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(dc => dc.ExternalId == id && dc.IsDeleted, cancellationToken);

        if (categoryToRestore is null) return false;

        // Check if an active category has already claimed this name string
        bool nameAlreadyExists = await _context.DocumentCategories
            .AnyAsync(c => c.Name == categoryToRestore.Name, cancellationToken); // Automatically checks active items only

        if (nameAlreadyExists)
        {
            // Fail gracefully with a clear domain warning instead of letting the database crash
            throw new InvalidOperationException(
                $"Cannot restore category '{categoryToRestore.Name}' because an active category " +
                "with the exact same name already exists. Please rename the active category first.");
        }

        // Safe to restore
        categoryToRestore.IsDeleted = false;

        return true;
    }

    public Task<CursorList<DocumentCategoryDto>> Stream(
        DocumentCategoryFilterDto? filter,
        Cursor? cursor,
        int pageSize = Constants.MaxPageSize,
        CancellationToken cancellationToken = default) =>

        _context.Set<DocumentCategory>()
        .ToCursorList<
            DocumentCategory,
            InternalDocumentCategoryDto, 
            DocumentCategoryDto>(
            cursor,
            pageSize,
            q => q.ToInternalDto(),
            filter: q => filter != null ? ApplyFilter(filter) : q,
            cancellationToken);

    public Task<CursorList<DocumentCategoryDto>> StreamDeltas(
        Cursor? cursor,
        DateTimeOffset until,
        int pageSize = Constants.MaxPageSize,
        CancellationToken cancellationToken = default) =>

        _context.Set<DocumentCategory>()
        .ToDeltaCursorList<
            DocumentCategory,
            InternalDocumentCategoryDto,
            DocumentCategoryDto>(
            cursor,
            until,
            pageSize,
            q => q.ToInternalDto(),
            cancellationToken);

    private IQueryable<DocumentCategory> ApplyFilter(DocumentCategoryFilterDto filter)
    {
        return _context.Set<DocumentCategory>()
            .WhereIf(filter.CreatedFrom != null, dc => dc.CreatedAt >= filter!.CreatedFrom)
            .WhereIf(filter.CreatedTo != null, dc => dc.CreatedAt <= filter!.CreatedTo)
            .WhereIf(filter.UpdatedFrom != null, dc => dc.UpdatedAt >= filter!.UpdatedFrom)
            .WhereIf(filter.UpdatedTo != null, dc => dc.UpdatedAt <= filter!.UpdatedTo)
            .WhereIf(filter.SearchTerm != null, dc => EF.Functions.ILike(dc.Name, $"%{filter!.SearchTerm}%")); // Case-insensitive partial matching
    }
}
