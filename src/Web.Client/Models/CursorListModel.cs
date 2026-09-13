namespace Web.Client.Models;

/// <summary>
/// 📑 A strongly-typed read-only collection wrapper used for cursor-based pagination streams.
/// </summary>
public record CursorListModel<T>(
    IReadOnlyCollection<T> Items,
    string? Next,
    int PageSize
) where T : class;

