using Api.Contracts;
using Api.Contracts.Options;
using Azure.Core;
using Azure.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.OutputCaching.StackExchangeRedis;
using Microsoft.EntityFrameworkCore;
using Polly;
using ServiceDefaults;
using SqliteWasmHelper;
using Web.Auth;
using Web.Client;
using Web.Client.Data;
using Web.Client.Services;
using Web.Components;
using Web.Extensions;
using Web.Http;
using Web.Options;
using Web.Services;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);

using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger("WebStartup");

var appConfigConnectionString = builder.Configuration.GetConnectionString("AppConfig");

var isDevelopment = builder.Environment.IsDevelopment();

var isAspireEmulator = !string.IsNullOrWhiteSpace(appConfigConnectionString)
    && appConfigConnectionString.Contains("localhost");

var managedIdentityClientId = builder.Configuration["Azure:ManagedIdentityClientId"];

TokenCredential tokenCredential;
// Check both the environment profile state and connection string flag.
// This ensures that headless integration test suites use DefaultAzureCredential
// for emulator handshakes instead of hanging on real cloud ManagedIdentity calls
if (isDevelopment || isAspireEmulator)
{
    tokenCredential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
    {
        // Skip checking real cloud directories for the emulator's mock tenant ID
        DisableInstanceDiscovery = isAspireEmulator,

        ExcludeManagedIdentityCredential = true,
        ExcludeWorkloadIdentityCredential = true
    });
}
else
{
    tokenCredential = new ManagedIdentityCredential(
        new ManagedIdentityCredentialOptions(
            !string.IsNullOrWhiteSpace(managedIdentityClientId)
                ? ManagedIdentityId.FromUserAssignedClientId(managedIdentityClientId)
                : ManagedIdentityId.SystemAssigned
        )
    );
}

if (!string.IsNullOrWhiteSpace(appConfigConnectionString))
{
    builder.AddCustomAzureAppConfiguration(
        appConfigConnectionString,
        tokenCredential,
        isAspireEmulator);
}

// Changing the app name value will invalidate existing cache entries and data protection keys.
var appName = builder.Configuration.GetValue<string>("AppName")
    ?? throw new InvalidOperationException("Setting 'AppName' is missing from configuration.");
appName = $"{appName}-UI";

builder.Services.AddOptions<CustomDataProtectionOptions>()
    .Bind(builder.Configuration.GetSection(CustomDataProtectionOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<ForwardedHeadersOptions>().ValidateOnStart();
builder.Services.ConfigureOptions<ConfigureForwardedHeadersOptions>();

// Clears Kestrel's signature
builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
});

builder.Services.PostConfigure<RedisOutputCacheOptions>(options =>
{
    options.InstanceName = $"{appName}:";
});

// Add services to the container.

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

builder.AddRedisOutputCache("Redis");

builder.Services.AddOutputCache(options =>
{
    // Define a global named policy to tag UI layouts cleanly
    // This makes it easy to Inject IOutputCacheStore into your deployment initializer or sync service
    // This instantly clears every single cache item versioned under appName tag via: await cacheStore.EvictByTagAsync(appName, cancellationToken);
    options.AddPolicy(appName, policy =>
    {
        policy.Tag(appName);

        // explicitly varies its cache key per user session.
        policy.SetVaryByRouteValue("*");
        policy.SetVaryByHeader("Cookie");
    });
});

/* 
 * Prevents a cryptographic split-brain error (HTTP 400 Bad Request) across scaled BFF nodes if Node 2 handles 
 * an antiforgery token issued by Node 1. Keys are encrypted via Key Vault/Certificates and shared in Redis, 
 * allowing seamless verification across container restarts, code updates, and auto-scaling events.
 */
var customDataProtectionOptions =
    builder.Configuration.GetRequiredSection(CustomDataProtectionOptions.SectionName)
    .Get<CustomDataProtectionOptions>();
builder.Services.AddCustomDataProtection(
    appName,
    customDataProtectionOptions!,
    logger,
    tokenCredential,
    isAspireEmulator);

/*
 * Antiforgery is mandatory for Blazor to securely authorize the initial SignalR 
 * WebSocket circuit execution loops, even when omitting traditional form elements.
 */
