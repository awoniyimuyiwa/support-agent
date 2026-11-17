namespace Web.Client.Data;

public interface ISyncableEntity
{
    Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public bool IsDeleted { get; set; }

    /// <summary>
    /// Server-controlled optimistic concurrency token updated exclusively when applying deltas or sync responses from the backend.
    /// Used to detect server-side modifications between sync cycles.
    /// </summary>
    string? ConcurrencyStamp { get; set; }

    /// <summary>
    /// Client-controlled optimistic concurrency token rotated on every local mutation and sync state transition.
    /// Prevents in-flight sync responses and concurrent browser tabs from overwriting newer local changes.
    /// </summary>
    string? LocalConcurrencyStamp { get; set; }

    SyncStatus SyncStatus { get; set; }

    /// <summary>
    /// JSON array of errors
    /// </summary>
    public string? SerializedErrors { get; set; }
}

