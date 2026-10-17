using Domain;
using Domain.BackgroundJobs;
using Domain.Shared;
using Domain.Shared.BackgroundJobs;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Pgvector;
using System.Linq.Expressions;

namespace Infrastructure.EntityFrameworkCore;

class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser, ApplicationRole, long>(options), IDataProtectionKeyContext
{
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<AuditLogAction> AuditLogActions => Set<AuditLogAction>();

    public DbSet<BackgroundJob> BackgroundJobs => Set<BackgroundJob>();

    public DbSet<BackgroundServiceStat> BackgroundServiceStats => Set<BackgroundServiceStat>();

    /// <summary>
    /// Stores encrypted keys for .NET data protection (e.g., cookies, tokens).
    /// </summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    public DbSet<Document> Documents => Set<Document>();

    public DbSet<DocumentCategory> DocumentCategories => Set<DocumentCategory>();

    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();

    public DbSet<DocumentVersion> DocumentVersions => Set<DocumentVersion>();
    
    public DbSet<EntityChange> EntityChanges => Set<EntityChange>();

    public DbSet<Ticket> Tickets => Set<Ticket>();

  
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Enable the pgvector database engine extension natively in PostgreSQL
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.Entity<ApplicationUser>(applicationUser =>
        {
            applicationUser.Property(au => au.FirstName).HasMaxLength(Constants.StringMaxLength);
            applicationUser.Property(au => au.LastName).HasMaxLength(Constants.StringMaxLength);
            applicationUser.Property(au => au.ConcurrencyStamp).IsConcurrencyToken().HasMaxLength(Constants.StringMaxLength);
            applicationUser.Property(au => au.Id).ValueGeneratedOnAdd();
            applicationUser.HasIndex(au => au.ExternalId).IsUnique();

            // Explicitly define relationship as optional 
            // to prevent INNER JOIN truncation caused by the global query filter.            
            // Marking this relationship as optional (.IsRequired(false)) forces EF Core 
            // to generate a LEFT JOIN instead of an INNER JOIN. This prevents global 
            // query filters on ApplicationUser from silently truncating AuditLog rows.
            //
            // NOTE ON PRODUCTION NULL DATA: 
            // The underlying foreign key constraint remains fully intact in PostgreSQL. 
            // As long as the application logic ensures a valid UserId is provided on 
            // every save, no actual NULL values will be written to the database.
            applicationUser.HasMany<AuditLog>()
            .WithOne(al => al.User)
            .HasForeignKey(al => al.UserId)
            .IsRequired(false);          
        });

        modelBuilder.Entity<ApplicationRole>(applicationRole =>
        {
            applicationRole.Property(ar => ar.Id).ValueGeneratedOnAdd();
            applicationRole.HasIndex(ar => ar.ExternalId)
            .IsUnique();
        });

        modelBuilder.Entity<AuditLogAction>(auditLogAction =>
        {
            auditLogAction.Property(ala => ala.MethodName).HasMaxLength(Constants.StringMaxLength);
            auditLogAction.Property(ala => ala.ServiceName).HasMaxLength(Constants.StringMaxLength);
        });

        modelBuilder.Entity<AuditLog>(auditLog =>
        {
            auditLog.Property(al => al.BrowserInfo).HasMaxLength(Constants.StringMaxLength);
            auditLog.Property(al => al.Exception).HasMaxLength(Constants.MaxExceptionLength);
            auditLog.Property(al => al.HttpMethod).HasMaxLength(Constants.StringMaxLength);
            auditLog.Property(al => al.IpAddress).HasMaxLength(45);
            auditLog.Property(al => al.RequestId).HasMaxLength(Constants.StringMaxLength);
            auditLog.Property(al => al.Url).HasMaxLength(Constants.StringMaxLength);

            auditLog.HasMany(al => al.AuditLogActions)
            .WithOne()
            .HasForeignKey(aa => aa.AuditLogId)
            .IsRequired();

            auditLog.HasMany(al => al.EntityChanges)
            .WithOne()
            .HasForeignKey(ec => ec.AuditLogId)
            .IsRequired();
        });

        modelBuilder.Entity<BackgroundJob>(backgroundJob =>
        {
            backgroundJob.Property(bj => bj.Error).HasMaxLength(Constants.MaxExceptionLength);
            backgroundJob.Property(bj => bj.SourceId).HasMaxLength(Constants.StringMaxLength);
            backgroundJob.Property(bj => bj.TraceId).HasMaxLength(Constants.StringMaxLength);

            // Worker Polling
            backgroundJob.HasIndex(bj => new { bj.Status, bj.Priority, bj.ScheduledFor })
              .HasFilter($"\"{nameof(BackgroundJob.Status)}\" = {(int)BackgroundJobStatus.Queued}");
          
            // Cleanup Task
            backgroundJob.HasIndex(bj => new { bj.Status, bj.UpdatedAt });

            // General Filtering
            backgroundJob.HasIndex(bj => bj.Type);
        });

