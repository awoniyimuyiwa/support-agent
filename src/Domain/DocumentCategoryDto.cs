namespace Domain;

public record DocumentCategoryDto(
    Guid Id,
    string? Description,
    string Name,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? ConcurrencyStamp,
    bool IsDeleted = false);

public record InternalDocumentCategoryDto(
    long InternalId,
    Guid ExternalId,
    string? Description,
    string Name,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? ConcurrencyStamp,
    bool IsDeleted = false) : DocumentCategoryDto(
        Id: ExternalId,
        Description,
        Name,
        CreatedAt,
        UpdatedAt,
        ConcurrencyStamp,
        IsDeleted), IHasInternalCursor, IHasInternalDeltaCursor;

public record DocumentCategoryFilterDto(
    DateTimeOffset? CreatedFrom = null,
    DateTimeOffset? CreatedTo = null,
    DateTimeOffset? UpdatedFrom = null,
    DateTimeOffset? UpdatedTo = null,
    string? SearchTerm = null);
