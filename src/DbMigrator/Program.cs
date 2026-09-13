using Infrastructure.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ServiceDefaults;

var builder = Host.CreateApplicationBuilder(args);

// Call Aspire template defaults method to wire up OpenTelemetry/Logging extensions natively!
builder.AddServiceDefaults();

var host = builder.Build();

// Create a distinct container collection for the factory block
var services = new ServiceCollection();

var loggerFactory = host.Services.GetRequiredService<ILoggerFactory>();
services.AddSingleton(loggerFactory);

using (var context = DbContextFactory.CreateDbContext(builder.Configuration, services))
{
    // Applies database updates, which automatically triggers your UseAsyncSeeding block natively
    // and streams all raw SQL migration logging directly to the Aspire Dashboard terminal window!
    await context.Database.MigrateAsync();
}

// Give OpenTelemetry a moment to flush buffers to the Aspire dashboard UI before killing the thread
await Task.Delay(2000);

// Signal exit code 0 to Aspire to unlock your API projects
await host.StopAsync();