builder.Services.AddAntiforgery(options =>
{
    // Completely masks the default ".AspNetCore.Antiforgery" framework signature
    options.Cookie.Name = "__Host-UI-Antiforgery";

    options.Cookie.Path = "/";
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;

    // Lax Permits safe cross-site deep links (bookmarks, email notifications) to attach the session cookie on entry, while YARP routes API queries securely under SameSite.Strict.
    options.Cookie.SameSite = SameSiteMode.Lax;
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<CookieDelegatingHandler>();

var apiAddress = builder.Configuration["API_HTTPS"]
    ?? builder.Configuration["API_HTTP"];
/**
  Even though 'ApiClient' is defined inside the 'Web.Client' assembly, it MUST also be 
  registered here.
  
  Because this solution leverages '@rendermode InteractiveAuto', the server executes a fast, 
  initial pre-render pass to generate completed HTML for instant loading and SEO crawlers. 
  Without this twin registration, the server-side engine will crash with a dependency injection 
  exception before the application ever reaches the browser's WebAssembly sandbox.
*/
builder.Services.AddHttpClient<ApiClient>(client =>
{
    var baseAddress = apiAddress ?? "https+http://api";
    var finalUrl = baseAddress.EndsWith('/') ? $"{baseAddress}api/" : $"{baseAddress}/api/";
    client.BaseAddress = new Uri(finalUrl);
})
 .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
 {
     // Instructs the handler to allow manual manipulation of cookies
     UseCookies = false
 })
.AddHttpMessageHandler(sp =>
{
    var httpContextAccessor = sp.GetRequiredService<IHttpContextAccessor>();
    return new CookieDelegatingHandler(httpContextAccessor);
})
// Must match the exact same retry criteria as the Web.Client project
// .NET registers a five-strategy resilience pipeline out of the box (Total Timeout, Retry, Circuit Breaker, Attempt Timeout, and Rate Limiter).
.AddStandardResilienceHandler(options =>
{
    // .Net implements 5 resiliene startegies out of the box
    options.Retry.MaxRetryAttempts = 3;
    options.Retry.BackoffType = DelayBackoffType.Exponential;
    options.Retry.UseJitter = true;
    options.Retry.ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
        .HandleResult(response =>
            (int)response.StatusCode >= 500 ||
            response.StatusCode == System.Net.HttpStatusCode.TooManyRequests ||
            response.StatusCode == System.Net.HttpStatusCode.RequestTimeout)
        .Handle<HttpRequestException>();
});

// Security and cascading auth state (For Server Pre-Rendering Protection)
builder.Services.AddTransient<CustomAuthenticationHandler>();
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CustomAuthenticationHandler.AuthSchemeName;

    // If an unauthenticated user hits an [Authorize] route, delegate the challenge handling 
    // to the native Cookie authentication scheme container
    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
}).AddScheme<AuthenticationSchemeOptions, CustomAuthenticationHandler>(CustomAuthenticationHandler.AuthSchemeName, options => { })
.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
 {
     // Explicitly forces the network layer to redirect unauthenticated address bar entries back to login page, rather than returning a 401 Unauthorized response.
     options.LoginPath = "/login";
     options.AccessDeniedPath = "/403";

     // Match auth cookie settings parameters with API service
     options.Cookie.Name = Constants.AuthCookieName;
     options.Cookie.Path = "/";
     options.Cookie.SecurePolicy = CookieSecurePolicy.Always;

     // Lax Permits safe cross-site deep links
     options.Cookie.SameSite = SameSiteMode.Lax;
 });
builder.Services.AddAuthorization();

// Registers the unified core authentication state task container.
// This flows security context metadata cleanly down to all <AuthorizeView> 
// tags and protected route views across both Server and WebAssembly layers.
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    // For WASM Support
    .AddInteractiveWebAssemblyComponents()
    // Configures Blazor to serialize the authenticated identity (including custom claims)
    // into the HTML during server prerendering, which the WebAssembly client automatically 
    // extracts during the hydration process.
    .AddAuthenticationStateSerialization(options => 
    {
        // Allows custom claims to flow down to WASM
        options.SerializeAllClaims = true;
     });

// Register dummy service implementations to support safe server-side pre-rendering
builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseSqlite("Data Source=:memory:"));
builder.Services.AddScoped(typeof(ISqliteWasmDbContextFactory<>), typeof(ServerSqliteWasmDbContextFactory<>));
builder.Services.AddScoped<ISyncService, ServerSyncService>();

// Enable YARP mapping layers.
builder.Services.AddHttpForwarder();

var app = builder.Build();

if (!string.IsNullOrWhiteSpace(appConfigConnectionString))
{
    // Must be at the top so configuration updates are applied before subsequent middleware executes.
    app.UseAzureAppConfiguration();
}

app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        context.Response.Headers.Remove("X-Powered-By");
        context.Response.Headers.Remove("Server");
        return Task.CompletedTask;
    });
    await next();
});

/*
 * Enable forwarded headers for when this app itself is behind a reverse proxy.
 * 
 * BLAZOR & HTTPS OFFLOADING NOTES:
 * Essential for edge proxies running SSL termination (e.g., Cloudflare, AWS ALB).
 * Without this, the server handles traffic over internal HTTP and mistakenly generates 
 * script, asset, and WebAssembly paths using 'http://' instead of 'https://', 
 * causing browsers to reject them with critical mixed-content security blocks.
 *
 * Furthermore, modern '.NET' mechanisms like 'MapStaticAssets()' rely directly on 
 * the validated host and scheme context to map fingerprint hashes and cache-safety headers.
 */
app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();

app.MapStaticAssets();

app.UseRouting();

app.UseAuthentication();

app.UseAntiforgery();

app.UseAuthorization();

// Output Cache below Authorization so it recognizes user identity
app.UseOutputCache();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(Routes).Assembly)
    .CacheOutput(appName);

// YARP reverse proxy
#pragma warning disable ASP0018 
app.MapForwarder("/api/{**_catchAll}", apiAddress ?? "http://api", transformBuilder =>
{
    // Automatically adds X-Forwarded-For, X-Forwarded-Proto, X-Forwarded-Host, and X-Forwarded-Prefix.
    transformBuilder.AddXForwarded();
});
#pragma warning restore ASP0018

app.MapDefaultEndpoints();

app.Run();
