using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.EntityFrameworkCore;
using Polly;
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
.AddHttpMessageHandler<AntiforgeryDelegatingHandler>()
.AddStandardResilienceHandler(options =>
{
    options.Retry.MaxRetryAttempts = 3;
    options.Retry.BackoffType = DelayBackoffType.Exponential;
    options.Retry.UseJitter = true; // Adds random jitter to prevent thundering herd

    // Explicitly allow it to safely retry idempotent POST/PUT requests
    options.Retry.ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
        .HandleResult(response =>
            (int)response.StatusCode >= 500 
            || response.StatusCode == System.Net.HttpStatusCode.TooManyRequests 
            || response.StatusCode == System.Net.HttpStatusCode.RequestTimeout)
        .Handle<HttpRequestException>();

    // Default is 2s
    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(30);

    // Must be >= double the AttemptTimeout (30s * 2 = 60s)
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);

    // Default is 10s
    options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(2);
});

// Register the sWebAssembly SQLite persistent engine factory
builder.Services.AddSqliteWasmDbContextFactory<ApplicationDbContext>(options =>
{
    // The "Data Source" name determines the unique storage file key inside IndexedDB
    // Increment the file name integer (v1 -> v2 e.g app_client_cachev2) when entities change
    options.UseSqlite("Data Source=app_client_cache.sqlite3");
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<SyncService>();

var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Web.Client WebAssembly engine has successfully booted inside the browser sandbox!");

if (OperatingSystem.IsBrowser())
{
    await InitializeStorage(host.Services, logger);
}
   
AppDomain.CurrentDomain.UnhandledException += (sender, errorArgs) =>
{
    if (errorArgs.ExceptionObject is Exception exception)
    {
        // Instantly routes to the browser console logs
        logger.LogError(exception, "Client side unhandled exception:{Message}", exception.Message);

        // Optional: Call ApiClient inside this handler to push 
        // stack traces back to BFF server
    }
};

// Catch unhandled exceptions in async background tasks (like sync loops)
TaskScheduler.UnobservedTaskException += (sender, errorArgs) =>
{
    logger.LogError(errorArgs.Exception, "Unhandled background task exception: {Message}", errorArgs.Exception.Message);

    // Prevent background failure from potentially destabilizing the WASM runtime thread
    errorArgs.SetObserved();
};

await host.RunAsync();

/// <summary>
/// Orchestrates pre-flight cache allocation
/// </summary>
static async Task InitializeStorage(IServiceProvider services, ILogger logger)
{
    try
    {
        var dbFactory = services.GetRequiredService<ISqliteWasmDbContextFactory<ApplicationDbContext>>();

        using var context = await dbFactory.CreateDbContextAsync();

        // Ensure the SQLite schema is initialized and up to date inside IndexedDB
        // This creates tables dynamically on first visit
        await context.Database.EnsureCreatedAsync();
        
        logger.LogInformation("Local SQLite storage engine validation finished.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Pre-flight client database storage initialization pass failed: {Message}", ex.Message);
    }
}
