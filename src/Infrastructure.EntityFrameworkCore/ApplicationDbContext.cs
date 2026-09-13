using Domain;
using Domain.BackgroundJobs;
using Domain.Shared;
using Domain.Shared.BackgroundJobs;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.EntityFrameworkCore;

class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser, ApplicationRole, long>(options), IDataProtectionKeyContext
{
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<AuditLogAction> AuditLogActions => Set<AuditLogAction>();

    public DbSet<BackgroundJob> BackgroundJobs => Set<BackgroundJob>();

    public DbSet<BackgroundServiceStat> BackgroundServiceStats => Set<BackgroundServiceStat>();

    public DbSet<Ticket> Tickets => Set<Ticket>();

    /// <summary>
    /// Stores encrypted keys for .NET data protection (e.g., cookies, tokens).
    /// </summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    public DbSet<EntityChange> EntityChanges => Set<EntityChange>();

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

            applicationUser.HasMany<AuditLog>()
            .WithOne(al => al.User)
            .HasForeignKey(al => al.UserId)
            .IsRequired();          
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
    }
}