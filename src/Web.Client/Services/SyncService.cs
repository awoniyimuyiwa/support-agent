using Api.Contracts;
using Api.Contracts.Models.V1;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using SqliteWasmHelper;
using System.Collections.Immutable;
using System.Net.Http.Json;
using System.Text.Json;
using Web.Client.Data;
using Web.Client.Extensions;
using Web.Client.Models;

namespace Web.Client.Services;

public class SyncService(
    AuthenticationStateProvider authStateProvider,
    ApiClient apiClient,
    ILogger<SyncService> logger,
    ISqliteWasmDbContextFactory<ApplicationDbContext> dbFactory,
    TimeProvider timeProvider) : ISyncService
{
    public async Task ApplyResolutions<TEntity>(
        IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, ResolutionChoice>> decisions,
        IReadOnlyCollection<TEntity> serverRecords,
        Func<ApplicationDbContext, DbSet<TEntity>> dbSetSelector,
        CancellationToken cancellationToken = default)
        where TEntity : class, ISyncableEntity
    {
        if (decisions is null || decisions.Count == 0) { return; }

        using var context = await dbFactory.CreateDbContextAsync();
        using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var targetIds = decisions.Keys.ToList();
            var clientRecords = await dbSetSelector(context)
                .Where(x => targetIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, cancellationToken);

            var serverMap = serverRecords.ToDictionary(x => x.Id);
            var properties = typeof(TEntity).GetProperties(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            foreach (var recordKv in decisions)
            {
                var recordId = recordKv.Key;
                var fieldChoices = recordKv.Value;

                if (clientRecords.TryGetValue(recordId, out var clientRecord)
                    && serverMap.TryGetValue(recordId, out var serverRecord))
                {
                    var hasClientOverride = false;

                    foreach (var choiceKv in fieldChoices)
                    {
                        var propName = choiceKv.Key;
                        var userChoice = choiceKv.Value;

                        var property = properties.FirstOrDefault(p => p.Name == propName);
                        if (property is not null && property.CanWrite)
                        {
                            if (userChoice == ResolutionChoice.Server)
                            {
                                var serverValue = property.GetValue(serverRecord);
                                property.SetValue(clientRecord, serverValue);
                            }
                            else
                            {
                                hasClientOverride = true;
                            }
                        }
                    }

                    clientRecord.SyncStatus = hasClientOverride ? SyncStatus.PendingUpdate : SyncStatus.Synced;
                    clientRecord.ConcurrencyStamp = serverRecord.ConcurrencyStamp;
                }
            }

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public Task FetchDocumentDeltas(CancellationToken cancellationToken = default)
        => FetchDeltas(

            apiCall: apiClient.StreamDocumentDeltas,
            dbSetSelector: ctx => ctx.Documents,

            updateAction: (clientRecord, serverRecord) =>
            {
                clientRecord.DocumentCategoryId = serverRecord.CategoryId;
                clientRecord.Name = serverRecord.Name;
                clientRecord.CreatedAt = serverRecord.CreatedAt.DateTime;
                clientRecord.UpdatedAt = serverRecord.UpdatedAt.DateTime;
                clientRecord.ConcurrencyStamp = serverRecord.ConcurrencyStamp;
                clientRecord.IsDeleted = serverRecord.IsDeleted;
            },

            createFunc: (DocumentSyncItem dc) => new Document
            {
                Id = dc.Id,
                DocumentCategoryId = dc.CategoryId,
                Name = dc.Name,
                CreatedAt = dc.CreatedAt.DateTime,
                UpdatedAt = dc.UpdatedAt.DateTime,
                ConcurrencyStamp = dc.ConcurrencyStamp,
                IsDeleted = dc.IsDeleted,
                SyncStatus = SyncStatus.Synced
            },
            cancellationToken);

    public Task FetchDocumentCategoryDeltas(CancellationToken cancellationToken = default)
        => FetchDeltas(

            apiCall: apiClient.StreamDocumentCategoryDeltas,
            dbSetSelector: ctx => ctx.DocumentCategories,

            updateAction: (clientRecord, serverRecord) =>
            {
                clientRecord.Description = serverRecord.Description;
                clientRecord.Name = serverRecord.Name;
                clientRecord.CreatedAt = serverRecord.CreatedAt.DateTime;
                clientRecord.UpdatedAt = serverRecord.UpdatedAt.DateTime;
                clientRecord.ConcurrencyStamp = serverRecord.ConcurrencyStamp;
                clientRecord.IsDeleted = serverRecord.IsDeleted;
            },

            createFunc: (DocumentCategorySyncItem dc) => new DocumentCategory
            {
                Id = dc.Id,
                Description = dc.Description,
                Name = dc.Name,
                CreatedAt = dc.CreatedAt.DateTime,
                UpdatedAt = dc.UpdatedAt.DateTime,
                ConcurrencyStamp = dc.ConcurrencyStamp,
                IsDeleted = dc.IsDeleted,
                SyncStatus = SyncStatus.Synced
            },
            cancellationToken);

    public Task FetchDocumentVersionDeltas(CancellationToken cancellationToken = default)
        => FetchDeltas(

            apiCall: apiClient.StreamDocumentVersionDeltas,
            dbSetSelector: ctx => ctx.DocumentVersions,

            updateAction: (clientRecord, serverRecord) =>
            {
                // Only timestamps and soft delete status can be updated
                clientRecord.CreatedAt = serverRecord.CreatedAt.DateTime;
                clientRecord.UpdatedAt = serverRecord.UpdatedAt.DateTime;
                clientRecord.ConcurrencyStamp = serverRecord.ConcurrencyStamp;
                clientRecord.IsDeleted = serverRecord.IsDeleted;
            },

            createFunc: (DocumentVersionSyncItem dv) => new DocumentVersion
            {
                Id = dv.Id,
                DocumentId = dv.DocumentId,
                CreatedAt = dv.CreatedAt.DateTime,
                UpdatedAt = dv.UpdatedAt.DateTime,
                ConcurrencyStamp = dv.ConcurrencyStamp,
                IsDeleted = dv.IsDeleted,
                SyncStatus = SyncStatus.Synced
            },
            cancellationToken);

    public async Task FetchTicketDeltas(CancellationToken cancellationToken = default)
    {
        var isAdmin = await authStateProvider.IsAdmin();
        
        await FetchDeltas(

            apiCall: isAdmin ? apiClient.StreamAdminTicketDeltas : apiClient.StreamTicketDeltas,
            dbSetSelector: ctx => ctx.Tickets,

            updateAction: (clientRecord, serverRecord) =>
            {
                clientRecord.Description = serverRecord.Description;
                clientRecord.Priority = serverRecord.Priority;
                clientRecord.Status = serverRecord.Status;
                clientRecord.Title = serverRecord.Title;
                clientRecord.CreatedAt = serverRecord.CreatedAt.DateTime;
                clientRecord.UpdatedAt = serverRecord.UpdatedAt.DateTime;
                clientRecord.ConcurrencyStamp = serverRecord.ConcurrencyStamp;
                clientRecord.IsDeleted = serverRecord.IsDeleted;
            },

            createFunc: (FullTicketSyncItem tm) => new Ticket
            {
                Id = tm.Id,
                Title = tm.Title,
                Description = tm.Description,
                Priority = tm.Priority,
                Status = tm.Status,
                CreatedAt = tm.CreatedAt.DateTime,
                UpdatedAt = tm.UpdatedAt.DateTime,
                ConcurrencyStamp = tm.ConcurrencyStamp,
                SyncStatus = SyncStatus.Synced
            },
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<(TEntity Client, TEntity Server)>> Sync<TEntity>(
        Func<ApplicationDbContext, DbSet<TEntity>> dbSetSelector,

         Func<SyncBatch,
             IReadOnlyCollection<TEntity>, 
             CancellationToken,
             Task<IReadOnlyCollection<(TEntity Client, TEntity Server)>>> syncRunner,

         int batchSize = Constants.MaxLengthOfList,

         CancellationToken cancellationToken = default) where TEntity : class, ISyncableEntity
    {
        using var context = await dbFactory.CreateDbContextAsync();

        // Safe query against SyncStatus property guaranteed by ISyncable constraint
        var entities = await dbSetSelector(context)
            .Where(x => x.SyncStatus != SyncStatus.Synced)
            .OrderBy(x => x.Id)
            .Take(batchSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (entities.Count == 0) { return []; }

        var batch = new SyncBatch
        {
            Id = Guid.NewGuid(),
            EntityTypeName = typeof(TEntity).Name,
            SerializedEntityIds = JsonSerializer.Serialize(
                entities
                .Select(x => x.Id)
                .ToList()),
            CreatedAt = timeProvider.GetUtcNow().DateTime
        };

        context.SyncBatches.Add(batch);
        await context.SaveChangesAsync(cancellationToken);

        return await syncRunner(
            batch,
            entities,
            cancellationToken);
    }

    public async Task ResumeBatch(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        using var dbContext = await dbFactory.CreateDbContextAsync();

        var batch = await dbContext.SyncBatches.FindAsync(
            [id],
            cancellationToken);

        if (batch is null) return;

        List<Guid> entityIds;
        try
        {
            entityIds = JsonSerializer.Deserialize<List<Guid>>(batch.SerializedEntityIds) ?? [];
        }
        catch (JsonException)
        {
            // Clear corrupted tracking records cleanly to unblock the engine boot checks
            dbContext.SyncBatches.Remove(batch);
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        if (entityIds.Count == 0)
        {
            dbContext.SyncBatches.Remove(batch);
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        // Dynamically route execution based on the tracked EntityTypeName discriminator
        if (batch.EntityTypeName == typeof(Document).Name)
        {
            var existingDocuments = await dbContext.Documents
                .Where(x => entityIds.Contains(x.Id))
                .ToListAsync(cancellationToken);

            if (existingDocuments.Count == 0)
            {
                dbContext.SyncBatches.Remove(batch);
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            await SyncDocuments(
                batch, 
                existingDocuments, 
                cancellationToken);
        }
        else if (batch.EntityTypeName == typeof(DocumentCategory).Name)
        {
            var existingCategories = await dbContext.DocumentCategories
                .Where(x => entityIds.Contains(x.Id))
                .ToListAsync(cancellationToken);

            if (existingCategories.Count == 0)
            {
                dbContext.SyncBatches.Remove(batch);
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            await SyncDocumentCategories(
                batch,
                existingCategories,
                cancellationToken);
        }
        else if (batch.EntityTypeName == typeof(DocumentVersion).Name)
        {
            var existingVersions = await dbContext.DocumentVersions
                .Where(x => entityIds.Contains(x.Id))
                .ToListAsync(cancellationToken);

            if (existingVersions.Count == 0)
            {
                dbContext.SyncBatches.Remove(batch);
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            await SyncDocumentVersions(
                batch,
                existingVersions,
                cancellationToken);
        }
        else if (batch.EntityTypeName == typeof(Ticket).Name)
        {
            var existingTickets = await dbContext.Tickets
                .Where(x => entityIds.Contains(x.Id))
                .ToListAsync(cancellationToken);

            if (existingTickets.Count == 0)
            {
                dbContext.SyncBatches.Remove(batch);
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            await SyncTickets(
                batch, 
                existingTickets,
                cancellationToken);
        }
        else
        {
            // Unrecognized type
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Unknown entity type skipped: {entityTypName}", batch.EntityTypeName);
            }
            dbContext.SyncBatches.Remove(batch);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public Task<IReadOnlyCollection<(
        Document Client,
        Document Server)>> SyncDocuments(
        SyncBatch batch,
        IReadOnlyCollection<Document> documents,
        CancellationToken cancellationToken = default)
        => Sync(
            batch,
            documents,
            apiCall: d => apiClient.SyncDocuments(
               new DocumentSyncRequest([.. d.Select(x =>  new DocumentSyncItem(
                    x.Id,
                    x.DocumentCategoryId,
                    x.Name,
                    x.CreatedAt,
                    x.UpdatedAt,
                    x.ConcurrencyStamp,
                    x.IsDeleted
                ))]),
               batch.Id,
               cancellationToken),

            mapToEntity: (DocumentSyncItem d) => new Document
            {
               Id = d.Id,
               DocumentCategoryId = d.CategoryId,
               Name = d.Name,
               CreatedAt = d.CreatedAt.DateTime,
               UpdatedAt = d.UpdatedAt.DateTime,
               ConcurrencyStamp = d.ConcurrencyStamp,
               IsDeleted = d.IsDeleted,
               SyncStatus = SyncStatus.Synced
           },

           dbSetSelector: ctx => ctx.Documents,
           cancellationToken);

    public Task<IReadOnlyCollection<(
        DocumentCategory Client,
        DocumentCategory Server)>> SyncDocumentCategories(
        SyncBatch batch,
        IReadOnlyCollection<DocumentCategory> documentCategories,
        CancellationToken cancellationToken = default)
        => Sync(
            batch,
            documentCategories,
            apiCall: dc => apiClient.SyncDocumentCategories(
               new DocumentCategorySyncRequest([.. dc.Select(x =>  new DocumentCategorySyncItem(
                    x.Id,
                    x.Description,
                    x.Name,
                    x.CreatedAt,
                    x.UpdatedAt,
                    x.ConcurrencyStamp,
                    x.IsDeleted
                ))]),
               batch.Id,
               cancellationToken),

            mapToEntity: (DocumentCategorySyncItem dc) => new DocumentCategory
            {
                Id = dc.Id,
                Description = dc.Description,
                Name = dc.Name,
                CreatedAt = dc.CreatedAt.DateTime,
                UpdatedAt = dc.UpdatedAt.DateTime,
                ConcurrencyStamp = dc.ConcurrencyStamp,
                IsDeleted = dc.IsDeleted,
                SyncStatus = SyncStatus.Synced
            },

            dbSetSelector: ctx => ctx.DocumentCategories,
            cancellationToken);

    public async Task<IReadOnlyCollection<(
        DocumentVersion Client,
        DocumentVersion Server)>> SyncDocumentVersions(
        SyncBatch batch,
        IReadOnlyCollection<DocumentVersion> documentVersions,
        CancellationToken cancellationToken = default)
    {
        // Todo 
        // Separate creation from syncing. content hash, file id and file type are only required during creation.
        // Call GetUploadUrls when creating to get file id and sas urls, upload files to sas urls, set file ids for entities in the creation request
        // Call endpoint for creating, update database with result
        // Cal endpoint to sync, only IsDeleted and concurrency stamp is requried in the sync request
       return await Sync(
            batch,
            documentVersions,
            apiCall: dc => apiClient.SyncDocumentVersions(
               new DocumentVersionSyncRequest([.. dc.Select(x =>  new DocumentVersionSyncRequestItem(
                    x.Id,
                    x.UpdatedAt,
                    x.ConcurrencyStamp,
                    x.IsDeleted
                ))]),
               batch.Id,
               cancellationToken),

            mapToEntity: (DocumentVersionSyncItem dv) => new DocumentVersion
            {
                Id = dv.Id,
                DocumentId = dv.DocumentId,
                CreatedAt = dv.CreatedAt.DateTime,
                UpdatedAt = dv.UpdatedAt.DateTime,
                ConcurrencyStamp = dv.ConcurrencyStamp,
                IsDeleted = dv.IsDeleted,
                SyncStatus = SyncStatus.Synced
            },

            dbSetSelector: ctx => ctx.DocumentVersions,
            cancellationToken);
    }
         
    public async Task<IReadOnlyCollection<(
        Ticket Client,
        Ticket Server)>> SyncTickets(

        SyncBatch batch,
        IReadOnlyCollection<Ticket> tickets,
        CancellationToken cancellationToken = default)
    {
        var isAdmin = await authStateProvider.IsAdmin();

        return await Sync(
            batch,
            tickets,

            apiCall: t => isAdmin ?
            apiClient.SyncAdminTickets(
                new FullTicketSyncRequest([.. t.Select(x =>  new FullTicketSyncItem(
                    x.Id,
                    x.Description,
                    x.Priority,
                    x.Status,
                    x.Title,
                    x.CreatedAt,
                    x.UpdatedAt,
                    x.ConcurrencyStamp,
                    x.IsDeleted
                ))]),
                batch.Id,
                cancellationToken)
            :
            apiClient.SyncTickets(
                new TicketSyncRequest([.. t.Select(x =>  new TicketSyncItem(
                    x.Id,
                    x.Description,
                    x.Title,
                    x.CreatedAt,
                    x.UpdatedAt,
                    x.ConcurrencyStamp,
                    x.IsDeleted
                ))]),
                batch.Id,
                cancellationToken),

            mapToEntity: (FullTicketSyncItem t) => new Ticket
            {
                Id = t.Id,
                Description = t.Description,
                Priority = t.Priority,
                Status = t.Status,
                Title = t.Title,
                CreatedAt = t.CreatedAt.DateTime,
                UpdatedAt = t.UpdatedAt.DateTime,
                ConcurrencyStamp = t.ConcurrencyStamp,
                IsDeleted = t.IsDeleted,
                SyncStatus = SyncStatus.Synced
            },

            dbSetSelector: ctx => ctx.Tickets,
            cancellationToken);
    }

    private async Task FetchDeltas<TEntity, TSyncableModel>(
        Func<string?, CancellationToken, Task<HttpResponseMessage>> apiCall,
        Func<ApplicationDbContext, DbSet<TEntity>> dbSetSelector,
        Action<TEntity, TSyncableModel> updateAction,
        Func<TSyncableModel, TEntity> createFunc,
        CancellationToken cancellationToken)
        where TEntity : class, ISyncableEntity
        where TSyncableModel : class, ISyncableModel
    {
        var entityTypeName = typeof(TEntity).Name;
        bool hasMore = true;

        while (hasMore)
        {
            string? nextCursorToken = null;

            using (var readContext = await dbFactory.CreateDbContextAsync())
            {
                var syncMetadata = await readContext.SyncMetadatas
                    .FirstOrDefaultAsync(x => x.EntityTypeName == entityTypeName, cancellationToken);

                nextCursorToken = syncMetadata?.Cursor;
            }

            HttpResponseMessage response = await apiCall(nextCursorToken, cancellationToken);
            response.EnsureSuccessStatusCode();
          
            var result = await response.Content
                .ReadFromJsonAsync<DeltaResponse<TSyncableModel>>(cancellationToken);

            if (result is null || result.Items.Count == 0)
            {
                hasMore = false;
                break;
            }

            using var writeContext = await dbFactory.CreateDbContextAsync();
            using var transaction = await writeContext.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var deltaIds = result.Items.Select(d => d.Id).ToList();
                var dbSet = dbSetSelector(writeContext);

                var clientRecords = await dbSet
                    .IgnoreQueryFilters()
                    .Where(x => deltaIds.Contains(x.Id))
                    .ToDictionaryAsync(x => x.Id, cancellationToken);

                foreach (var item in result.Items)
                {
                    if (clientRecords.TryGetValue(item.Id, out var clientRecord))
                    {
                        if (clientRecord.SyncStatus == SyncStatus.PendingUpdate) { continue; } // Protect offline edits

                        if (item.IsDeleted)
                        {
                            dbSet.Remove(clientRecord);
                            continue;
                        }

                        updateAction(clientRecord, item);
                        dbSet.Update(clientRecord);
                    }
                    else
                    {
                        if (item.IsDeleted) { continue; }

                        var newRecord = createFunc(item);
                        dbSet.Add(newRecord);
                    }
                }

                var metadata = await writeContext.SyncMetadatas
                    .FirstOrDefaultAsync(x => x.EntityTypeName == entityTypeName, cancellationToken);

                if (metadata is null)
                {
                    metadata = new SyncMetadata { EntityTypeName = entityTypeName };
                    writeContext.SyncMetadatas.Add(metadata);
                }

                metadata.Cursor = result.Next;
                metadata.LastSyncedAt = result.Until.DateTime; // Server clock drift immunity preserved!

                if (string.IsNullOrWhiteSpace(result.Next))
                {
                    hasMore = false;
                }

                await writeContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
    }

    private async Task<IReadOnlyCollection<(TEntity Client, TEntity Server)>> Sync<
        TEntity,
        TSyncableModel>(
    
        SyncBatch batch,
        IReadOnlyCollection<TEntity> entities,
        Func<IReadOnlyCollection<TEntity>, Task<HttpResponseMessage>> apiCall,
        Func<TSyncableModel, TEntity> mapToEntity,
        Func<ApplicationDbContext, DbSet<TEntity>> dbSetSelector,
        CancellationToken cancellationToken)

        where TEntity : class, ISyncableEntity
        where TSyncableModel : class, ISyncableModel
    {
        var response = await apiCall(entities);
        response.EnsureSuccessStatusCode();

        var result = await response.Content
            .ReadFromJsonAsync<SyncResponse<TSyncableModel>>(cancellationToken);
        if (result is null) { return []; }

        using var writeContext = await dbFactory.CreateDbContextAsync();
        using var transaction = await writeContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var conflictedIds = result
                .Conflicts
                .Select(c => c.Id)
                .ToHashSet();

            var successfulIds = entities
                .Where(e => !conflictedIds.Contains(e.Id))
                .Select(e => e.Id)
                .ToHashSet();

            var recordsToUpdate = await dbSetSelector(writeContext)
                .Where(x => successfulIds.Contains(x.Id))
                .ToListAsync(cancellationToken);

            foreach (var item in recordsToUpdate)
            {
                item.SyncStatus = SyncStatus.Synced;
            }

            var batchToDelete = await writeContext
                .SyncBatches
                .FindAsync([batch.Id], cancellationToken);
            if (batchToDelete is not null)
            {
                writeContext.SyncBatches.Remove(batchToDelete);
            }

            await writeContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            var serverRecords = result
                .Conflicts
                .Select(mapToEntity)
                .ToImmutableList();

            var clientRecords = entities
                .Where(e => conflictedIds.Contains(e.Id))
                .ToImmutableList();

            return [.. clientRecords.Zip(serverRecords, (c, s) => (Client: c, Server: s))];
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
