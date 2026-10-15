using Domain;
using Domain.Shared;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infrastructure.EntityFrameworkCore;

static class DbInitializer
{
    public static async Task Seed(
        DbContext applicationDbContext,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        // EF Core 9+ manages the transaction automatically during migrations and seeding.

        // Dig into the context options to find the true root application container
        var options = applicationDbContext.GetService<IDbContextOptions>();
        var coreExtension = options.FindExtension<CoreOptionsExtension>();

        var rootProvider = coreExtension?.ApplicationServiceProvider;

        // Safety Check: If the root container is missing (e.g., inside an unconfigured test setup)
        if (rootProvider is null)
        {
            var internalProvider = applicationDbContext.GetService<IServiceProvider>();
            var logger = internalProvider.GetService<ILoggerFactory>()?.CreateLogger("DatabaseSeeding");
            logger?.LogWarning("Application service provider not found. Skipping database seeding logic.");
            return;
        }

        var configuration = rootProvider.GetRequiredService<IConfiguration>();
        var userManager = rootProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = rootProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var timeProvider = rootProvider.GetRequiredService<TimeProvider>();

        await SeedAdminUser(
            configuration,
            userManager,
            roleManager,
            timeProvider);
    }

    private static async Task SeedAdminUser(
        IConfiguration configuration,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        TimeProvider timeProvider)
    {
        var adminEmail = configuration["AdminUser:Email"];
        var adminUserName = configuration["AdminUser:UserName"];
        var adminPassword = configuration["AdminUser:Password"];
        var adminRole = Constants.AdminRoleName;

        if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
        {
            var roleExists = roleManager.Roles.Any(r => r.Name == adminRole);
            if (!roleExists)
            {
                await roleManager.CreateAsync(new ApplicationRole
                {
                    ExternalId = Guid.NewGuid(),
                    Name = adminRole,
                    NormalizedName = adminRole.ToUpperInvariant()
                });
            }

            var existing = await userManager.FindByEmailAsync(adminEmail);
            if (existing is null)
            {
                var now = timeProvider.GetUtcNow();
                var adminUser = new ApplicationUser
                {
                    ExternalId = Guid.NewGuid(),
                    UserName = adminUserName,
                    NormalizedUserName = adminUserName!.ToUpperInvariant(),
                    Email = adminEmail,
                    NormalizedEmail = adminEmail.ToUpperInvariant(),
                    EmailConfirmed = true,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                var result = await userManager.CreateAsync(adminUser, adminPassword);
                if (result.Succeeded)
                {
                   await userManager.AddToRoleAsync(adminUser, adminRole);
                }
            }
            else
            {
                var isInRole = await userManager.IsInRoleAsync(existing, adminRole);
                if (!isInRole)
                {
                    await userManager.AddToRoleAsync(existing, adminRole);
                }
            }
        }
    }
}