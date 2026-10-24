namespace Domain;

public record DocumentChunkDto(
    double Distance,
    long DocumentId,
    long DocumentVersionId,
    string Content,
    uint Index);
