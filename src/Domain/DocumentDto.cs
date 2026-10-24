namespace Domain;

public record DocumentDto(
    Guid Id,
    Guid CategoryId,
    string Name,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? ConcurrencyStamp,
    bool IsDeleted = false);

public record FullDocumentDto(
    Guid Id,
    Guid CategoryId,
    string CategoryName,
    string Name,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? ConcurrencyStamp,
    bool IsDeleted = false,
    IReadOnlyCollection<DocumentVersionDto>? Versions = null) : DocumentDto(
        Id,
        CategoryId,
        Name,
        CreatedAt,
        UpdatedAt,
        ConcurrencyStamp,
        IsDeleted)
{
    public IReadOnlyCollection<DocumentVersionDto> Versions { get; init; } = Versions ?? [];
}

public record InternalDocumentDto(
    long InternalId,
    Guid ExternalId,
    Guid CategoryId,
    string Name,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? ConcurrencyStamp,
    bool IsDeleted = false) : DocumentDto(
        Id: ExternalId,
        CategoryId,
        Name,
        CreatedAt,
        UpdatedAt,
        ConcurrencyStamp,
        IsDeleted), IHasInternalCursor, IHasInternalDeltaCursor;

public record DocumentFilterDto(
    Guid[]? Categories = null,
    DateTimeOffset? CreatedFrom = null,
    DateTimeOffset? CreatedTo = null,
    DateTimeOffset? UpdatedFrom = null,
    DateTimeOffset? UpdatedTo = null,
    string? SearchTerm = null)
{
    /// <summary>
    /// Specific categories to include. If empty, all categories are processed.
    /// </summary>
    public Guid[] Categories { get; init; } = Categories ?? [];
}
