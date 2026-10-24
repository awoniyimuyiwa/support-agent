using Application.Contracts;
using Domain;
using Domain.Shared;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace Application.Services;

class DocumentCategoryService(
    IDocumentCategoryRepository categoryRepository,
    IUnitOfWork uow,
    IAuditLogManager auditLogManager,
    TimeProvider timeProvider,
    [FromKeyedServices(Constants.AuditLogJsonSerializationOptionsName)] JsonSerializerOptions auditJsonSerializerOptions)
    : BaseService(auditLogManager, timeProvider, auditJsonSerializerOptions), IDocumentCategoryService
{
    public async Task<IReadOnlyCollection<DocumentCategoryDto>> Sync(
        DocumentCategorySyncDto input,
        CancellationToken cancellationToken = default)
    {
        if (input.Items.Count == 0) { return []; }

        var conflicts = new List<DocumentCategoryDto>();

        var categoryIds = input.Items
            .Select(x => x.Id)
            .Distinct()
            .ToList();
        var idCategoryMap = await categoryRepository.GetByIds(
            categoryIds,
            includes: [],
            ignoreQueryFilters: true,
            cancellationToken);

        var names = input.Items
            .Select(x => x.Name.Trim())
            .Distinct()
            .ToList();
        var nameCategoryMap = (await categoryRepository.GetByNames(
            names,
            cancellationToken))
            .ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value,
            StringComparer.OrdinalIgnoreCase);

        foreach (var categoryDto in input.Items)
        {
            var finalName = categoryDto.Name.Trim();
            var isNameTaken = nameCategoryMap.TryGetValue(finalName, out var categoryWithName);

            if (idCategoryMap.TryGetValue(categoryDto.Id, out var category))
            {
                // True client-vs-server collision
                var hasConcurrencyConflict = category.ConcurrencyStamp != categoryDto.ConcurrencyStamp;

                // Check if name has been taken by another document
                var hasNameConflict = isNameTaken && categoryWithName!.Id != category.ExternalId;

                if ((hasConcurrencyConflict || hasNameConflict)
                    && input.Behavior != SyncBehavior.ClientWins)
                {
                     conflicts.Add(new DocumentCategoryDto(
                        category.ExternalId,
                        category.Description,
                        category.Name, // Pass back the current name
                        category.CreatedAt,
                        category.UpdatedAt,
                        category.ConcurrencyStamp,
                        category.IsDeleted));
                    continue;
                }

                if (hasNameConflict)
                {
                    finalName = GenerateUniqueName(finalName);
                }

                category.Description = categoryDto.Description;
                category.Name = finalName;
                category.IsDeleted = categoryDto.IsDeleted;

                categoryRepository.Update(category);
            }
            else
            {
                // Row generated offline 

                if (isNameTaken && input.Behavior != SyncBehavior.ClientWins)
                {
                    conflicts.Add(categoryWithName!);
                    continue;
                }

                if (isNameTaken)
                {
                    finalName = GenerateUniqueName(finalName);
                }

                var newCategory = new DocumentCategory(
                   categoryDto.Id,
                   categoryDto.Description,
                   finalName);

                categoryRepository.Add(newCategory);
            }
        }

        await uow.SaveChanges(cancellationToken);

        return [.. conflicts];
    }

    private static string GenerateUniqueName(string targetName) =>
        $"{targetName} (Copy_{Guid.NewGuid().ToString()[..4]})";
}
