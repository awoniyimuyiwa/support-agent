namespace Domain;

public record DocumentChunkDto(
    double Distance,
    long DocumentId,
    string Content,
    string DocumentName,
    uint Index);