        modelBuilder.Entity<BackgroundServiceStat>(backgroundServiceStat =>
        {
            backgroundServiceStat
            .HasIndex(bss => bss.Type)
            .IsUnique();
        });

        modelBuilder.Entity<Document>(document =>
        {
            document.Ignore(d => d.Versions);

            document.Property(d => d.Name).HasMaxLength(Constants.StringMaxLength).IsRequired();

            document.HasIndex(d => d.Name)
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");

            // Crucial for Multi-Agent routing: Speeds up filtering documents by category 
            // and soft-delete status simultaneously before traversing child vector trees.
            document.HasIndex(d => d.DocumentCategoryId)
            .HasDatabaseName("IX_Documents_DocumentCategoryId_Active")
            .HasFilter("\"IsDeleted\" = false");
        });

        modelBuilder.Entity<DocumentCategory>(documentCategory =>
        {
            documentCategory.Property(dc => dc.Name).HasMaxLength(Constants.StringMaxLength).IsRequired();

            documentCategory.Property(dc => dc.Description).HasMaxLength(Constants.StringMaxLength);

            documentCategory.HasIndex(dc => dc.Name)
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");
        });

        modelBuilder.Entity<DocumentChunk>(documentChunk =>
        {
            documentChunk.HasKey(dc => dc.Id);
            documentChunk.Property(dc => dc.Content).IsRequired();

            var vectorConverter = new ValueConverter<float[], Vector>(
                v => new Vector(v),
                v => v.ToArray());
          
            var vectorComparer = new ValueComparer<float[]>(
                (c1, c2) => c1 != null && c2 != null && c1.SequenceEqual(c2),
                c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                c => c.ToArray() 
            );
           
            // Configure the vector column to support the target size (e.g. 768 dimensions)
            documentChunk
            .Property(dc => dc.Vector)
            .HasColumnType("halfvec(768)") // <-- Maps to the 16-bit float vector type
            .HasConversion(vectorConverter, vectorComparer);

            // Set up Hierarchical Navigable Small World (Cosine search index natively)
            documentChunk
            .HasIndex(dc => dc.Vector)
            .HasMethod("hnsw")
            // Instructs PostgreSQL to build the HNSW graph using Cosine Distance (<=>) as the mathematical validation metric (other distance methods are straight flat grid line (Euclidean/L2), and absolute city grid turn count (Manhattan/L1))
            .HasOperators("halfvec_cosine_ops")
            // Production Tuning Options: Essential for high-dimensionaNSW) vectors (e.g., 1536 dimensions).
            // By increasing 'm' (default 16) and 'ef_construction' (default 64), we construct a denser multi-layer 
            // search graph. This ensures the AI Agent rarely misses highly relevant documentation chunks (near-perfect recall), 
            // sacrificing a tiny bit of background worker indexing time for ultimate runtime search accuracy.
            .HasStorageParameter("m", 24) // Max bidirectional link connections per graph node
            .HasStorageParameter("ef_construction", 100); // Size of the dynamic candidate list evaluated during index creation

            // Explicitly define relationship as optional 
            // to prevent INNER JOIN truncation caused by the global query filter.
            documentChunk.HasOne(dc => dc.DocumentVersion)      
            .WithMany("DocumentChunks")                     
            .HasForeignKey(dc => dc.DocumentVersionId)
            .IsRequired(false);
        });

        modelBuilder.Entity<DocumentVersion>(documentVersion =>
        {
            documentVersion.Ignore(dv => dv.Chunks);

            // If hashing utility outputs a standard hexadecimal string (e.g., using Convert.ToHexString or BitConverter), it will always be exactly 64 characters long
            documentVersion.Property(dv => dv.ContentHash)
            .HasColumnType("char(64)") // Forces PostgreSQL to use fixed-length char optimization
            .IsFixedLength()
            .IsRequired();

            documentVersion.Property(dv => dv.FileType)
            .HasMaxLength(Constants.StringMaxLength)
            .IsRequired();

            documentVersion.
            HasIndex(dv => dv.FileId)
            .IsUnique();

            documentVersion.HasIndex(dv => new { dv.DocumentId, dv.CreatedAt });
        });

