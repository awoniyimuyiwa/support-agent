using Domain;
using Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Text.Json;

namespace Infrastructure.EntityFrameworkCore.Interceptors;

class CustomSaveChangesInterceptor(
    IAuditLogManager auditLogManager,
    TimeProvider timeProvider) : SaveChangesInterceptor
{
    readonly IAuditLogManager _auditLogManager = auditLogManager;
    readonly TimeProvider _timeProvider = timeProvider;

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        UpdateEntities(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        UpdateEntities(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void UpdateEntities(DbContext? context)
    {
        if (context == null) return;

        var utcNow = _timeProvider.GetUtcNow();

        // To prevent "Collection modified during enumeration" errors when we change tracking states,
        // we take a quick static list copy of the tracker contents.
        foreach (var entry in context.ChangeTracker.Entries().ToList())
        {
            // Check if the entity implements ISoftDeleted and a deletion was explicitly requested via EF Core
            if (entry.Entity is ISoftDeleted softDeleteEntity && entry.State == EntityState.Deleted)
            {
                // Intercept and mutate the EF Core state machine from a physical purge to a safe Update
                entry.State = EntityState.Modified;
                softDeleteEntity.IsDeleted = true;

                // Mark the column specifically as modified so EF Core includes it in the UPDATE query statement
                var isDeletedProperty = entry.Property(nameof(ISoftDeleted.IsDeleted));
                isDeletedProperty.IsModified = true;
            }

            // Compute hasMeaningfulChange AFTER the soft-delete state mutation occurs
            bool hasMeaningfulChange = entry.State == EntityState.Added
               || entry.Properties.Any(p => p.IsModified);

            // Handle ICreationAuditedEntity (Only contains CreatedAt)
            if (entry.Entity is ICreationAuditedEntity creationAuditedEntity)
            {
                if (entry.State == EntityState.Added)
                {
                    if (creationAuditedEntity.CreatedAt == default)
                    {
                        creationAuditedEntity.CreatedAt = utcNow;
                    }
                }
            }

            // Handle IAuditedEntity (Contains both CreatedAt and UpdatedAt)
            if (entry.Entity is IAuditedEntity auditedEntity)
            {
                if (entry.State == EntityState.Added)
                {
                    if (auditedEntity.CreatedAt == default)
                    {
                        auditedEntity.CreatedAt = utcNow;
                    }
                    auditedEntity.UpdatedAt = utcNow;
                }
                else if (entry.State == EntityState.Modified && hasMeaningfulChange)
                {
                    auditedEntity.UpdatedAt = utcNow;

                    var updatedAtProperty = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "UpdatedAt");
                    if (updatedAtProperty != null)
                    {
                        updatedAtProperty.CurrentValue = auditedEntity.UpdatedAt;
                        updatedAtProperty.IsModified = true;
                    }
                }
            }

            // Handle Concurrency Stamps
            if (entry.Entity is IHasConcurrencyStamp hasConcurrencyStamp)
            {
                if (entry.State == EntityState.Added)
                {
                    hasConcurrencyStamp.ConcurrencyStamp = Guid.NewGuid().ToString();
                }
                else if (entry.State == EntityState.Modified && hasMeaningfulChange)
                {
                    // If a tracked property exists, override the OriginalValue with value from user input so EF core includes it in the WHERE clause
                    var concurrencyStampProperty = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "ConcurrencyStamp");

                    concurrencyStampProperty?.OriginalValue = concurrencyStampProperty.CurrentValue;
                    hasConcurrencyStamp.ConcurrencyStamp = Guid.NewGuid().ToString();
                    // If a tracked property exists, ensure its current value is synced (not strictly required)
                    if (concurrencyStampProperty is not null)
                    {
                        concurrencyStampProperty.CurrentValue = hasConcurrencyStamp.ConcurrencyStamp;
                        concurrencyStampProperty.IsModified = true;
                    }
                }
            }

            // Handle Audit Logging with Correct Operator Precedence Boundaries
            bool isAuditedClass = entry.Entity is IAuditedEntity || entry.Entity is ICreationAuditedEntity;

            // Note: Because soft delete changes the state from Deleted to Modified above, 
            // we check the true database state tracking configuration here.
            bool isTargetState = entry.State == EntityState.Added
                || entry.State == EntityState.Modified
                || entry.State == EntityState.Deleted;

            if (isAuditedClass && isTargetState)
            {
                long entityId = entry.State == EntityState.Deleted
                    ? (long)entry.Property("Id").OriginalValue!
                    : (long)entry.Property("Id").CurrentValue!;

                // Determine if this is an explicit hard delete or an intercepted soft delete event
                bool isSoftDeleteEvent = entry.Entity is ISoftDeleted sd && sd.IsDeleted && entry.State == EntityState.Modified;

                _auditLogManager.Current?.EntityChanges.Add(new EntityChange
                {
                    EntityId = entityId,
                    EntityName = entry.Entity.GetType().Name,

                    // If it's a hard delete or an intercepted soft delete, serialize all initial properties
                    OldValues = SerializeProperties(entry, isOld: true, isSoftDelete: isSoftDeleteEvent)!,
                    NewValues = entry.State == EntityState.Deleted ? null : SerializeProperties(entry, isOld: false, isSoftDelete: isSoftDeleteEvent),

                    Type = entry.State switch
                    {
                        EntityState.Added => EntityChangeType.Created,
                        EntityState.Deleted => EntityChangeType.Deleted,
                        EntityState.Modified => isSoftDeleteEvent ? EntityChangeType.Deleted : EntityChangeType.Updated,
                        _ => throw new NotSupportedException($"Unsupported entity state: {entry.State}")
                    }
                });
            }
        }
    }

    private static string? SerializeProperties(
        EntityEntry entry,
        bool isOld, 
        bool isSoftDelete)
    {
        var properties = entry.Properties
            .Where(p =>
            {
                // If this is a hard delete or an intercepted soft delete, log all existing properties.
                // Otherwise, only capture values that actually changed during standard updates.
                return entry.State == EntityState.Added
                       || entry.State == EntityState.Deleted
                       || isSoftDelete
                       || p.IsModified;
            })
            .ToDictionary(
            p => p.Metadata.Name,
            p =>
            {
                if (p.Metadata.ClrType == typeof(byte[])
                    || p.Metadata.ClrType == typeof(Stream))
                {
                    return Constants.BinaryDataMask;
                }

                var propertyInfo = p.Metadata.PropertyInfo;
                if (Constants.SensitiveFieldNames.Contains(p.Metadata.Name)
                    || (propertyInfo != null && Attribute.IsDefined(propertyInfo, typeof(NotAuditedAttribute))))
                {
                    return Constants.Mask;
                }

                // Safe fallback reading: For hard deletions, always fall back to reading OriginalValue
                var val = (isOld || entry.State == EntityState.Deleted) ? p.OriginalValue : p.CurrentValue;

                if (val is string s && s.Length > Constants.StringMaxLength)
                    return string.Concat(
                        s.AsSpan(0, Constants.StringMaxLength - Constants.TruncationIndicator.Length),
                        Constants.TruncationIndicator);

                return val;
            });

        return properties.Count != 0
            ? JsonSerializer.Serialize(properties) : null;
    }

}

