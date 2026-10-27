using Api.Contracts;
using Microsoft.EntityFrameworkCore;
using Web.Client.Data;
using Web.Client.Models;

namespace Web.Client.Services;

public interface ISyncService
{
    /// <summary>
    /// Applies resolution choices to server records using the specified decisions and updates the database accordingly.
    /// </summary>
    /// <typeparam name="TEntity">The entity type that implements ISyncableEntity.</typeparam>
    /// <param name="decisions">A dictionary mapping entity identifiers to property resolution choices.</param>
    /// <param name="serverRecords">The collection of server records to apply resolutions to.</param>
    /// <param name="dbSetSelector">A function to select the DbSet for the specified entity type.</param>
    /// <param name="cancellationToken"></param>
    Task ApplyResolutions<TEntity>(
        IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, ResolutionChoice>> decisions, 
        IReadOnlyCollection<TEntity> serverRecords,
        Func<ApplicationDbContext, DbSet<TEntity>> dbSetSelector, 
        CancellationToken cancellationToken = default) 
        where TEntity : class, ISyncableEntity;

    /// <summary>
    /// Fetches document changes from the server
    /// </summary>
    Task FetchDocumentDeltas(CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches document category changes from the server
    /// </summary>
    Task FetchDocumentCategoryDeltas(CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches document version changes from the server
    /// </summary>
    Task FetchDocumentVersionDeltas(CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches ticket changes from the server
    /// </summary>
    Task FetchTicketDeltas(CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes an interrupted sync batch
    /// </summary>
    /// <param name="id"></param>
    /// <param name="cancellationToken"></param>
    Task ResumeBatch(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronizes entities between client and server in batches.
    /// </summary>
    /// <typeparam name="TEntity">The entity type to synchronize, implementing ISyncableEntity.</typeparam>
    /// <param name="dbSetSelector">A function to select the DbSet for the entity type from the database context.</param>
    /// <param name="syncRunner">A function that performs the synchronization for a batch of entities.</param>
    /// <param name="batchSize">The maximum number of entities to process in each batch.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Tuples with client and server entities that have conflicts</returns>
    Task<IReadOnlyCollection<(TEntity Client, TEntity Server)>> Sync<TEntity>(
        Func<ApplicationDbContext, DbSet<TEntity>> dbSetSelector, 
        Func<SyncBatch, IReadOnlyCollection<TEntity>, CancellationToken, Task<IReadOnlyCollection<(TEntity Client, TEntity Server)>>> syncRunner, 
        int batchSize = Constants.MaxLengthOfList,
        CancellationToken cancellationToken = default) where TEntity : class, ISyncableEntity;

    /// <summary>
    /// Syncs documents between client and server.
    /// </summary>
    /// <param name="batch">The synchronization batch containing context for the operation.</param>
    /// <param name="documents">The list of items to be synchronized.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Tuples with client and server entities that have conflicts</returns>
    Task<IReadOnlyCollection<(Document Client, Document Server)>> SyncDocuments(
        SyncBatch batch,
        IReadOnlyCollection<Document> documents,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Syncs document categories between the client and server.
    /// </summary>
    /// <param name="batch">The synchronization batch containing context for the operation.</param>
    /// <param name="documentCategories">The list of items to be synchronized.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Tuples with client and server entities that have conflicts</returns>
    Task<IReadOnlyCollection<(DocumentCategory Client, DocumentCategory Server)>> SyncDocumentCategories(
        SyncBatch batch,
        IReadOnlyCollection<DocumentCategory> documentCategories,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Syncs document versions between client and server.
    /// </summary>
    /// <param name="batch">The synchronization batch containing context for the operation.</param>
    /// <param name="documentVersions">The list of items to be synchronized.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Tuples with client and server entities that have conflicts</returns>
    Task<IReadOnlyCollection<(DocumentVersion Client, DocumentVersion Server)>> SyncDocumentVersions(
        SyncBatch batch,
        IReadOnlyCollection<DocumentVersion> documentVersions,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Syncs tickets between client and server.
    /// </summary>
    /// <param name="batch">The synchronization batch containing context for the operation.</param>
    /// <param name="tickets">The list of tickets to be synchronized.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Tuples with client and server entities that have conflicts</returns>
    Task<IReadOnlyCollection<(Ticket Client, Ticket Server)>> SyncTickets( 
        SyncBatch batch,
        IReadOnlyCollection<Ticket> tickets,
        CancellationToken cancellationToken = default);
}