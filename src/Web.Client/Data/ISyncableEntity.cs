namespace Web.Client.Data;

public interface ISyncableEntity
{
    Guid Id { get; set; }

    string? ConcurrencyStamp { get; set; }

    public bool IsDeleted { get; set; }

    SyncStatus SyncStatus { get; set; }
}

