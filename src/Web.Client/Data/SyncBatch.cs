namespace Web.Client.Data;

public class SyncBatch
{
    public Guid Id { get; set; }

    public string EntityTypeName { get; set; } = string.Empty;

    public string SerializedEntityIds { get; set; } = string.Empty; // JSON array of entity IDs included

    public SyncType Type { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum SyncType
{
    Sync = 0,

    GetUploadUrls = 1,

    Create = 2
}

