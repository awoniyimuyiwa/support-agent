namespace Web.Client.Data;

public class SyncBatch
{
    public Guid Id { get; set; }

    public string EntityTypeName { get; set; } = string.Empty;

    /// <summary>
    /// Base64-encoded SHA-256 hash of the serialized request payload DTO.
    /// Computed from the mapped request rather than the raw entities so it tracks the exact
    /// wire payload validated by the server's idempotency filter, avoiding false mismatches from
    /// local-only metadata (such as SyncStatus or SerializedErrors) and preventing serialization
    /// errors from EF Core navigation properties.
    /// </summary>
    public string Hash { get; set; } = string.Empty;

    /// <summary>
    /// JSON array of entity IDs included
    /// </summary>
    public string SerializedEntityIds { get; set; } = string.Empty;

    public SyncType Type { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum SyncType
{
    Sync = 0,

    UploadFiles = 1,

    Create = 2
}

