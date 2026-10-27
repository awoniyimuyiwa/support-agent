namespace Web.Client.Data;

public enum SyncStatus : byte
{
    Synced = 0,
    PendingInsert = 1,
    PendingUpdate = 2,
    PendingDelete = 3
}

