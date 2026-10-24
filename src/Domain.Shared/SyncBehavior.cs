namespace Domain.Shared;

public enum SyncBehavior
{
    Default,         // Validates concurrency stamps and catches conflicts (returns 207 if stale)
    ClientWins  // Bypasses concurrency stamp verification and forces client data into the DB
}
