namespace Domain;

public record DocumentDto(
    Guid Id,
    string Name,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? ConcurrencyStamp,
    bool IsDeleted = false);

public record InternalDocumentDto(
    long InternalId,
    Guid ExternalId,
    string Name,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? ConcurrencyStamp,
    bool IsDeleted = false) : DocumentDto(
        Id: ExternalId,
        Name,
        CreatedAt,
        UpdatedAt,
        ConcurrencyStamp,
        IsDeleted), IHasInternalCursor, IHasInternalDeltaCursor;

public record DocumentFilterDto(
    DateTimeOffset? CreatedFrom = null,
    DateTimeOffset? CreatedTo = null,
    DateTimeOffset? UpdatedFrom = null,
    DateTimeOffset? UpdatedTo = null,
    string? SearchTerm = null);
