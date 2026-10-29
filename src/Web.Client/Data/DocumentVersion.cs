namespace Web.Client.Data;

public class DocumentVersion : IHasFile
{
    public Guid Id { get; set; }

    public string ContentHash { get; set; } = string.Empty;

    public Guid DocumentId { get; set; }

    public Document? Document { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public string? ConcurrencyStamp { get; set; }

    public Guid StoredFileId { get; set; }

    public StoredFile? StoredFile { get; set; }

    public bool IsDeleted { get; set; }

    public string? SerializedErrors { get; set; }

    public SyncStatus SyncStatus { get; set; } = SyncStatus.Synced;
    
}
