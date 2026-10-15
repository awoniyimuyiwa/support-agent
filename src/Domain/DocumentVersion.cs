
namespace Domain;

public class DocumentVersion : CreationAuditedEntity
{
    protected List<DocumentChunk> DocumentChunks { get; set; } = [];

    public string ContentHash { get; set; } = string.Empty;

    public long DocumentId { get; protected set; }

    public Document Document { get; protected set; } = null!;

    public Guid FileId { get; set; }

    public string FileType { get; set; } = string.Empty;

    public IReadOnlyCollection<DocumentChunk> Chunks => DocumentChunks;

    public DocumentVersion() { }

    public DocumentVersion(
        Guid externalId,
        string contentHash,
        Document document,
        Guid fileId,
        string fileType)
    {
        ContentHash = contentHash;
        Document = document;
        ExternalId = externalId;
        FileId = fileId;
        FileType = fileType;
    }

    public void AddChunk(
        string content,
        uint index,
        float[] vector)
    {
        DocumentChunks.Add(new DocumentChunk(
            content,
            this, 
            index, 
            vector));
    }
}