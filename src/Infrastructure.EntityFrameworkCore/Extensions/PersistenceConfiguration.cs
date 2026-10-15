using Domain;
using Domain.BackgroundJobs;
using Infrastructure.EntityFrameworkCore.Interceptors;
using Infrastructure.EntityFrameworkCore.Repositories;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Infrastructure.EntityFrameworkCore.Extensions;

public static class PersistenceConfiguration
{
    extension(IHostApplicationBuilder builder)
    {
        public IHostApplicationBuilder AddEntityFrameworkServices(string connectionName = "DefaultConnection")
        {
            builder.Services.AddScoped<IAuditLogManager, AuditLogManager>();
            builder.Services.AddScoped<CustomSaveChangesInterceptor>();
            builder.Services.AddScoped<BatchDeleteDbCommandInterceptor>();

            // ARCHITECTURAL DECISION: Manual Scoped Registration Over Aspire DbContext Pooling (builder.AddNpgsqlDbContext<ApplicationDbContext>().
            //
            // 1. Interceptor Lifecycle & Scoping: Our CustomSaveChangesInterceptor relies on scoped dependencies 
            //    (e.g., pulling user identity or current audit state from the active HTTP/Task request scope). 
            //
            // 2. The Pooling Limitation: By default, Aspire's builder.AddNpgsqlDbContext activates DbContext Pooling. 
            //    Pooling instantiates and freezes the DbContext configuration (including interceptors) upfront at the 
            //    root/singleton level. This causes scoped interceptors to either fail during DI validation, or worse, 
            //    become captured as singletons—silently leaking thread state and user data across parallel requests.
            //
            // 3. Resolving via Service Provider: Registering natively via standard AddDbContext allows us to pass 
            //    the active Service Provider (sp) directly to .AddInterceptors(). This guarantees that every request 
            //    receives a pristine, isolated context instance bound to its own unique scoped interceptor lifecycle.

            builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
            {
                var configuration = builder.Configuration;

                options.Configure(configuration, connectionName);
                options.AddInterceptors(sp.GetRequiredService<CustomSaveChangesInterceptor>());
                options.AddInterceptors(sp.GetRequiredService<BatchDeleteDbCommandInterceptor>());
            });

            // Instruct Aspire to append telemetry, health checks, and logging to custom context
            builder.EnrichNpgsqlDbContext<ApplicationDbContext>(configureSettings: settings =>
            {
                // ARCHITECTURAL DECISION: Disable Aspire's automatic EnableRetryOnFailure strategy.
                // 
                // 1. Transaction Complications: EF Core execution strategies do not natively support user-initiated
                //    transaction boundaries (BeginTransactionAsync). Forcing retries requires complex relational extension 
                //    wrappers (ExecuteInTransactionAsync) which obscure basic command flows and transaction lifecycles.
                //
                // 2. State & Tracker Pollution: When SaveChangesAsync fails mid-retry, entities remain marked as 'Modified' 
                //    in the internal Change Tracker with stale concurrency stamps. Subsequent retry loops apply incremental 
                //    mutations on top of corrupted state, resulting in unavoidable, permanent DbUpdateConcurrencyExceptions.
                //
                // 3. Simple Self-Healing: For chunked background tasks, allowing transient network blips to simply bubble 
                //    up and fail the current iteration is vastly superior. The database rolls back cleanly, local metrics 
                //    stay synchronized, and the remaining queue safely processes during the next scheduled run or manual trigger.

                settings.DisableRetry = true;
            });
           
            builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
            builder.Services.AddScoped<IBackgroundJobRepository, BackgroundJobRepository>();
            builder.Services.AddScoped<IBackgroundServiceStatRepository, BackgroundServiceStatRepository>();
            builder.Services.AddScoped<IDocumentCategoryRepository, DocumentCategoryRepository>();
            builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();
            builder.Services.AddScoped<IDocumentVersionRepository, DocumentVersionRepository>();
            builder.Services.AddScoped<ITicketRepository, TicketRepository>();
            builder.Services.AddScoped<IUserRepository, UserRepository>();
            builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

            return builder;
        }
    }

    extension(IDataProtectionBuilder builder)
    {
        public IDataProtectionBuilder CustomPersistKeysToDbContext()
        {
            builder.PersistKeysToDbContext<ApplicationDbContext>();

            return builder;
        }
    }

    extension(IdentityBuilder builder)
    {
        public IdentityBuilder AddCustomEntityFrameworkStores()
        {
            builder.AddEntityFrameworkStores<ApplicationDbContext>();

            return builder;
        }
    }
}
