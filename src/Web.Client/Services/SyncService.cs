using Api.Contracts.Models.V1;
using Domain.Shared;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using SqliteWasmHelper;
using System.Net.Http.Headers;
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
    TimeProvider timeProvider)
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
    }

    public Task<bool> CreateDocumentVersions(
        Guid? batchId = null,
        CancellationToken cancellationToken = default) => Create<DocumentVersion>(
        creationApiCall: (entities, activeBatchId, ct) =>
        {
            var requestItems = entities
                .Select(x => new CreateDocumentVersionRequestItem(
                    x.Id,
                    x.ContentHash,
                    x.DocumentId,
                    x.StoredFile!.ServerFileId!.Value,
                    x.StoredFile.Type,
                    new DateTimeOffset(x.CreatedAt)))
                .ToList();

            return apiClient.CreateDocumentVersions(
                new CreateDocumentVersionRequest(requestItems),
                activeBatchId, // Pass the batch id
                ct);
        },
        batchId,
        cancellationToken);

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

            apiCall: isAdmin ? apiClient.StreamAllTicketDeltas : apiClient.StreamTicketDeltas,
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

            createFunc: (TicketSyncResponseItem tm) => new Ticket
            {
                Id = tm.Id,
                CreatedByUserId = tm.CreatedByUserId,
                Description = tm.Description,
                Priority = tm.Priority,
                Status = tm.Status,
                Title = tm.Title,
                CreatedAt = tm.CreatedAt.DateTime,
                UpdatedAt = tm.UpdatedAt.DateTime,
                ConcurrencyStamp = tm.ConcurrencyStamp,
                SyncStatus = SyncStatus.Synced
            },
            cancellationToken);
    }

    public async Task ResumeBatch(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        List<Guid> entityIds;
        SyncBatch? batch = null;
        bool hasEntities = false;

        // Open short-lived read context to safely inspect tracking constraints
        using (var dbContext = await dbFactory.CreateDbContextAsync())
        {
            batch = await dbContext.SyncBatches.FindAsync([id], cancellationToken);
            if (batch is null) { return; }

            try
            {
                entityIds = JsonSerializer.Deserialize<List<Guid>>(batch.SerializedEntityIds) ?? [];
            }
            catch (JsonException)
            {
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

            // Check if matching records still reside locally before routing
            if (batch.EntityTypeName == typeof(Document).Name)
                hasEntities = await dbContext.Documents.AnyAsync(x => entityIds.Contains(x.Id), cancellationToken);
            else if (batch.EntityTypeName == typeof(DocumentCategory).Name)
                hasEntities = await dbContext.DocumentCategories.AnyAsync(x => entityIds.Contains(x.Id), cancellationToken);
            else if (batch.EntityTypeName == typeof(DocumentVersion).Name)
                hasEntities = await dbContext.DocumentVersions.AnyAsync(x => entityIds.Contains(x.Id), cancellationToken);
            else if (batch.EntityTypeName == typeof(Ticket).Name)
                hasEntities = await dbContext.Tickets.AnyAsync(x => entityIds.Contains(x.Id), cancellationToken);

            if (!hasEntities)
            {
                dbContext.SyncBatches.Remove(batch);
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }
        } // Read context is fully closed and disposed here

        if (batch.Type == SyncType.UploadFiles)
        {
            if (batch.EntityTypeName == typeof(DocumentVersion).Name)
            {
                await UploadDocumentVersionFiles(
                    batch.Id,
                    cancellationToken);
            }
        }
        else if (batch.Type == SyncType.Create)
        {
            if (batch.EntityTypeName == typeof(DocumentVersion).Name)
            {
                await CreateDocumentVersions(
                    batch.Id,
                    cancellationToken);
            }
        }
        else if (batch.Type == SyncType.Sync)
        {
            using var syncContext = await dbFactory.CreateDbContextAsync();

            if (batch.EntityTypeName == typeof(Document).Name)
            {
                var existingDocuments = await syncContext.Documents
                    .Where(x => entityIds.Contains(x.Id))
                    .ToListAsync(cancellationToken);
                await SyncDocuments(
                    batch.Id,
                    existingDocuments, 
                    cancellationToken);
            }
            else if (batch.EntityTypeName == typeof(DocumentCategory).Name)
            {
                var existingCategories = await syncContext.DocumentCategories
                    .Where(x => entityIds.Contains(x.Id))
                    .ToListAsync(cancellationToken);
                await SyncDocumentCategories(
                    batch.Id, 
                    existingCategories, 
                    cancellationToken);
            }
            else if (batch.EntityTypeName == typeof(DocumentVersion).Name)
            {
                var existingVersions = await syncContext.DocumentVersions
                    .Where(x => entityIds.Contains(x.Id))
                    .ToListAsync(cancellationToken);
                await SyncDocumentVersions(
                    batch.Id, 
                    existingVersions, 
                    cancellationToken);
            }
            else if (batch.EntityTypeName == typeof(Ticket).Name)
            {
                var existingTickets = await syncContext.Tickets
                    .Where(x => entityIds.Contains(x.Id))
                    .ToListAsync(cancellationToken);
                await SyncTickets(
                    batch.Id, 
                    existingTickets, 
                    cancellationToken);
            }
        }
        else
        {
            using var fallbackContext = await dbFactory.CreateDbContextAsync();
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Unknown batch type: {entityTypeName}", batch.EntityTypeName);
            }
            fallbackContext.SyncBatches.Remove(new SyncBatch { Id = batch.Id });
            await fallbackContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyCollection<(TEntity Client, TEntity Server)>> Sync<TEntity>(
        Func<ApplicationDbContext, DbSet<TEntity>> dbSetSelector,

        Func<Guid,
           IReadOnlyCollection<TEntity>,
           CancellationToken,
           Task<IReadOnlyCollection<(TEntity Client, TEntity Server)>>> syncRunner,

        CancellationToken cancellationToken = default) where TEntity : class, ISyncableEntity
    {
        var batchId = Guid.NewGuid();

        var entities = await CreateSyncBatch(
            dbSetSelector,
            batchId,
            SyncType.Sync,
            cancellationToken);
        if (entities.Count == 0) { return []; }

        return await syncRunner(
            batchId,
            entities,
            cancellationToken);
    }

    public Task<IReadOnlyCollection<(
        Document Client,
        Document Server)>> SyncDocuments(
        Guid batchId,
        IReadOnlyCollection<Document> documents,
        CancellationToken cancellationToken = default)
        => Sync(
            batchId,
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
               batchId,
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
        Guid batchId,
        IReadOnlyCollection<DocumentCategory> documentCategories,
        CancellationToken cancellationToken = default)
        => Sync(
            batchId,
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
               batchId,
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
        Guid batchId,
        IReadOnlyCollection<DocumentVersion> documentVersions,
        CancellationToken cancellationToken = default)
    { 
        return await Sync(
            batchId,
            documentVersions,
            apiCall: dc => apiClient.SyncDocumentVersions(
               new DocumentVersionSyncRequest([.. dc.Select(x =>  new DocumentVersionSyncRequestItem(
                    x.Id,
                    x.UpdatedAt,
                    x.ConcurrencyStamp,
                    x.IsDeleted
                ))]),
               batchId,
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

    public async Task<IReadOnlyCollection<(Ticket Client, Ticket Server)>> SyncTickets(
        Guid batchId,
        IReadOnlyCollection<Ticket> tickets,
        CancellationToken cancellationToken = default)
    {
        var isAdmin = await authStateProvider.IsAdmin();

        var conflicts = await Sync(
            batchId,
            tickets,

            apiCall: t => isAdmin ?
            apiClient.SyncAllTickets(
                new FullTicketSyncRequest([.. t.Select(x => new FullTicketSyncRequestItem(
                    x.Id, 
                    x.Description, 
                    x.Priority, 
                    x.Status, 
                    x.Title, 
                    x.CreatedAt, 
                    x.UpdatedAt, 
                    x.ConcurrencyStamp, 
                    x.IsDeleted))]),
                batchId,
                cancellationToken)
            :
            apiClient.SyncTickets(
                new TicketSyncRequest([.. t.Select(x => new TicketSyncRequestItem(
                    x.Id,
                    x.Description, 
                    x.Title,
                    x.CreatedAt, 
                    x.UpdatedAt, 
                    x.ConcurrencyStamp, 
                    x.IsDeleted))]),
                batchId,
                cancellationToken),

            mapToEntity: (TicketSyncResponseItem t) => new Ticket
            {
                Id = t.Id,
                CreatedByUserId = t.CreatedByUserId,
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

        // For Non-Admins, process and strip out specific server-side conflicts
        if (!isAdmin && conflicts.Count > 0)
        {
            var idsToDelete = conflicts
                .Where(t => t.Server.IsDeleted && t.Server.ConcurrencyStamp == t.Client.ConcurrencyStamp)
                .Select(t => t.Server.Id)
                .ToList();

            var idsToUpdate = conflicts
                .Where(t => t.Server.Status != TicketStatus.Open)
                .Select(t => t.Server.Id)
                .ToList();

            using var context = await dbFactory.CreateDbContextAsync();
            using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                if (idsToDelete.Count > 0)
                {
                    await context.Tickets
                        .Where(t => idsToDelete.Contains(t.Id))
                        .ExecuteDeleteAsync(cancellationToken);
                }

                // Fetch and batch-update records inside the same transaction
                if (idsToUpdate.Count > 0)
                {
                    var clientMap = await context.Tickets
                        .Where(t => idsToUpdate.Contains(t.Id))
                        .ToDictionaryAsync(t => t.Id, cancellationToken);

                    foreach (var conflict in conflicts.Where(c => idsToUpdate.Contains(c.Server.Id)))
                    {
                        var serverTicket = conflict.Server;

                        if (clientMap.TryGetValue(serverTicket.Id, out var client))
                        {
                            client.Description = serverTicket.Description;
                            client.Priority = serverTicket.Priority;
                            client.Status = serverTicket.Status;
                            client.Title = serverTicket.Title;
                            client.CreatedAt = serverTicket.CreatedAt;
                            client.UpdatedAt = serverTicket.UpdatedAt;
                            client.ConcurrencyStamp = serverTicket.ConcurrencyStamp;
                            client.IsDeleted = serverTicket.IsDeleted;
                            client.SyncStatus = SyncStatus.Synced;

                            context.Tickets.Update(client);
                        }
                    }

                    await context.SaveChangesAsync(cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }

            // Build and return the remaining conflicts
            var filteredConflicts = conflicts
                .Where(t => !idsToDelete.Contains(t.Server.Id) && !idsToUpdate.Contains(t.Server.Id))
                .ToList();

            return filteredConflicts.AsReadOnly();
        }

        return conflicts;
    }

    public Task<bool> UploadDocumentVersionFiles(
        Guid? batchId = null,
        CancellationToken cancellationToken = default) => UploadFiles(
            apiClient.CreateDocumentVersionUploadUrls,
            dbSetSelector: ctx => ctx.DocumentVersions,
            batchId,
            cancellationToken);

    private async Task<bool> Create<TEntity>(
        Func<List<TEntity>,
            Guid,
            CancellationToken,
            Task<HttpResponseMessage>> creationApiCall,

        Guid? batchId,

        CancellationToken cancellationToken) where TEntity : class, IHasFile
    {
        var entityTypeName = typeof(TEntity).Name;
        var activeBatchId = Guid.NewGuid();
        List<TEntity> pendingItems = [];

        if (batchId.HasValue && batchId.Value != Guid.Empty)
        {
            using var checkContext = await dbFactory.CreateDbContextAsync();

            var existingBatch = await checkContext.SyncBatches
                .FirstOrDefaultAsync(x => x.Id == batchId.Value
                                          && x.EntityTypeName == entityTypeName
                                          && x.Type == SyncType.Create,
                                     cancellationToken);

            if (existingBatch is null) { return false; }

            activeBatchId = batchId.Value;
            var trackedIds = JsonSerializer.Deserialize<List<Guid>>(existingBatch.SerializedEntityIds) ?? [];

            if (trackedIds.Count > 0)
            {
                pendingItems = await checkContext.Set<TEntity>()
                    .Include(x => x.StoredFile)
                    .Where(x => trackedIds.Contains(x.Id))
                    .AsNoTracking()
                    .ToListAsync(cancellationToken);
            }
        }
        else
        {
            activeBatchId = Guid.NewGuid();
            pendingItems = await CreateSyncBatch(
                ctx => ctx.Set<TEntity>()
                .Include(x => x.StoredFile)
                .Where(x => x.SyncStatus == SyncStatus.PendingInsert
                            && x.StoredFile != null
                            && x.StoredFile.ServerFileId != null
                            && string.IsNullOrWhiteSpace(x.SerializedErrors))
                .OrderBy(x => x.Id),
                activeBatchId,
                SyncType.Create,
                cancellationToken);
        }

        if (pendingItems.Count == 0) { return false; }

        using var response = await creationApiCall(
            pendingItems,
            activeBatchId,
            cancellationToken);

        using var context = await dbFactory.CreateDbContextAsync();
        using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                var problemDetails = await response.Content.ReadFromJsonAsync<ApiValidationProblemDetails>(cancellationToken);
                int totalMappedItemErrors = 0;

                if (problemDetails?.Errors is not null)
                {
                    for (int i = 0; i < pendingItems.Count; i++)
                    {
                        var prefix = $"{nameof(CreateDocumentVersionRequest.Items)}[{i}].";
                        var indexErrors = problemDetails.Errors
                            .Where(kvp => kvp.Key.StartsWith(prefix))
                            .ToDictionary(
                                kvp => kvp.Key.Replace(prefix, ""),
                                kvp => kvp.Value
                            );

                        if (indexErrors.Count > 0)
                        {
                            totalMappedItemErrors += indexErrors.Count;
                            var targetEntity = context.Set<TEntity>().Attach(pendingItems[i]).Entity;
                            targetEntity.SerializedErrors = JsonSerializer.Serialize(indexErrors);
                        }
                    }
                }

                if (totalMappedItemErrors == 0)
                {
                    response.EnsureSuccessStatusCode();
                }
            }
            else
            {
                response.EnsureSuccessStatusCode();

                var itemIds = pendingItems.Select(x => x.Id).ToList();
                var fileIds = pendingItems.Select(x => x.StoredFileId).ToList();

                await context.Set<TEntity>()
                    .Where(x => itemIds.Contains(x.Id))
                    .ExecuteUpdateAsync(s => s.SetProperty(b => b.SyncStatus, SyncStatus.Synced), cancellationToken);

                await context.StoredFiles
                    .Where(x => fileIds.Contains(x.Id))
                    .ExecuteDeleteAsync(cancellationToken);
            }

            await context.SyncBatches
                .Where(b => b.Id == activeBatchId)
                .ExecuteDeleteAsync(cancellationToken);

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return pendingItems.Count >= Api.Contracts.Constants.MaxLengthOfList;
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

            using var response = await apiCall(nextCursorToken, cancellationToken);
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
        Guid batchId,
        IReadOnlyCollection<TEntity> entities,
        Func<IReadOnlyCollection<TEntity>, Task<HttpResponseMessage>> apiCall,
        Func<TSyncableModel, TEntity> mapToEntity,
        Func<ApplicationDbContext, DbSet<TEntity>> dbSetSelector,
        CancellationToken cancellationToken)

        where TEntity : class, ISyncableEntity
        where TSyncableModel : class, ISyncableModel
    {
        using var response = await apiCall(entities);
        response.EnsureSuccessStatusCode();

        var result = await response.Content
            .ReadFromJsonAsync<SyncResponse<TSyncableModel>>(cancellationToken);
        if (result is null) { return []; }

        var conflictedIds = result
            .Conflicts
            .Select(c => c.Id)
            .ToHashSet();

        var successfulIds = entities
            .Where(e => !conflictedIds.Contains(e.Id))
            .Select(e => e.Id)
            .ToList();

        using var context = await dbFactory.CreateDbContextAsync();
        // Open an explicit transaction to ensure atomic updates and deletions
        using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        
        try
        {
            if (successfulIds.Count > 0)
            {
                await dbSetSelector(context)
                    .Where(x => successfulIds.Contains(x.Id))
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.SyncStatus, SyncStatus.Synced), cancellationToken);
            }

            await context.SyncBatches
                .Where(b => b.Id == batchId)
                .ExecuteDeleteAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        var clientMap = entities
            .Where(e => conflictedIds.Contains(e.Id))
            .ToDictionary(e => e.Id);

        return [.. result.Conflicts
            .Select(conflict =>
            {
                var server = mapToEntity(conflict);
                // Safely extract the exact matching client side counterpart
                return (Client: clientMap[server.Id], Server: server);
            })];
    }

    private async Task<bool> UploadFiles<TEntity>(
       Func<CreateUploadUrlRequest,
           Guid,
           CancellationToken,
           Task<HttpResponseMessage>> apiCall,

       Func<ApplicationDbContext,
           DbSet<TEntity>> dbSetSelector,

       Guid? batchId,

       CancellationToken cancellationToken) where TEntity : class, IHasFile
    {
        var activeBatchId = Guid.NewGuid();
        var entityTypeName = typeof(TEntity).Name;
        List<TEntity> pendingItems = [];

        if (batchId.HasValue && batchId.Value != Guid.Empty)
        {
            using var checkContext = await dbFactory.CreateDbContextAsync();

            var existingBatch = await checkContext.SyncBatches
                .FirstOrDefaultAsync(x => x.Id == batchId.Value
                                          && x.EntityTypeName == entityTypeName
                                          && x.Type == SyncType.UploadFiles,
                                          cancellationToken);
            
            if (existingBatch is null) { return false; }

            activeBatchId = batchId.Value;
            var trackedIds = JsonSerializer.Deserialize<List<Guid>>(existingBatch.SerializedEntityIds) ?? [];

            if (trackedIds.Count > 0)
            {
                pendingItems = await dbSetSelector(checkContext)
                    .Where(e => trackedIds.Contains(e.Id))
                    .AsNoTracking()
                    .ToListAsync(cancellationToken);
            }
        }
        else
        {
            pendingItems = await CreateSyncBatch(
                ctx => dbSetSelector(ctx)
                .Where(e => e.SyncStatus == SyncStatus.PendingInsert
                            && e.StoredFile != null
                            && e.StoredFile.ServerFileId == null)
                .OrderBy(x => x.Id),
                activeBatchId,
                SyncType.UploadFiles,
                cancellationToken);
        }

        if (pendingItems.Count == 0) { return false; }

        var requestItems = pendingItems
            .Select(e => new CreateUploadUrlRequestItem(e.StoredFile!.Type, e.Id))
            .ToList();
        // If process crashes mid flight and a sync batch is resumed
        // the server will send the same storage urls and file ids it sent previously
        // the server's cache duration will be approximately that of the TTL of the urls
        using var response = await apiCall(
            new CreateUploadUrlRequest(requestItems),
            activeBatchId,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        var apiResult = await response.Content.ReadFromJsonAsync<UploadUrlsResponse>(cancellationToken);

        var updatesMap = new Dictionary<Guid, Guid>();
        var uploadTasks = new List<Task>();

        foreach (var entity in pendingItems)
        {
            if (entity.StoredFile != null && apiResult!.Map.TryGetValue(entity.Id, out var responseItem))
            {
                updatesMap[entity.StoredFile.Id] = responseItem.FileId;

                uploadTasks.Add(Task.Run(async () =>
                {
                    using var blobRequest = new HttpRequestMessage(HttpMethod.Put, responseItem.Url);
                    blobRequest.Content = new ByteArrayContent(entity.StoredFile.Bytes);
                    blobRequest.Content.Headers.ContentType = new MediaTypeHeaderValue(entity.StoredFile.Type);
                    blobRequest.Headers.Add("x-ms-blob-type", "BlockBlob");

                    using var blobResponse = await apiClient.HttpClient.SendAsync(
                        blobRequest,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken);

                    blobResponse.EnsureSuccessStatusCode();
                }, cancellationToken));
            }
        }
        await Task.WhenAll(uploadTasks);

        using var context = await dbFactory.CreateDbContextAsync();
        using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            if (updatesMap.Count > 0)
            {
                var filesToUpdate = await context.StoredFiles
                    .Where(f => updatesMap.Keys.Contains(f.Id))
                    .ToListAsync(cancellationToken);

                foreach (var file in filesToUpdate)
                {
                    if (updatesMap.TryGetValue(file.Id, out var ServerId))
                    {
                        file.ServerFileId = ServerId;
                    }
                }
            }

            await context.SyncBatches
                .Where(b => b.Id == activeBatchId)
                .ExecuteDeleteAsync(cancellationToken);

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return pendingItems.Count >= Api.Contracts.Constants.MaxLengthOfList;
    }

    private async Task<List<TEntity>> CreateSyncBatch<TEntity>(
        Func<ApplicationDbContext, IQueryable<TEntity>> querySelector,
        Guid batchId,
        SyncType type,
        CancellationToken cancellationToken) where TEntity : class, ISyncableEntity
    {
        using var context = await dbFactory.CreateDbContextAsync();

        var entities = await querySelector(context)
            .Take(Api.Contracts.Constants.MaxLengthOfList)
            .ToListAsync(cancellationToken);

        if (entities.Count == 0) { return entities; }

        var batch = new SyncBatch
        {
            Id = batchId,
            EntityTypeName = typeof(TEntity).Name,
            SerializedEntityIds = JsonSerializer.Serialize(entities.Select(x => x.Id).ToList()),
            Type = type,
            CreatedAt = timeProvider.GetUtcNow().DateTime
        };

        context.SyncBatches.Add(batch);
        await context.SaveChangesAsync(cancellationToken);

        return entities;
    }
}
