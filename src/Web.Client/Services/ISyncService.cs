using Api.Contracts;
using Microsoft.EntityFrameworkCore;
using System.Collections.Immutable;
using Web.Client.Components;
using Web.Client.Data;
using Web.Client.Models;

namespace Web.Client.Services;

public interface ISyncService
{
    Task ApplyResolutions<TEntity>(
        Dictionary<Guid, Dictionary<string, ResolutionChoice>> decisions, 
        ImmutableList<TEntity> serverRecords,
        Func<ApplicationDbContext, DbSet<TEntity>> dbSetSelector, 
        CancellationToken cancellationToken = default) 
        where TEntity : class, ISyncableEntity;

    Task FetchTicketDeltas(CancellationToken cancellationToken = default);

    Task FetchWeatherForecastDeltas(CancellationToken cancellationToken = default);

    /// <summary>
    /// Safely resumes an interrupted synchronization pipeline by dynamically routing 
    /// the serialized payload based on the tracked entity metadata.
    /// </summary>
    /// <param name="idempotencyKey"></param>
    /// <param name="cancellationToken"></param>
    Task ResumeInterruptedBatch(
        Guid idempotencyKey, 
        CancellationToken cancellationToken = default);

    Task<ImmutableList<(TEntity Client, TEntity Server)>> SyncPendingChanges<TEntity>(
        Func<ApplicationDbContext, DbSet<TEntity>> dbSetSelector, 
        Func<SyncBatch, List<TEntity>, CancellationToken, Task<ImmutableList<(TEntity Client, TEntity Server)>>> syncRunner, 
        int batchSize = Constants.MaxLengthOfList, 
        CancellationToken cancellationToken = default) where TEntity : class, ISyncableEntity;

    Task<ImmutableList<(Ticket Client, Ticket Server)>> SyncPendingTicketChanges(
        SyncBatch batch, 
        List<Ticket> tickets,
        CancellationToken cancellationToken = default);

    Task<ImmutableList<(WeatherForecast Client, WeatherForecast Server)>> SyncPendingWeatherForecastChanges(
        SyncBatch batch, 
        List<WeatherForecast> weatherForecasts,
        CancellationToken cancellationToken = default);
}