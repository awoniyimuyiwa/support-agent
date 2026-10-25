using AgentGovernance;
using AgentGovernance.Policy;
using AgentGuard.Core.Builders;
using AgentGuard.Core.ChatClient;
using AgentGuard.Pii;
using Api;
using Api.AI;
using Api.Attributes;
using Api.BackgroundServices;
using Api.Contracts.Extensions;
using Api.Contracts.Models.V1;
using Api.Contracts.Options;
using Api.Controllers.V1;
using Api.Conventions;
using Api.Extensions;
using Api.Filters;
using Api.Middlewares;
using Api.OpenApi;
using Api.Options;
using Api.Services;
using Api.Utils;
using Application.Contracts;
using Application.Extensions;
using Asp.Versioning;
using Azure.Core;
using Azure.Identity;
using Domain;
using Infrastructure.EntityFrameworkCore.Extensions;
using MaxMind.GeoIP2;
using Medallion.Threading;
using Medallion.Threading.Postgres;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using OllamaSharp;
using Polly;
using Polly.Retry;
using Polly.Timeout;
using Scalar.AspNetCore;
using ServiceDefaults;
using StackExchange.Redis;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading.RateLimiting;
using UAParser;

var builder = WebApplication.CreateBuilder(args);

using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger("APIStartup");

var isDevelopment = builder.Environment.IsDevelopment();

var appConfigConnectionString = builder.Configuration.GetConnectionString("AppConfig");

var isAspireEmulator = !string.IsNullOrWhiteSpace(appConfigConnectionString) 
    && appConfigConnectionString.Contains("localhost");

var managedIdentityClientId = builder.Configuration["Azure:ManagedIdentityClientId"];

TokenCredential tokenCredential;
// Check both the environment profile state and connection string flag.
// This ensures that headless integration test suites use DefaultAzureCredential
// for emulator handshakes instead of hanging on real cloud ManagedIdentity calls!
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

// Changing the app name value will invalidate existing cache entries, rate limiting keys,
// auth tickets and data protection keys.
// Inside your Program.cs file
string appName = !string.IsNullOrWhiteSpace(builder.Configuration.GetValue<string>("AppName"))
    ? builder.Configuration.GetValue<string>("AppName")!
    : throw new InvalidOperationException("AppHost Boot Error: Setting 'AppName' must be explicitly specified in configurations and cannot be empty or whitespace.");

