using Azure.Security.KeyVault.Keys;
using AzureKeyVaultEmulator.Aspire.Hosting;
using Microsoft.Extensions.Hosting;
using System.Text;

var builder = DistributedApplication.CreateBuilder(args);

var isDev = builder.Environment.IsDevelopment();

// Enforces local persistence and port mapping ONLY during local development sessions
var kvOptions = isDev
    ? new KeyVaultEmulatorOptions { Persist = true, Port = 4997 }
    : new KeyVaultEmulatorOptions { Persist = false };

var keyVault = builder.AddAzureKeyVault("KeyVault")
    .RunAsEmulator(kvOptions)
    .SeedWithKey("data-protection-key", KeyType.Rsa);

var appConfig = builder.AddAzureAppConfiguration("AppConfig")
    .RunAsEmulator(emulator =>
    {
        if (isDev)
        {
            emulator.WithAnnotation(new ContainerLifetimeAnnotation
            {
                Lifetime = ContainerLifetime.Persistent
            }).WithDataVolume();
        }

        // Maintain consistent security signatures across both testing and local frameworks
        emulator.WithEnvironment("Tenant:HmacSha256Enabled", "true")
        .WithEnvironment("Tenant:AccessKeys:0:Id", "anonymous")
        .WithEnvironment("Tenant:AccessKeys:0:Secret", "abcdefghijklmnopqrstuvwxyz1234567890")
        .WithEnvironment("Tenant:AnonymousAuthEnabled", "true")
        .WithEnvironment("Authentication:Anonymous:AnonymousUserRole", "Owner");
    });

var cache = builder.AddRedis("Redis");
if (isDev)
{
    cache.WithDataVolume()
         .WithRedisInsight();
}

var postgres = builder.AddPostgres("postgres")
    .WithImage("pgvector/pgvector")
    .WithImageTag("pg17");
if (isDev)
{
    postgres.WithDataVolume()    
        .WithPgAdmin();
}

var supportDb = postgres.AddDatabase(
    "DefaultConnection",
    databaseName: "SupportAgent");

var dbMigrator = builder.AddProject<Projects.DbMigrator>("db-migrator")
    .WithReference(supportDb)
    .WaitFor(supportDb)
    // Injects safe, non-sensitive fallback credential values
    // If secrets.json exists (locally), .NET configuration providers will overwrite these automatically.
    .WithEnvironment("AdminUser__Email", "ci-admin@supportagent.internal")
    .WithEnvironment("AdminUser__UserName", "admin")
    .WithEnvironment("AdminUser__Password", "Pass@word123!");

var apiService = builder.AddProject<Projects.Api>("api")
    .WithHttpHealthCheck("/health")
    .WithReference(keyVault)
    .WithAzureKeyVaultEmulatorCredentials(keyVault)
    .WaitFor(keyVault)
    .WithReference(appConfig)
    .WaitFor(appConfig)
    .WithReference(supportDb)
    .WaitForCompletion(dbMigrator)
    .WithReference(cache)
    .WaitFor(cache)
    .WithEnvironment("DataProtectionOptions__KeyIdentifier",
        ReferenceExpression.Create($"{keyVault.GetEndpoint("https")}/keys/data-protection-key"))
    .WithEnvironment("DataProtectionOptions__LocalFallBack", "");

builder.AddProject<Projects.Web>("web")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(keyVault)
    .WithAzureKeyVaultEmulatorCredentials(keyVault)
    .WithReference(appConfig)
    .WithReference(cache)
    .WithReference(apiService)
    .WaitFor(apiService)
    .WithEnvironment("DataProtectionOptions__KeyIdentifier",
        ReferenceExpression.Create($"{keyVault.GetEndpoint("https")}/keys/data-protection-key"))
    .WithEnvironment("DataProtectionOptions__LocalFallBack", "");

builder.Build().Run();
