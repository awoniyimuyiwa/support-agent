using Application.Contracts;
using Domain;
using Domain.Shared;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace Application.Services;

class DocumentService(
    IDocumentRepository documentRepository,
    IDocumentVersionRepository versionRepository,
    IUnitOfWork uow,
    IAuditLogManager auditLogManager,
    TimeProvider timeProvider,
    [FromKeyedServices(Constants.AuditLogJsonSerializationOptionsName)] JsonSerializerOptions auditJsonSerializerOptions)
    : BaseService(auditLogManager, timeProvider, auditJsonSerializerOptions), IDocumentService
{
    public async Task<IReadOnlyCollection<DocumentDto>> Sync(
        DocumentSyncDto input,
        CancellationToken cancellationToken = default)
    {
        if (input.Items.Count == 0) { return []; }

        var conflicts = new List<DocumentDto>();

        var documentIds = input.Items
            .Select(x => x.Id)
            .ToList();

        var idDocumentMap = await documentRepository.GetByIds(
            documentIds,
            includes: [nameof(Document.DocumentCategory)],
            ignoreQueryFilters: true,
            cancellationToken);

        var names = input.Items
            .Select(x => x.Name.Trim())
            .Distinct()
            .ToList();
        var nameDocumentDtoMap = (await documentRepository.GetByNames(
            names,
            cancellationToken))
            .ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value,
            StringComparer.OrdinalIgnoreCase);

        foreach (var syncItemDto in input.Items)
        { 
            var finalName = syncItemDto.Name.Trim();
            var isNameTaken = nameDocumentDtoMap.TryGetValue(finalName, out var documentWithName);
           
            if (idDocumentMap.TryGetValue(syncItemDto.Id, out var document))
            {
                var hasConcurrencyConflict = document.ConcurrencyStamp != syncItemDto.ConcurrencyStamp;
                var hasNameConflict = isNameTaken && documentWithName!.Id != document.ExternalId;

                if ((hasConcurrencyConflict || hasNameConflict)
                    && input.Behavior != SyncBehavior.ClientWins)
                {
                    conflicts.Add(new DocumentDto(
                       document.ExternalId,
                       document.DocumentCategory.ExternalId,
                       document.Name,
                       document.CreatedAt,
                       document.UpdatedAt,
                       document.ConcurrencyStamp,
                       document.IsDeleted));
                    continue;
                }

                document.IsDeleted = syncItemDto.IsDeleted;
                if (document.DocumentCategoryId != syncItemDto.Category.Id)
                {
                    document.DocumentCategory = null!;
                    document.DocumentCategoryId = syncItemDto.Category.Id;
                }

                if (hasNameConflict)
                {
                    finalName = GenerateUniqueName(finalName);
                }
                document.Name = finalName;

                documentRepository.Update(document);
            }
            else
            {
                if (isNameTaken && input.Behavior != SyncBehavior.ClientWins)
                {
                    conflicts.Add(documentWithName!);
                    continue;
                }

                if (isNameTaken)
                {
                    finalName = GenerateUniqueName(finalName);
                }

                var newDocument = new Document(
                   syncItemDto.Id,
                   syncItemDto.Category.Id,
                   finalName)
                {
                    IsDeleted = syncItemDto.IsDeleted
                };

                documentRepository.Add(newDocument);
            }
        }

        await uow.SaveChanges(cancellationToken);

        return [.. conflicts];
    }

    public async Task<IReadOnlyCollection<DocumentVersionDto>> Sync(
        DocumentVersionSyncDto input,
        CancellationToken cancellationToken = default)
    {
        if (input.Items.Count == 0) { return []; }

        var conflicts = new List<DocumentVersionDto>();
        var now = _timeProvider.GetUtcNow();

        var versionIds = input.Items
            .Select(x => x.Id)
            .ToList();
        var idVersionMap = await versionRepository.GetByIds(
            versionIds,
            includes: [],
            ignoreQueryFilters: false,
            cancellationToken);

        var orderedVersions = input.Items
            .OrderBy(x => x.CreatedAt)
            .ToList();

        for (int index = 0; index < orderedVersions.Count; index++)
        {
            var versionDto = orderedVersions[index];

            if (idVersionMap.TryGetValue(versionDto.Id, out var version))
            {
                // Properties cannot be modified. Conflict detection is focused on identifying deviations across the soft-delete status timeline.
                var hasConcurrencyConflict = version.ConcurrencyStamp != versionDto.ConcurrencyStamp;

                if (hasConcurrencyConflict && input.Behavior != SyncBehavior.ClientWins)
                {
                    conflicts.Add(new DocumentVersionDto(
                        version.ExternalId,
                        version.ContentHash,
                        versionDto.Document.ExternalId,
                        version.FileId,
                        version.FileType,
                        version.CreatedAt,
                        version.UpdatedAt,
                        version.ConcurrencyStamp,
                        version.IsDeleted));
                    continue;
                }

                if (version.IsDeleted != versionDto.IsDeleted)
                {
                    version.IsDeleted = versionDto.IsDeleted;

                    // The Interceptor catches this update, increments the timeline, 
                    // and safely rolls the ConcurrencyStamp automatically upon SaveChanges!
                    versionRepository.Update(version);
                }
            }
            else
            {
                // Row generated offline 

                // This forces sequential sorting to hold true
                var sequentialTime = now.AddMilliseconds(index);

                var newVersion = new DocumentVersion(
                    versionDto.Id,
                    versionDto.ContentHash,
                    versionDto.Document.Id,
                    versionDto.FileId,
                    versionDto.FileType)
                {
                    CreatedAt = sequentialTime,
                    UpdatedAt = sequentialTime,
                    IsDeleted = versionDto.IsDeleted
                };

                versionRepository.Add(newVersion);
            }
        }
       
        await uow.SaveChanges(cancellationToken);

        return [.. conflicts];
    }

    private static string GenerateUniqueName(string targetName) =>
        $"{targetName} (Copy_{Guid.NewGuid().ToString()[..4]})";
}
