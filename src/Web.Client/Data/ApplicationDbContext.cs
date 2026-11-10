using Domain.Shared;
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

        modelBuilder.Entity<Document>(b =>
        {
            b.Property(d => d.Name)
           .HasMaxLength(Domain.Shared.Constants.StringMaxLength)
           .HasAnnotation("Relational:Collation", "NOCASE")
           .IsRequired();

            // Include IsDeleted in the unique index so you can have 
            // two files with the same name if one is soft-deleted.
            b.HasIndex(d => new { d.Name, d.IsDeleted }).IsUnique();

            b.HasOne(d => d.DocumentCategory)
            .WithMany()
            .HasForeignKey(d => d.DocumentCategoryId)
            .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DocumentCategory>(b =>
        {
            b.Property(dc => dc.Name)
            .HasMaxLength(Domain.Shared.Constants.StringMaxLength)
            .HasAnnotation("Relational:Collation", "NOCASE")
            .IsRequired();

            b.Property(dc => dc.Description)
            .HasMaxLength(Domain.Shared.Constants.StringMaxLength);

            b.HasIndex(dc => new { dc.Name, dc.IsDeleted }).IsUnique();
        });

        modelBuilder.Entity<Ticket>(b =>
        {
            // Force 1-byte storage to shrink row size and save browser RAM
            b.Property(t => t.Priority).HasConversion<byte>();
            b.Property(t => t.Status).HasConversion<byte>();

            // Single Index on Priority ONLY (Covers searches where ONLY Priority is picked)
            b.HasIndex(t => t.Priority);

            // Composite Index (Covers BOTH selected, and covers searches where ONLY Status is picked)
            b.HasIndex(t => new { t.Status, t.Priority });
        });

        modelBuilder.Entity<SyncMetadata>(b =>
        {
            // Enforce one sync record per user, per entity type
            // This handles normal users (UserId is not null)
            b.HasIndex(s => new { s.UserId, s.EntityTypeName })
             .IsUnique()
             .HasFilter("[UserId] IS NOT NULL");

            // Enforce a single admin record (UserId is null) per entity type
            b.HasIndex(s => s.EntityTypeName)
             .IsUnique()
             .HasFilter("[UserId] IS NULL");
        });

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

            entityBuilder.Property(nameof(ISyncableEntity.ConcurrencyStamp))
                .IsConcurrencyToken()
                .HasMaxLength(Domain.Shared.Constants.StringMaxLength);
        }
    }
}
