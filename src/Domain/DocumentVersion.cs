
namespace Domain;

/// <summary>
/// Represents a point-in-time snapshot of document contents.
/// </summary>
/// <remarks>
/// Although document versions are immutable (updates are not allowed), 
/// the <c>UpdatedAt</c> timestamp is explicitly required to accurately track when a version 
/// is soft-deleted and to support delta-synchronization queries fetching changes since a specific baseline.
/// </remarks>
public class DocumentVersion : AuditedEntity, IHasConcurrencyStamp
{
    protected List<DocumentChunk> DocumentChunks { get; set; } = [];

    public string ContentHash { get; set; } = string.Empty;

    public long DocumentId { get; protected set; }

    public Document Document
    {
        get;
        protected set
        {
            field = value;
            if (value != null)
            {
                DocumentId = value.Id;
            }
        }
    } = null!;

    public Guid FileId { get; set; }

    public string FileType { get; set; } = string.Empty;

    public string? ConcurrencyStamp { get; set; }

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

    public DocumentVersion(
       Guid externalId,
       string contentHash,
       long documentId,
       Guid fileId,
       string fileType)
    {
        ContentHash = contentHash;
        DocumentId = documentId;
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