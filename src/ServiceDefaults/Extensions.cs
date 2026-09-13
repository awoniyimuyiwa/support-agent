using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace ServiceDefaults;

// Adds common Aspire services: service discovery, resilience, health checks, and OpenTelemetry.
// This project should be referenced by each service project in your solution.
// To learn more about using this project, see https://aka.ms/dotnet/aspire/service-defaults
public static class Extensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureOpenTelemetry();

        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Turn on resilience by default
            http.AddStandardResilienceHandler();

            // Turn on service discovery by default
            http.AddServiceDiscovery();
        });

        // Uncomment the following to restrict the allowed schemes for service discovery.
        // builder.Services.Configure<ServiceDiscoveryOptions>(options =>
        // {
        //     options.AllowedSchemes = ["https"];
        // });

        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        // Extract the custom AppName or fallback to standard Assembly environment names
        var appName = builder.Configuration["AppName"]
                      ?? builder.Environment.ApplicationName;

        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
            logging.ParseStateValues = true; // Makes log placeholders searchable in Jaeger/Aspire
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
            })
            .WithTracing(tracing =>
            {
                tracing.AddSource(appName)
                    .AddAspNetCoreInstrumentation(tracing =>
                        // Exclude health check requests from tracing
                        tracing.Filter = context =>
                            !context.Request.Path.StartsWithSegments(HealthEndpointPath)
                            && !context.Request.Path.StartsWithSegments(AlivenessEndpointPath)
                    )
                    // =========================================================================
                    // NOTE ON DATABASE & REDIS INSTRUMENTATION (e.g., .AddRedisInstrumentation)
                    // =========================================================================
                    // DO NOT add explicit SQL Server, PostgreSQL, or Redis tracing configurations here.
                    // When you utilize Aspire Client components in your service projects (for example, 
                    // calling 'builder.AddNpgsqlDbContext' or 'builder.AddRedisDistributedCache'), 
                    // Aspire AUTOMATICALLY injects and configures their respective database, query-level, 
                    // and verbose command tracking rules under the hood without cluttering this method.

                    // Uncomment the following line to enable gRPC instrumentation (requires the OpenTelemetry.Instrumentation.GrpcNetClient package)
                    //.AddGrpcClientInstrumentation()
                    .AddHttpClientInstrumentation();
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        // DEVELOPMENT ENVIRONMENT (MANAGED BY ASPIRE):
        // Aspire automatically injects 'OTEL_EXPORTER_OTLP_ENDPOINT' at runtime in development.
        // It points to the local Aspire Dashboard. You do NOT need to set this manually on your machine.
        // 
        // PRODUCTION ENVIRONMENT RESPONSIBILITY:
        // If your production environment uses a vendor-neutral OpenTelemetry backend (like Grafana, 
        // Jaeger, or an OTel Collector), YOU are responsible for setting 'OTEL_EXPORTER_OTLP_ENDPOINT' 
        // in your production environment variables along with secure authorization headers.

        // HOW TO SET THE AUTHORIZATION HEADER IN PRODUCTION:
        // You do not add authorization headers in C# code. Instead, you inject an additional environment 
        // variable on your production server. The OpenTelemetry SDK automatically reads it and appends it:
        // Name:  OTEL_EXPORTER_OTLP_HEADERS
        // Value: api-key=your_token  (OR  Authorization=Bearer your_token, depending on your OTel provider)
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        // PRODUCTION ENVIRONMENT (AZURE APPLICATION INSIGHTS alternative):
        // If you are deploying to Microsoft Azure, you do NOT need to use 'OTEL_EXPORTER_OTLP_ENDPOINT' 
        // in production. Instead, YOU are responsible for injecting 'APPLICATIONINSIGHTS_CONNECTION_STRING' 
        // into your cloud environment configuration. This block automatically swaps the pipeline to Azure.
        // 
        // Uncomment the following lines to enable the Azure Monitor exporter (requires the Azure.Monitor.OpenTelemetry.AspNetCore package)
        //if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
        //{
        //    builder.Services.AddOpenTelemetry()
        //       .UseAzureMonitor();
        //}

        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            // Add a default liveness check to ensure app is responsive
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        // Adding health checks endpoints to applications in non-development environments has security implications.
        // See https://aka.ms/dotnet/aspire/healthchecks for details before enabling these endpoints in non-development environments.
        if (app.Environment.IsDevelopment())
        {
            // All health checks must pass for app to be considered ready to accept traffic after starting
            app.MapHealthChecks(HealthEndpointPath);

            // Only health checks tagged with the "live" tag must pass for app to be considered alive
            app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
            {
                Predicate = r => r.Tags.Contains("live")
            });
        }

        return app;
    }
}
