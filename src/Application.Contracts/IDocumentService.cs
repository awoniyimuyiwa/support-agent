using Domain;
using Domain.Shared;

namespace Application.Contracts;

public interface IDocumentService
{
    /// <summary>
    /// Synchronizes documents between client and server.
    /// </summary>
    /// <param name="input"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>Documents with conflicts</returns>
    Task<IReadOnlyCollection<DocumentDto>> Sync(
        DocumentSyncDto input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronizes document versions between client and server.
    /// </summary>
    /// <param name="input"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>Document versions with conflicts</returns>
    Task<IReadOnlyCollection<DocumentVersionDto>> Sync(
        DocumentVersionSyncDto input,
        CancellationToken cancellationToken = default);
}

public record DocumentSyncItemDto(
    Guid Id,
    EntityDto Category,
    string Name,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? ConcurrencyStamp,
    bool IsDeleted = false);

public record DocumentSyncDto(
    IReadOnlyCollection<DocumentSyncItemDto> Items,
    SyncBehavior Behavior = SyncBehavior.Default)
{
    public IReadOnlyCollection<DocumentSyncItemDto> Items { get; init; } = Items ?? [];
}

public record DocumentVersionSyncItemDto(
    Guid Id,
    string ContentHash,
    EntityDto Document,
    Guid FileId,
    string FileType,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? ConcurrencyStamp,
    bool IsDeleted = false);

public record DocumentVersionSyncDto(
    IReadOnlyCollection<DocumentVersionSyncItemDto> Items,
    SyncBehavior Behavior = SyncBehavior.Default)
{
    public IReadOnlyCollection<DocumentVersionSyncItemDto> Items { get; init; } = Items ?? [];
}
