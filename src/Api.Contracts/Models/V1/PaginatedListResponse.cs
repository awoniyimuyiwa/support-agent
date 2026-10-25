namespace Api.Contracts.Models.V1;

public record PaginatedListResponse<T>(
    IReadOnlyCollection<T> Items,
    int TotalCount,
    int PageNumber,
    int TotalPages,
    bool HasPreviousPage,
    bool HasNextPage) where T : class;
