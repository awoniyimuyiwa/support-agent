using Domain;
using Domain.Shared;

namespace Application.Contracts;

public interface IDocumentCategoryService
{
    /// <summary>
    /// Synchronizes document categories between client and server.
    /// </summary>
    /// <param name="input"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>Documents with conflicts</returns>
    Task<IReadOnlyCollection<DocumentCategoryDto>> Sync(
        DocumentCategorySyncDto input,
        CancellationToken cancellationToken = default);
}

public record DocumentCategorySyncDto(
    IReadOnlyCollection<DocumentCategoryDto> Items,
    SyncBehavior Behavior = SyncBehavior.Default)
{
    public IReadOnlyCollection<DocumentCategoryDto> Items { get; init; } = Items ?? [];
}