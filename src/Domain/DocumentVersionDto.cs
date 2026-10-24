namespace Domain;

public record DocumentVersionDto(
    Guid Id,
    string ContentHash,
    Guid DocumentId,
    Guid FileId,
    string FileType,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? ConcurrencyStamp,
    bool IsDeleted = false);

public record InternalDocumentVersionDto(
    long InternalId,
    Guid ExternalId,
    string ContentHash,
    Guid DocumentId,
    Guid FileId,
    string FileType,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? ConcurrencyStamp,
    bool IsDeleted = false) : DocumentVersionDto(
        ExternalId,
        ContentHash,
        DocumentId,
        FileId,
        FileType,
        CreatedAt,
        UpdatedAt,
        ConcurrencyStamp,
        IsDeleted), IHasInternalCursor, IHasInternalDeltaCursor;

public record DocumentVersionFilterDto(
    Guid DocumentId,
    DateTimeOffset? CreatedFrom = null,
    DateTimeOffset? CreatedTo = null,
    DateTimeOffset? UpdatedFrom = null,
    DateTimeOffset? UpdatedTo = null);