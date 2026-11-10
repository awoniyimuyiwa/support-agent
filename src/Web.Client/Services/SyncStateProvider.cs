
namespace Web.Client.Services;

public class SyncStateProvider
{
    public event Func<Task>? OnSyncCompleted;

    public async Task NotifyCompleted()
    {
        if (OnSyncCompleted is null) return;

        // Cleanly invoke and await all attached async page handlers sequentially
        foreach (Func<Task> handler in OnSyncCompleted.GetInvocationList())
        {
            await handler();
        }
    }
}
