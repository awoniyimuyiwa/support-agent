namespace Web.Client.Data;

public interface ISyncableEntity
{
    Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public bool IsDeleted { get; set; }

    string? ConcurrencyStamp { get; set; }

    SyncStatus SyncStatus { get; set; }

    public string? SerializedErrors { get; set; } // JSON array of errors
}

