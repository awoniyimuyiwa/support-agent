namespace Domain;

public record DocumentVersionDto(
    Guid Id,
    Guid FileId,
    string FileType,
    DateTimeOffset CreatedAt,
    bool IsDeleted = false);