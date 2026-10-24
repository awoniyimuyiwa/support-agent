using Domain;
using Domain.BackgroundJobs;
using Domain.Shared;
using Domain.Shared.BackgroundJobs;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
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
        // Enable the pgvector database engine extension natively in PostgreSQL
        modelBuilder.HasPostgresExtension("vector");

        // Mandated to recognize and compile 'citext' data types natively, needed to make name fields case insensitive
        // _context.Documents.FirstOrDefault(d => d.Name == inputName) will automatically generate case -insensitive SQL matching, eliminating the need to write .ToLower() everywhere.
        modelBuilder.HasPostgresExtension("citext");

        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ApplicationUser>(applicationUser =>
        {
            applicationUser.Property(au => au.FirstName).HasMaxLength(Constants.StringMaxLength);
            applicationUser.Property(au => au.LastName).HasMaxLength(Constants.StringMaxLength);
            applicationUser.Property(au => au.ConcurrencyStamp).IsConcurrencyToken().HasMaxLength(Constants.StringMaxLength);
            applicationUser.Property(au => au.Id).ValueGeneratedOnAdd();
            applicationUser.HasIndex(au => au.ExternalId).IsUnique();         
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

            auditLog.HasOne(al => al.User)
            .WithMany()
            .HasForeignKey(al => al.UserId)
            .IsRequired(false);

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

            // Enforces that name must be globally unique (case insensitive).
            // Partial index filtering (e.g., ignoring soft-deleted rows) is intentionally omitted. 
            // This prevents name duplication between active and soft-deleted records.
            document.Property(d => d.Name)
            .HasColumnType("citext") //  Use the case-insensitive text data type
            .HasMaxLength(Constants.StringMaxLength)
            .IsRequired();

            document.HasIndex(d => d.Name)
            .IsUnique();

            // Crucial for Multi-Agent routing: Speeds up filtering documents by category 
            // and soft-delete status simultaneously before traversing child vector trees.
            document.HasIndex(d => d.DocumentCategoryId)
            .HasDatabaseName("IX_Documents_DocumentCategoryId_Active")
            .HasFilter("\"IsDeleted\" = false");
        });

        modelBuilder.Entity<DocumentCategory>(documentCategory =>
        {
            documentCategory.Property(dc => dc.Description).HasMaxLength(Constants.StringMaxLength);

            documentCategory.Property(dc => dc.Name)
           .HasColumnType("citext")
           .HasMaxLength(Constants.StringMaxLength)
           .IsRequired();

            documentCategory.HasIndex(dc => dc.Name)
            .IsUnique();
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
         
            documentChunk.HasOne(dc => dc.DocumentVersion)      
            .WithMany("DocumentChunks")                     
            .HasForeignKey(dc => dc.DocumentVersionId)
            .IsRequired();

            documentChunk.HasIndex(dc => new { dc.DocumentVersionId, dc.Index })
            .IsUnique();
        });

        modelBuilder.Entity<DocumentVersion>(documentVersion =>
        {
            documentVersion.Ignore(dv => dv.Chunks);

            // If hashing utility outputs a standard hexadecimal string (e.g., using Convert.ToHexString or BitConverter), it will always be exactly 64 characters long
            documentVersion.Property(dv => dv.ContentHash)
            .HasColumnType("char(64)") // Forces PostgreSQL to use fixed-length char optimization
            .IsFixedLength()
            .IsRequired()
            .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);

            documentVersion.Property(dv => dv.CreatedAt)
            .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);

            documentVersion.Property(dv => dv.DocumentId)
           .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);

            documentVersion.Property(dv => dv.FileType)
            .HasMaxLength(Constants.StringMaxLength)
            .IsRequired()
            .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);

            documentVersion.Property(dv => dv.FileId)
             .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);

            documentVersion.HasIndex(dv => dv.FileId)
            .IsUnique();

            // Optimizes the subquery computing the latest active version via Max(CreatedAt).
            // Not unique to avoid collision when multiple versions are synced at the exact same time.
            // Sorted descending to give the database instant O(1) access to the maximum timestamp leaf.
            documentVersion.HasIndex(dv => new { dv.DocumentId, dv.CreatedAt })    
            .IsDescending(false, true); // (DocumentId ASC, CreatedAt DESC)

            documentVersion.HasIndex(dv => new { dv.DocumentId, dv.ContentHash })
            .IsUnique();
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

            ticket.HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .IsRequired();
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

            var propertyAccess = Expression.Property(parameter, nameof(ISoftDeleted.IsDeleted));

            // Negate the value: !e.IsDeleted
            Expression filterExpression = Expression.Not(propertyAccess);

            if (entityType.ClrType == typeof(DocumentVersion))
            {
                var documentNavigation = Expression.Property(parameter, nameof(DocumentVersion.Document));
                var parentIsDeletedAccess = Expression.Property(documentNavigation, nameof(ISoftDeleted.IsDeleted));
                var parentNotDeleted = Expression.Not(parentIsDeletedAccess);

                filterExpression = Expression.AndAlso(filterExpression, parentNotDeleted);

                // Register the filter for DocumentChunk
                var chunkParam = Expression.Parameter(typeof(DocumentChunk), "dc");
                var chunkVersionNav = Expression.Property(chunkParam, nameof(DocumentChunk.DocumentVersion));
                var chunkDocNav = Expression.Property(chunkVersionNav, nameof(DocumentVersion.Document));
                var versionDeletedCheck = Expression.Not(Expression.Property(chunkVersionNav, nameof(ISoftDeleted.IsDeleted)));
                var docDeletedCheck = Expression.Not(Expression.Property(chunkDocNav, nameof(ISoftDeleted.IsDeleted)));
                var chunkFilterExpr = Expression.AndAlso(versionDeletedCheck, docDeletedCheck);
                var chunkLambda = Expression.Lambda(chunkFilterExpr, chunkParam);
                modelBuilder.Entity<DocumentChunk>().HasQueryFilter(chunkLambda);
            }

            var lambda = Expression.Lambda(filterExpression, parameter);

            modelBuilder.Entity(entityType.ClrType)
                .HasQueryFilter(lambda);
        }
    }
}