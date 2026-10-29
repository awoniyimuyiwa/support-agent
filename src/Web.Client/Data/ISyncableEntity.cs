namespace Web.Client.Data;

public interface ISyncableEntity
{
    Guid Id { get; set; }

    string? ConcurrencyStamp { get; set; }

    SyncStatus SyncStatus { get; set; }

    public string? SerializedErrors { get; set; } // JSON array of errors
}

