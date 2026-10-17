using Azure.Security.KeyVault.Keys;
using AzureKeyVaultEmulator.Aspire.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var isDev = builder.Environment.IsDevelopment();

// Enforces local persistence and port mapping ONLY during local development sessions
var kvOptions = isDev
    ? new KeyVaultEmulatorOptions { Persist = true, Port = 4997 }
    : new KeyVaultEmulatorOptions { Persist = false };

var keyVault = builder.AddAzureKeyVault("keyVault")
    .RunAsEmulator(kvOptions)
    .SeedWithKey("data-protection-key", KeyType.Rsa);

var appConfig = builder.AddAzureAppConfiguration("appConfig")
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

var storage = builder.AddAzureStorage("Storage")
    .RunAsEmulator(emulator =>
    {
        if (isDev)
        {
            emulator.WithDataVolume();
        }
    });
var blobStorage = storage.AddBlobs("blobs");

var aiConfig = builder.Configuration.GetSection("AiOptions");

var chatModelName = !string.IsNullOrWhiteSpace(aiConfig["ChatModelName"])
    ? aiConfig["ChatModelName"]!
    : throw new InvalidOperationException("AppHost Boot Error: 'AiOptions:ChatModelName' must be specified and cannot be empty or whitespace.");

var embeddingModelName = !string.IsNullOrWhiteSpace(aiConfig["EmbeddingModelName"])
    ? aiConfig["EmbeddingModelName"]!
    : throw new InvalidOperationException("AppHost Boot Error: 'AiOptions:EmbeddingModelName' must be specified and cannot be empty or whitespace.");

var ollama = builder.AddOllama("ollama");
if (isDev)
{
    ollama.WithDataVolume();
}
var embeddingModel = ollama.AddModel(embeddingModelName);
var llmModel = ollama.AddModel(chatModelName);

var cache = builder.AddRedis("redis");
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

// Reads secrets.json configuration layer first during local development.
// If running in GitHub Actions (CI) where secrets.json is missing, 
// it falls back to these safe, non-sensitive credential values automatically.
var adminEmail = builder.Configuration["AdminUser:Email"] ?? "admin@supportagent.test";
var adminUser = builder.Configuration["AdminUser:UserName"] ?? "admin";
var adminPass = builder.Configuration["AdminUser:Password"] ?? "Secret@123";

var dbMigrator = builder.AddProject<Projects.DbMigrator>("db-migrator")
    .WithReference(supportDb)
    .WaitFor(supportDb)
    .WithEnvironment("AdminUser__Email", adminEmail)
    .WithEnvironment("AdminUser__UserName", adminUser)
    .WithEnvironment("AdminUser__Password", adminPass   );

var apiService = builder.AddProject<Projects.Api>("api")
    .WithHttpHealthCheck("/health")
    .WithReference(keyVault)
    .WithAzureKeyVaultEmulatorCredentials(keyVault)
    .WaitFor(keyVault)
    .WithReference(appConfig)
    .WaitFor(appConfig)
    .WithReference(blobStorage)
    .WithReference(embeddingModel)
    .WithReference(llmModel)
    .WithReference(cache)
    .WaitFor(cache)
    .WithReference(supportDb)
    .WaitForCompletion(dbMigrator) 
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
