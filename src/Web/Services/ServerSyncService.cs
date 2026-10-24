using Microsoft.EntityFrameworkCore;
using System.Collections.Immutable;
using Web.Client.Data;
using Web.Client.Models;
using Web.Client.Services;

namespace Web.Services;

/// <summary>
/// Provides a passive implementation of <see cref="ISyncService"/> for the server-side DI container.
/// This prevents dependency injection failures during Blazor component discovery and pre-rendering 
/// lifecycles without requiring client-side database or browser dependencies on the server.
/// </summary>
public class ServerSyncService : ISyncService
{
    public Task ApplyResolutions<TEntity>(
        Dictionary<Guid, Dictionary<string, ResolutionChoice>> decisions,
        ImmutableList<TEntity> serverRecords, 
        Func<ApplicationDbContext, DbSet<TEntity>> dbSetSelector, 
        CancellationToken cancellationToken = default)
        where TEntity : class, ISyncableEntity
        => Task.CompletedTask;

    public Task FetchTicketDeltas(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task FetchWeatherForecastDeltas(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task ResumeInterruptedBatch(Guid idempotencyKey, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<ImmutableList<(Ticket Client, Ticket Server)>> SyncPendingTicketChanges(
        SyncBatch batch, List<Ticket> tickets, CancellationToken cancellationToken = default)
        => Task.FromResult(ImmutableList<(Ticket, Ticket)>.Empty);

    public Task<ImmutableList<(WeatherForecast Client, WeatherForecast Server)>> SyncPendingWeatherForecastChanges(
        SyncBatch batch, List<WeatherForecast> weatherForecasts, CancellationToken cancellationToken = default)
        => Task.FromResult(ImmutableList<(WeatherForecast, WeatherForecast)>.Empty);

    public Task<ImmutableList<(TEntity Client, TEntity Server)>> SyncPendingChanges<TEntity>(
        Func<ApplicationDbContext, DbSet<TEntity>> dbSetSelector,
        Func<SyncBatch, List<TEntity>, CancellationToken, Task<ImmutableList<(TEntity Client, TEntity Server)>>> syncRunner,
        int batchSize,
        CancellationToken cancellationToken)
        where TEntity : class, ISyncableEntity
        => Task.FromResult(ImmutableList<(TEntity, TEntity)>.Empty);
}
