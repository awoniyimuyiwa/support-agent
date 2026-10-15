using Domain;
using Infrastructure.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infrastructure.EntityFrameworkCore;

class DbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        // Credentials (Connection String, Admin credentials for seeding) 
        // must be in User Secrets, not appsettings.json. 
        // This ensures EF tools only access what is necessary for schema/seed management.
        var configuration = new ConfigurationBuilder()
          .AddUserSecrets<DbContextFactory>()
          .Build();

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole());

        return CreateDbContext(
            configuration, 
            services);
    }

    public static ApplicationDbContext CreateDbContext(
        IConfiguration configuration,
        IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(configuration);

        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
        }).AddRoles<ApplicationRole>()
        .AddEntityFrameworkStores<ApplicationDbContext>();

        services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.Configure(configuration, "DefaultConnection");

            
            options.UseApplicationServiceProvider(sp);
        });

        var serviceProvider = services.BuildServiceProvider();

        return serviceProvider.GetRequiredService<ApplicationDbContext>();
    }
}