builder.Services.AddOptions<AiOptions>()
    .Bind(builder.Configuration.GetSection(AiOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<AuditLogOptions>()
    .Bind(builder.Configuration.GetSection(AuditLogOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<BackgroundJobOptions>()
    .BindConfiguration(BackgroundJobOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<CustomDataProtectionOptions>()
    .Bind(builder.Configuration.GetSection(CustomDataProtectionOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<FileStorageOptions>()
    .BindConfiguration(FileStorageOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<IdempotencyOptions>()
    .Bind(builder.Configuration.GetSection(IdempotencyOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<RateLimitOptions>()
    .Bind(builder.Configuration.GetSection(RateLimitOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<SecurityOptions>()
    .Bind(builder.Configuration.GetSection(SecurityOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<ForwardedHeadersOptions>().ValidateOnStart();

builder.Services.ConfigureOptions<ConfigureForwardedHeadersOptions>()
    .ConfigureOptions<ConfigureBearerTokenOptions>()
    .ConfigureOptions<ConfigureCookieAuthenticationOptions>()
    .ConfigureOptions<ConfigureJsonOptions>()
    .ConfigureOptions<ConfigureMvcJsonOptions>();

// Clears Kestrel's signature during local debugging and container hosts
builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
});

// Add services to the container.

// Add service defaults & Aspire client integrations. Adds OpenTelemetry, logs, service discovery, and health checks
builder.AddServiceDefaults();

builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddExceptionHandler<ExceptionHandler>();

builder.Services.AddProblemDetails();

builder.Services.AddControllers(options =>
{
    options.Conventions.Add(new RoutePrefixConvention("api"));
    options.Filters.Add<AntiforgeryFilter>();
    options.Filters.Add<IdempotencyFilter>();
}).AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddApiVersioning(options =>
{
    //options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    // Choose versioning strategy (e.g., URL segment)
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
})
.AddMvc()
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
})
.AddOpenApi(options =>
{
    // Fix: Access .Document to apply your custom native .NET 10 document transformers
    options.Document.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info.Title = appName.AddSpacesToPascalCase;
        return Task.CompletedTask;
    });

    options.Document.AddSchemaTransformer<EnumSchemaTransformer>();
    options.Document.AddOperationTransformer<AntiforgeryHeaderOperationTransformer>();
    options.Document.AddOperationTransformer<IdempotencyHeaderOperationTransformer>();
});

builder.Services.AddKeyedSingleton(Domain.Shared.Constants.AuditLogJsonSerializationOptionsName, new JsonSerializerOptions
{
    TypeInfoResolver = new DefaultJsonTypeInfoResolver
    {
        Modifiers = { AuditLogJsonModifier.Modify }
    },
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
});

var cacheVersion = builder.Configuration.GetValue<string>("CacheOptions:Version") ?? "V1";
var cachePrefix = $"{appName}:{cacheVersion}:";

// Let Aspire initialize the core telemetry, metrics, and connection pools natively.
builder.AddRedisClient("redis");

// Find Aspire's original registration descriptor in the collection list
var aspireRedisDescriptor = builder.Services.FirstOrDefault(d => d.ServiceType == typeof(IConnectionMultiplexer));

if (aspireRedisDescriptor is not null)
{
    builder.Services.Remove(aspireRedisDescriptor);

    // re-register it as a KEYED service under a unique string name!
    // This safely isolates Aspire's original factory away from default lookups.
    builder.Services.AddKeyedSingleton("UnprefixedRedis",
        (sp, key) => (IConnectionMultiplexer)aspireRedisDescriptor.ImplementationFactory!.Invoke(sp));
}

// Register the default IConnectionMultiplexer that all third-party packages will pull.
// Because it calls the KEYED service explicitly, there is zero risk of an infinite loop
builder.Services.AddSingleton(sp =>
{
    var underlyingRedis = sp.GetRequiredKeyedService<IConnectionMultiplexer>("UnprefixedRedis");
    return underlyingRedis.WithCustomKeyPrefix(cachePrefix);
});

builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<IConnectionMultiplexer>().GetDatabase());

builder.Services.AddOptions<RedisCacheOptions>()
    .Configure<IServiceProvider>((options, sp) =>
    {
        options.InstanceName = string.Empty; // Avoids double-prefixing
        options.ConnectionMultiplexerFactory = () =>
            Task.FromResult(sp.GetRequiredService<IConnectionMultiplexer>());
    });

builder.Services.AddStackExchangeRedisCache(_ => { });

// Let Aspire initialize the core telemetry, metrics, and connection pools natively.
builder.AddAzureBlobServiceClient("blobs");

builder.Services.AddSingleton(sp =>
{
    return new GovernanceKernel(new GovernanceOptions
    {
        // Points to the YAML file where enterprise guardrails live
        PolicyPaths = ["AI/policies/default.yml"],

        ConflictStrategy = ConflictResolutionStrategy.DenyOverrides,
        EnableRings = true,
        EnablePromptInjectionDetection = true,
        EnableCircuitBreaker = true
    });
});

// Let Aspire initialize the core telemetry, metrics, and connection pools natively.
builder.AddOllamaApiClient("ollama");

var aiOptions = builder.Configuration.GetSection(AiOptions.SectionName).Get<AiOptions>()
    ?? throw new InvalidOperationException("Critical Configuration Section 'AiOptions' is missing.");

builder.Services.AddEmbeddingGenerator(sp => {
    var client = sp.GetRequiredService<OllamaApiClient>();
    client.SelectedModel = aiOptions.EmbeddingModelName;
    return client;
});

builder.Services.AddChatClient(sp => {
    var coreClient = sp.GetRequiredService<OllamaApiClient>();
    coreClient.SelectedModel = aiOptions.ChatModelName;
   
    IChatClient chatClient = coreClient;

    chatClient = chatClient    
    .AsBuilder()   
    .UseLogging()   
    .Use(inner => new CustomDelegatingChatClient(    
        inner,    
        sp.GetRequiredService<GovernanceKernel>()))    
    .Build();

    // This ensures AgentGuard acts as the outermost shield. It cleans incoming 
    // prompts (PII & injection screening) BEFORE your middleware does access checks!
    return chatClient
    .UseAgentGuard(new GuardrailPolicyBuilder()    
    .NormalizeInput()    
    .BlockPromptInjection()    
    .LimitInputTokens(aiOptions.MaxSafeInputTokens)
    .LimitOutputTokens(aiOptions.MaxOutputTokens)
    .RedactPii()    
    .Build());
});


builder.Services.AddScoped<TicketResolutionTools>();

builder.Services.AddTransient<SupportAgent>();

builder.AddEntityFrameworkServices();

builder.Services.AddApplicationServices();


// Configure distributed lock provider here, SQL server, Postgresql or Redis. Zookeeper requires ZooKeeperNetEx library
builder.Services.AddSingleton<IDistributedLockProvider>(
    serviceProvider => new PostgresDistributedSynchronizationProvider(builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.")));

builder.Services.AddResiliencePipeline<string, IDistributedSynchronizationHandle?>(
    Api.Constants.DistributedLockResiliencePolicyName, (builder, context) =>
    {
        builder
            // Per-attempt timeout: Ensures a stuck network call doesn't hang the background service
            .AddTimeout(TimeSpan.FromSeconds(5))
            .AddRetry(new RetryStrategyOptions<IDistributedSynchronizationHandle?>
            {
                ShouldHandle = new PredicateBuilder<IDistributedSynchronizationHandle?>()
                    .Handle<TimeoutRejectedException>() // Matches the timeout above
                    .Handle<Exception>()
                    .HandleResult(result => result is null),
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(10),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true // Prevent thundering herd
            });
    });

builder.Services.AddSingleton<AuditLogCleanupTrigger>();
builder.Services.AddSingleton<IAuditLogCleanupTrigger>(sp => sp.GetRequiredService<AuditLogCleanupTrigger>());
builder.Services.AddSingleton<IAuditLogCleanupReader>(sp => sp.GetRequiredService<AuditLogCleanupTrigger>());
builder.Services.AddHostedService<AuditLogCleanupService>();

builder.Services.AddSingleton<BackgroundJobTrigger>();
builder.Services.AddSingleton<IBackgroundJobTrigger>(sp => sp.GetRequiredService<BackgroundJobTrigger>());
builder.Services.AddSingleton<IBackgroundJobReader>(sp => sp.GetRequiredService<BackgroundJobTrigger>());
builder.Services.AddHostedService<BackgroundJobService>();

builder.Services.AddSingleton<RateLimitService>();
builder.Services.AddRateLimiter(rateLimiterOptions =>
{
    // Distributed global policy (Cluster-wide protection)
    // Ensures the entire cluster does not exceed global rate limit (e.g. 5000)
    rateLimiterOptions.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var rateLimitService = context.RequestServices.GetRequiredService<RateLimitService>();
        return rateLimitService.GetGlobalPolicyPartition(context);
    });

    // Distributed user Policy (Per-user/IP protection)
    rateLimiterOptions.AddPolicy(Api.Constants.UserRateLimitPolicyName, context =>
    {
        var rateLimitService = context.RequestServices.GetRequiredService<RateLimitService>();
        return rateLimitService.GetUserPolicyPartition(context);
    });

    // Unified rejection handling
    rateLimiterOptions.OnRejected = (context, token) =>
    {
        var rateLimitService = context.HttpContext.RequestServices.GetRequiredService<RateLimitService>();
        return rateLimitService.HandleOnRejected(context, token);
    };
});


// Add user agent parser
builder.Services.AddSingleton(_ => Parser.GetDefault());

// Add MaxMind DatabaseReader for geo location-based features
builder.Services.AddSingleton<IGeoIP2DatabaseReader>(sp =>
{
    var path = Path.Combine(
        builder.Environment.ContentRootPath,
        "Data",
        "GeoLite2-City.mmdb");

    if (!File.Exists(path))
    {
        throw new FileNotFoundException($"GeoLite database not found at {path}. Ensure 'Copy to Output Directory' is configured.");
    }

    return new DatabaseReader(path);
});

builder.Services.AddHttpContextAccessor();

builder.Services.AddSingleton<RedisTicketStore>();
builder.Services.AddSingleton<ITicketStore>(sp => sp.GetRequiredService<RedisTicketStore>());
builder.Services.AddSingleton<ISecureDataFormat<AuthenticationTicket>>(sp => sp.GetRequiredService<RedisTicketStore>());
builder.Services.AddSingleton<IUserSessionManager>(sp => sp.GetRequiredService<RedisTicketStore>());

var customDataProtectionOptions =
    builder.Configuration.GetRequiredSection(CustomDataProtectionOptions.SectionName)
    .Get<CustomDataProtectionOptions>();
builder.Services.AddCustomDataProtection(
    appName,
    customDataProtectionOptions!,
    logger,
    tokenCredential,
    isAspireEmulator);

// Register the specific base IDataProtector for your application
builder.Services.AddSingleton<IDataProtector>(sp =>
{
    var provider = sp.GetRequiredService<IDataProtectionProvider>();

    return provider.CreateProtector(cachePrefix);
});

builder.Services.AddAntiforgery(options =>
{
    // In modern browsers, the __Host- prefix enforces strict security: 
    // Must be HTTPS (Secure) 
    // Must have Path=/ 
    // Prevents subdomains from overriding or 'tossing' this cookie (Domain Locking).
    options.Cookie.Name = Api.Constants.AntiforgeryCookieName;

    options.Cookie.HttpOnly = true;
    options.Cookie.Path = "/";
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;

    // The Antiforgery system will look for the request token in this header.
    // SPAs must include this header for state-changing requests (POST, PUT, PATCH, DELETE)
    // when using Cookie Authentication to prevent Cross-Site Request Forgery (CSRF).
    // The value should be read from the 'XSRF-TOKEN' cookie (provided by the /auth/antiforgery-token endpoint)
    // and attached to the request headers via JavaScript.
    options.HeaderName = Api.Contracts.Constants.AntiforgeryHeaderName;
});

builder.Services.AddSingleton<IActivityProvider>(new ActivityProvider(appName));

// Registers the services required for the [RequestTimeout] attribute.
// Note: This must be paired with app.UseRequestTimeouts() in the middleware pipeline
// for the timeouts to be actively monitored and enforced.
builder.Services.AddRequestTimeouts();

builder.Services.AddAuthorization();

builder.Services.AddIdentityApiEndpoints<ApplicationUser>(options =>
{
    // Forces the UserManager to reject duplicate email records natively at the API gate
    options.User.RequireUniqueEmail = true;
}).AddRoles<ApplicationRole>()    
.AddCustomEntityFrameworkStores();

// Add the database developer page exception filter service
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddDatabaseDeveloperPageExceptionFilter();
}

var app = builder.Build();

if (!string.IsNullOrWhiteSpace(appConfigConnectionString))
{
    app.UseAzureAppConfiguration();
}

// Clear platform signatures dynamically regardless of the underlying OS
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

app.UseForwardedHeaders();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
    app.UseDeveloperExceptionPage(); // Detailed page for development
    app.MapOpenApi().WithDocumentPerVersion();
    app.MapScalarApiReference();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "v1");

    });

    app.MapGet("/", (context) =>
    {
        context.Response.Redirect("/swagger");
        return Task.CompletedTask;
    });
}
else
{
    app.UseExceptionHandler(); // Use the registered IExceptionHandler in non dev environments
    app.UseHsts();
}

app.UseHttpsRedirection();

// Explicitly call Routing so we can identify the endpoint early
app.UseRouting();

// Expose health checks before security/auth blocks execute
app.MapDefaultEndpoints();

app.Use(async (context, next) =>
{
    // Conditional Buffering based on the [Idempotent] attribute
    /* 
     * EnableBuffering() must be called in this middleware (post-routing) to ensure the request 
     * stream is seekable before it reaches the Model Binder or the IdempotencyFilter.
     * 
     * If called later inside an Action Filter, the Binder has typically already consumed 
     * the forward-only stream. Attempting to buffer at that stage results in an 
     * exhausted stream and an empty SHA-256 hash (47DEQ...).
     */
    var endpoint = context.GetEndpoint();
    var isIdempotent = endpoint?.Metadata.GetMetadata<IdempotentAttribute>() != null;

    if (isIdempotent)
    {
        context.Request.EnableBuffering();
    }

    await next();
});

// Security headers
app.UseWhen(
    context => !context.Request.Path.StartsWithSegments("/scalar")
               && !context.Request.Path.StartsWithSegments("/swagger"),
    appBuilder =>
    {
        appBuilder.UseXContentTypeOptions();
        appBuilder.UseXfo(options => options.Deny());
        appBuilder.UseCsp(options => options
            .DefaultSources(s => s.None())
            .FrameAncestors(s => s.None())
        );
    }
);

app.Use(async (context, next) =>
{
    // Expose the W3C Trace ID in the response header to correlate client requests with server-side telemetry.
    var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
    context.Response.Headers.Append(Constants.TraceIdHeaderName, traceId);
    await next();
});

/* 
 * CORS is currently disabled because the Frontend and API should ideally share the same domain.
 * Under the Same-Origin Policy (SOP), the browser allows the UI to read all 
 * 'X-RateLimit-*' and 'Retry-After' headers automatically.
 * 
 * If the Frontend is moved to a different domain (Cross-Origin), 
 * CORS must be enabled and headers MUST be explicitly exposed:
 * 
 * policy.WithExposedHeaders(
 *    "X-RateLimit-Limit", 
 *    "X-RateLimit-Remaining",
 *    "X-RateLimit-Reset",
 *    "X-RateLimit-Scope",
 *    "Retry-After");
 */
// app.UseCors(); // Not required for same-origin deployments

app.UseRequestTimeouts();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.UseMiddleware<RateLimitHeadersMiddleware>() // Add rate limit headers to successful requests for UI frameworks
    .UseMiddleware<AuditLogMiddleware>() // Audit log middleware should be as early as possible to capture all necessary information.
    .UseMiddleware<TransactionMiddleware>();

var apiGroup = app.MapGroup("api");

var v1Group = apiGroup.MapGroup("v{version:apiVersion}");

v1Group.AddEndpointFilter<AntiforgeryFilter>()  
    .AddEndpointFilter<IdempotencyFilter>()   
    .MapCustomIdentityApiV1<ApplicationUser>()   
    .WithApiVersionSet(app.NewApiVersionSet().HasApiVersion(new ApiVersion(1, 0)).Build())
    .RequireRateLimiting(Constants.UserRateLimitPolicyName);

app.MapControllers()
    .RequireRateLimiting(Constants.UserRateLimitPolicyName);

string[] summaries = ["Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"];
v1Group.MapGet("/weather-forecasts", (TimeProvider timeProvider) =>
{
    var dateTime = timeProvider.GetUtcNow().DateTime;

    var forecast = Enumerable
        .Range(1, 5)
        .Select(index => new WeatherForecastSyncItem
        (
            Guid.NewGuid(),
            // Now loops cleanly over the static local variable tracking snapshot
            DateOnly.FromDateTime(dateTime.AddDays(index)),
            summaries[Random.Shared.Next(summaries.Length)],
            Random.Shared.Next(-20, 55),
            Guid.NewGuid().ToString(),
            false
        ))
        .ToArray();

    return forecast;
})
.WithName("GetWeatherForecast")
.WithTags("WeatherForecasts");


app.Run();