        modelBuilder.Entity<EntityChange>(entityChange =>
        {
            entityChange.Property(ec => ec.EntityName).HasMaxLength(Constants.StringMaxLength).IsRequired();
        });

        modelBuilder.Entity<Ticket>(ticket =>
        {
            // Apply global query filter to exclude soft-deleted items automatically
            ticket.HasQueryFilter(t => !t.IsDeleted);

            // Restrict property string lengths to protect against overflow memory loads
            ticket.Property(t => t.Title).HasMaxLength(Constants.StringMaxLength).IsRequired();
            ticket.Property(t => t.Description).HasMaxLength(Constants.MaxExceptionLength).IsRequired();

            ticket
            .HasIndex(t => new { t.UpdatedAt, t.Id });
        });


        // Find all entity types that are a subclass of Entity
        var entityTypes = modelBuilder.Model.GetEntityTypes()
            .Where(e => typeof(Entity).IsAssignableFrom(e.ClrType) && !e.ClrType.IsAbstract);

        foreach (var entityType in entityTypes)
        {
            modelBuilder.Entity(entityType.ClrType)
                .Property("Id") // Use string if Id is defined in the base class
                .UseIdentityColumn();

            modelBuilder.Entity(entityType.ClrType)
                .HasIndex("ExternalId")
                .IsUnique();
        }

        // Find all entity types implementing cursor metadata
        var cursorMetadataTypes = modelBuilder.Model.GetEntityTypes()
            .Where(t => typeof(IHasCursor).IsAssignableFrom(t.ClrType) && !t.ClrType.IsAbstract);

        foreach (var entityType in cursorMetadataTypes)
        {
            // Create a composite index (CreatedAt, Id) for cursor-based pagination
            modelBuilder.Entity(entityType.ClrType)
                .HasIndex(nameof(IHasCursor.CreatedAt), "Id")
                .HasDatabaseName($"IX_{entityType.GetTableName()}_CursorPagination");
        }

        // Find all entity types implementing delta cursor metadata
        var deltaCursorMetadataTypes = modelBuilder.Model.GetEntityTypes()
            .Where(t => typeof(IHasDeltaCursor).IsAssignableFrom(t.ClrType) && !t.ClrType.IsAbstract);

        foreach (var entityType in deltaCursorMetadataTypes)
        {
            // Creates a composite index (UpdatedAt, Id) for cursor-based delta replication
            modelBuilder.Entity(entityType.ClrType)
                .HasIndex(nameof(IHasDeltaCursor.UpdatedAt), "Id")
                .HasDatabaseName($"IX_{entityType.GetTableName()}_DeltaCursorPagination");
        }

        // Find all entity types that implement concurrency stamp interface
        var concurrencyStampTypes = modelBuilder.Model.GetEntityTypes()
            .Where(e => typeof(IHasConcurrencyStamp).IsAssignableFrom(e.ClrType) && !e.ClrType.IsAbstract);

        foreach (var entityType in concurrencyStampTypes)
        {
            // Dynamically apply the concurrency token and string length constraints
            modelBuilder.Entity(entityType.ClrType)
                .Property("ConcurrencyStamp")
                .IsConcurrencyToken()
                .HasMaxLength(Constants.StringMaxLength);
        }

        // Find all entity types that implement the soft delete interface
        var softDeleteTypes = modelBuilder.Model.GetEntityTypes()
            .Where(e => typeof(ISoftDeleted).IsAssignableFrom(e.ClrType) && !e.ClrType.IsAbstract);

        foreach (var entityType in softDeleteTypes)
        {
            // Configure the default boolean column mapping layout
            modelBuilder.Entity(entityType.ClrType)
                .Property(nameof(ISoftDeleted.IsDeleted))
                .HasDefaultValue(false);

            // Dynamically construct the Expression Tree: e => !e.IsDeleted
            var parameter = Expression.Parameter(entityType.ClrType, "e");

            // Access the 'IsDeleted' property on the parameter: e.IsDeleted
            var propertyAccess = Expression.Property(parameter, nameof(ISoftDeleted.IsDeleted));

            // Negate the value: !e.IsDeleted
            var filterExpression = Expression.Not(propertyAccess);

            // Compile into a Lambda expression: Lambda<Func<TEntity, bool>>
            var lambda = Expression.Lambda(filterExpression, parameter);

            // Apply the global query filter dynamically to the entity configuration
            modelBuilder.Entity(entityType.ClrType)
                .HasQueryFilter(lambda);
        }
    }
}