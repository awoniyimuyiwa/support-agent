namespace Web.Client.Data;

public class DocumentCategory : ISyncableEntity
{
    public Guid Id { get; set; }

    public string? Description { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public string? ConcurrencyStamp { get; set; }

    public bool IsDeleted { get; set; }

    public SyncStatus SyncStatus { get; set; } = SyncStatus.Synced;
}
