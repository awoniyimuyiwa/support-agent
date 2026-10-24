using Api.Contracts;
using Api.Contracts.Models.V1;
using Microsoft.EntityFrameworkCore;
using SqliteWasmHelper;
using System.Collections.Immutable;
using System.Net.Http.Json;
using System.Text.Json;
using Web.Client.Data;
using Web.Client.Models;

namespace Web.Client.Services;

public class SyncService(
    ApiClient apiClient,
    ILogger<SyncService> logger,
    ISqliteWasmDbContextFactory<ApplicationDbContext> dbFactory,
    TimeProvider timeProvider) : ISyncService
{
    /// <summary>
    /// Processes field-level conflict choices using reflection to dynamically merge property attributes.
    /// </summary>
    public async Task ApplyResolutions<TEntity>(
        Dictionary<Guid, Dictionary<string, ResolutionChoice>> decisions,
        ImmutableList<TEntity> serverRecords,
        Func<ApplicationDbContext, DbSet<TEntity>> dbSetSelector,
        CancellationToken cancellationToken = default)
        where TEntity : class, ISyncableEntity
    {
        if (decisions == null || decisions.Count == 0) { return; }

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
                            // Explicit cast or dynamic checking string values ensures perfect validation across nested enums
                            if (userChoice.ToString() == "Server")
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

    /// <summary>
    /// Fetches incremental deltas from the server for tickets based on the client milestone table,
    /// avoiding complete cache clearing.
    /// </summary>
    public Task FetchTicketDeltas(CancellationToken cancellationToken = default)
        => FetchDeltas<
            Ticket,
            TicketModel>(

            apiCall: apiClient.StreamTicketDeltas,
            dbSetSelector: ctx => ctx.Tickets,

            updateAction: (clientRecord, serverRecord) =>
            {
                clientRecord.Title = serverRecord.Title;
                clientRecord.Description = serverRecord.Description;
                clientRecord.CreatedAt = serverRecord.CreatedAt.DateTime;
                clientRecord.UpdatedAt = serverRecord.UpdatedAt.DateTime;
                clientRecord.ConcurrencyStamp = serverRecord.ConcurrencyStamp;
            },

            createFunc: tm => new Ticket
            {
                Id = tm.Id,
                Title = tm.Title,
                Description = tm.Description,
                CreatedAt = tm.CreatedAt.DateTime,
                UpdatedAt = tm.UpdatedAt.DateTime,
                ConcurrencyStamp = tm.ConcurrencyStamp,
                SyncStatus = SyncStatus.Synced
            },
            cancellationToken);

    /// <summary>
    /// Fetches incremental deltas from the server for wether forecast based on the client milestone table,
    /// avoiding complete cache clearing.
    /// </summary>
    public Task FetchWeatherForecastDeltas(CancellationToken cancellationToken = default)
        => FetchDeltas<
            WeatherForecast,
            WeatherForecastModel>(

            apiCall: apiClient.StreamWeatherForecastDeltas,
            dbSetSelector: ctx => ctx.WeatherForecasts,

            updateAction: (clientRecord, serverRecord) =>
            {
                clientRecord.Date = serverRecord.Date;
                clientRecord.Summary = serverRecord.Summary;
                clientRecord.TemperatureC = serverRecord.TemperatureC;
                clientRecord.ConcurrencyStamp = serverRecord.ConcurrencyStamp;
            },

            createFunc: wfm => new WeatherForecast
            {
                Id = wfm.Id,
                Date = wfm.Date,
                Summary = wfm.Summary,
                SyncStatus = SyncStatus.Synced,
                TemperatureC = wfm.TemperatureC,
                ConcurrencyStamp = wfm.ConcurrencyStamp 
            },
            cancellationToken);

    public async Task<ImmutableList<(TEntity Client, TEntity Server)>> SyncPendingChanges<TEntity>(
        Func<ApplicationDbContext, DbSet<TEntity>> dbSetSelector,

         Func<SyncBatch,
             List<TEntity>, 
             CancellationToken,
             Task<ImmutableList<(TEntity Client, TEntity Server)>>> syncRunner,

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

    public async Task ResumeInterruptedBatch(
        Guid idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        using var dbContext = await dbFactory.CreateDbContextAsync();

        var batch = await dbContext.SyncBatches.FindAsync(
            [idempotencyKey],
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
        if (batch.EntityTypeName == typeof(WeatherForecast).Name)
        {
            var existingForecasts = await dbContext.WeatherForecasts
                .Where(x => entityIds.Contains(x.Id))
                .ToListAsync(cancellationToken);

            if (existingForecasts.Count == 0)
            {
                dbContext.SyncBatches.Remove(batch);
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            // Hand off execution directly to your dedicated weather runner
            await SyncPendingWeatherForecastChanges(
                batch, 
                existingForecasts, 
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

            // Hand off execution directly to your dedicated ticket runner
            await SyncPendingTicketChanges(
                batch, 
                existingTickets, 
                cancellationToken);
        }
        else
        {
            // Unrecognized table type boundary; safely clean up to prevent infinite boot lockups
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("[SYNC RECOVERY] Unknown entity type discriminator skipped: {entityTypName}", batch.EntityTypeName);
            }
            dbContext.SyncBatches.Remove(batch);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
    
    public Task<ImmutableList<(
        Ticket Client,
        Ticket Server)>> SyncPendingTicketChanges(

        SyncBatch batch,
        List<Ticket> tickets,
        CancellationToken cancellationToken = default)
        => SyncPendingChanges<

            Ticket,
            TicketModel>(
            batch,
            tickets,

            apiCall: t => apiClient.SyncTickets(
                new TicketSyncModel([.. t.Select(x =>  new TicketModel(
                    x.Id, 
                    x.Description,
                    x.Title, 
                    x.CreatedAt,
                    x.UpdatedAt,
                    x.ConcurrencyStamp
                ))]),
                batch.Id,
                cancellationToken),

            mapToEntity: tm => new Ticket
            {
                Id = tm.Id,
                Title = tm.Title,
                Description = tm.Description,
                CreatedAt = tm.CreatedAt.DateTime,
                UpdatedAt = tm.UpdatedAt.DateTime,
                ConcurrencyStamp = tm.ConcurrencyStamp,
                SyncStatus = SyncStatus.Synced
            },

            dbSetSelector: ctx => ctx.Tickets,
            cancellationToken);

    public Task<ImmutableList<(
        WeatherForecast Client,
        WeatherForecast Server)>> SyncPendingWeatherForecastChanges(

        SyncBatch batch,    
        List<WeatherForecast> weatherForecasts,
        CancellationToken cancellationToken = default)
        => SyncPendingChanges<
            WeatherForecast,
            WeatherForecastModel>(
            batch,
            weatherForecasts,

            apiCall: wf => apiClient.SyncWeatherForecasts(
                new WeatherForecastSyncModel([.. wf.Select(x => new WeatherForecastModel(
                    x.Id,
                    x.Date,
                    x.Summary,
                    x.TemperatureC,
                    x.ConcurrencyStamp
                ))]),
                batch.Id,
                cancellationToken),

            mapToEntity: wfm => new WeatherForecast
            {
                Id = wfm.Id,
                Date = wfm.Date,
                TemperatureC = wfm.TemperatureC,
                Summary = wfm.Summary,
                ConcurrencyStamp = wfm.ConcurrencyStamp,
                SyncStatus = SyncStatus.Synced
            },

            dbSetSelector: ctx => ctx.WeatherForecasts,
            cancellationToken);

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
                .ReadFromJsonAsync<DeltaModel<TSyncableModel>>(cancellationToken);

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

    private async Task<ImmutableList<(TEntity Client, TEntity Server)>> SyncPendingChanges<
        TEntity,
        TSyncableModel>(
    
        SyncBatch batch,
        List<TEntity> entities,
        Func<List<TEntity>, Task<HttpResponseMessage>> apiCall,
        Func<TSyncableModel, TEntity> mapToEntity,
        Func<ApplicationDbContext, DbSet<TEntity>> dbSetSelector,
        CancellationToken cancellationToken)

        where TEntity : class, ISyncableEntity
        where TSyncableModel : class, ISyncableModel
    {
        var response = await apiCall(entities);
        response.EnsureSuccessStatusCode();

        var result = await response.Content
            .ReadFromJsonAsync<SyncResultModel<TSyncableModel>>(cancellationToken);
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
