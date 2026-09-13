using Microsoft.EntityFrameworkCore;

namespace Web.Client.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<SyncBatch> SyncBatches => Set<SyncBatch>();

    public DbSet<SyncMetadata> SyncMetadatas => Set<SyncMetadata>();

    public DbSet<Ticket> Tickets => Set<Ticket>();

    public DbSet<WeatherForecast> WeatherForecasts => Set<WeatherForecast>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Map the tracking SyncStatus enum values as direct integers or bytes
        var syncableEntityTypes = modelBuilder.Model.GetEntityTypes()
            .Where(et => typeof(ISyncableEntity).IsAssignableFrom(et.ClrType))
            .Select(et => et.ClrType);


        foreach (var entityType in syncableEntityTypes)
        {
            var entityBuilder = modelBuilder.Entity(entityType);

            // Convert the SyncStatus enum to a single-byte numeric field inside SQLite
            entityBuilder.Property(nameof(ISyncableEntity.SyncStatus))
                .HasConversion<byte>();

            // Create a direct performance index on the status column for ultra-fast batch slicing
            entityBuilder.HasIndex(nameof(ISyncableEntity.SyncStatus));
        }
    }
}
