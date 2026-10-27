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
        IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, ResolutionChoice>> decisions,
        IReadOnlyCollection<TEntity> serverRecords, 
        Func<ApplicationDbContext, DbSet<TEntity>> dbSetSelector, 
        CancellationToken cancellationToken = default)
        where TEntity : class, ISyncableEntity
        => Task.CompletedTask;

    public Task FetchDocumentDeltas(
       CancellationToken cancellationToken = default) 
        => Task.CompletedTask;

    public Task FetchDocumentCategoryDeltas(CancellationToken cancellationToken = default) 
        => Task.CompletedTask;

    public Task FetchDocumentVersionDeltas(CancellationToken cancellationToken = default) 
        => Task.CompletedTask;

    public Task FetchTicketDeltas(
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task ResumeBatch(
        Guid id,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<IReadOnlyCollection<(TEntity Client, TEntity Server)>> Sync<TEntity>(
        Func<ApplicationDbContext, DbSet<TEntity>> dbSetSelector,
        Func<SyncBatch, IReadOnlyCollection<TEntity>, CancellationToken, Task<IReadOnlyCollection<(TEntity Client, TEntity Server)>>> syncRunner,
        int batchSize,
        CancellationToken cancellationToken)
        where TEntity : class, ISyncableEntity
        => Task.FromResult<IReadOnlyCollection<(TEntity, TEntity)>>([]);

    public Task<IReadOnlyCollection<(Document Client, Document Server)>> SyncDocuments(
       SyncBatch batch,
       IReadOnlyCollection<Document> documents,
       CancellationToken cancellationToken = default)
       => Task.FromResult<IReadOnlyCollection<(Document, Document)>>([]);

    public Task<IReadOnlyCollection<(DocumentCategory Client, DocumentCategory Server)>> SyncDocumentCategories(
        SyncBatch batch,
        IReadOnlyCollection<DocumentCategory> documentCategories,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<(DocumentCategory, DocumentCategory)>>([]);

    public Task<IReadOnlyCollection<(DocumentVersion Client, DocumentVersion Server)>> SyncDocumentVersions(
        SyncBatch batch,
        IReadOnlyCollection<DocumentVersion> documentVersions,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<(DocumentVersion, DocumentVersion)>>([]);

    public Task<IReadOnlyCollection<(Ticket Client, Ticket Server)>> SyncTickets(
        SyncBatch batch, 
        IReadOnlyCollection<Ticket> tickets,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<(Ticket Client, Ticket Server)>>([]);
}
