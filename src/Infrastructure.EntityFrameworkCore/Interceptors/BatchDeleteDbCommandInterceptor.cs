using Domain;
using Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Infrastructure.EntityFrameworkCore.Interceptors;

public partial class BatchDeleteDbCommandInterceptor(
    IAuditLogManager auditLogManager, 
    TimeProvider timeProvider) : DbCommandInterceptor
{
    // The containing class MUST be marked 'partial' for source generators to attach the compiled regex math
    [GeneratedRegex(@"DELETE\s+FROM\s+""?([^""\s\)]+)""?", RegexOptions.IgnoreCase, "en-NG")]
    private static partial Regex DeleteTableRegex();

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        LogBatchDeleteIfAudited(command, eventData.Context);
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result)
    {
        LogBatchDeleteIfAudited(command, eventData.Context);
        return base.NonQueryExecuting(command, eventData, result);
    }

    private void LogBatchDeleteIfAudited(DbCommand command, DbContext? context)
    {
        if (context == null || auditLogManager.Current == null) return;

        if (!command.CommandText.StartsWith("DELETE FROM", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Call the static partial method. This runs code generated at compile-time with zero runtime string parsing overhead.
        var match = DeleteTableRegex().Match(command.CommandText);
        if (!match.Success) return;

        string tableName = match.Groups[1].Value;

        var entityType = context.Model.GetEntityTypes()
            .FirstOrDefault(e => e.GetTableName() == tableName || e.GetViewName() == tableName);

        if (entityType == null) return;

        bool isAuditedClass = typeof(IAuditedEntity).IsAssignableFrom(entityType.ClrType)              
            || typeof(ICreationAuditedEntity).IsAssignableFrom(entityType.ClrType);

        if (!isAuditedClass) return;

        var oldValuesBackup = new Dictionary<string, object>
        {
            { "Table", tableName },
            { "HardDeletedAt", timeProvider.GetUtcNow() },
            { "RawCommandText", command.CommandText },
            { "BatchOperation", true }
        };

        auditLogManager.Current.EntityChanges.Add(new EntityChange
        {
            EntityId = 0,
            EntityName = entityType.ClrType.Name,
            OldValues = JsonSerializer.Serialize(oldValuesBackup),
            NewValues = null,
            Type = EntityChangeType.Deleted
        });
    }
}
