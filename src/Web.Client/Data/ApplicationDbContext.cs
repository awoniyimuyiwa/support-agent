using Microsoft.EntityFrameworkCore;

namespace Web.Client.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Document> Documents => Set<Document>();

    public DbSet<DocumentCategory> DocumentCategories => Set<DocumentCategory>();

    public DbSet<DocumentVersion> DocumentVersions => Set<DocumentVersion>();

    public DbSet<StoredFile> StoredFiles => Set<StoredFile>();

    public DbSet<SyncBatch> SyncBatches => Set<SyncBatch>();

    public DbSet<SyncMetadata> SyncMetadatas => Set<SyncMetadata>();

    public DbSet<Ticket> Tickets => Set<Ticket>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Document>()
           .HasOne(d => d.DocumentCategory)
           .WithMany()
           .HasForeignKey(d => d.DocumentCategoryId)
           .OnDelete(DeleteBehavior.Cascade);

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
