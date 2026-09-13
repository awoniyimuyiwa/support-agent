using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.EntityFrameworkCore;
using SqliteWasmHelper;
using Web.Client;
using Web.Client.Data;
using Web.Client.Http;
using Web.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// The builder.RootComponents.Add<App>("#app") and 
// builder.RootComponents.Add<HeadOutlet>("head::after") lines have been completely removed.
// In an InteractiveAuto architecture, root components are mounted server-side by 
// Web/Components/App.razor. Leaving them here will cause WebAssembly initialization 
// errors inside the browser because it will try to find DOM elements it cannot control.

builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();

// Configures WebAssembly to extract the serialized authentication state from 
// the server's HTML payload during the initial page load and hydration process.
builder.Services.AddAuthenticationStateDeserialization();

builder.Services.AddTransient<AntiforgeryDelegatingHandler>();
builder.Services.AddHttpClient<ApiClient>(client =>
{
    // Point to the Web application proxy endpoint for API calls, which will forward to the API service.
    var baseUri = new Uri(builder.HostEnvironment.BaseAddress);
    client.BaseAddress = new Uri(baseUri, "api/");
})
.AddHttpMessageHandler<AntiforgeryDelegatingHandler>();

// Register the sWebAssembly SQLite persistent engine factory
builder.Services.AddSqliteWasmDbContextFactory<ApplicationDbContext>(options =>
{
    // The "Data Source" name determines the unique storage file key inside IndexedDB
    // Increment the file name integer (v1 -> v2 e.g app_client_cachev2) when entities change
    options.UseSqlite("Data Source=app_client_cache.sqlite3");
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ISyncService, SyncService>();

var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Web.Client WebAssembly engine has successfully booted inside the browser sandbox!");

// Execute Async Database and Queue Validation Boot Sequence before launching the UI
// Pass the target bootLogger instance straight down to support structural data writing loops
await InitializeStorage(host.Services, logger);

await host.RunAsync();

/// <summary>
/// Orchestrates pre-flight cache allocation and recovers broken sync queues at app boot time.
/// </summary>
static async Task InitializeStorage(IServiceProvider services, ILogger logger)
{
    try
    {
        var dbFactory = services.GetRequiredService<ISqliteWasmDbContextFactory<ApplicationDbContext>>();

        using var context = await dbFactory.CreateDbContextAsync();

        // Ensure the SQLite schema is initialized and up to date inside IndexedDB
        // This creates tables dynamically on first visit without full migration baggage overhead
        await context.Database.EnsureCreatedAsync();

        // Check for Unfinished In-Flight Synchronization Operations
        var interruptedBatch = await context.SyncBatches
            .OrderBy(x => x.CreatedAt)
            .FirstOrDefaultAsync();

        if (interruptedBatch is not null)
        {
            // An active batch metadata entry was left stranded because the user closed 
            // the tab or their battery died mid-transmission.             
            logger.LogWarning("Interrupted data synchronization batch discovered during startup processing sequence. Stranded Idempotency Key: {BatchId}", interruptedBatch.Id);

            var syncService = services.GetRequiredService<ISyncService>();
            await syncService.ResumeInterruptedBatch(interruptedBatch.Id);
        }
        else
        {
            logger.LogInformation("Local SQLite storage engine validation finished. Outbound data synchronization queue is clean.");
        }
    }
    catch (Exception ex)
    {        
        logger.LogError(ex, "Pre-flight client database storage initialization pass crashed.");
    }
}
