namespace Web.Client.Data;

public class Document : ISyncableEntity
{
    public Guid Id { get; set; }

    public Guid DocumentCategoryId { get; set; }

    public DocumentCategory? DocumentCategory { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public string? ConcurrencyStamp { get; set; }

    public bool IsDeleted { get; set; }

    public SyncStatus SyncStatus { get; set; } = SyncStatus.Synced;
}
