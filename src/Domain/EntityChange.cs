using Domain.Shared;

namespace Domain;

public class EntityChange
{
    public long Id { get; protected set; } = default!;

    public long AuditLogId { get; set; } = default!;

    public string EntityName { get; set; } = default!;

    public long EntityId { get; set; } = default!;

    public string? NewValues { get; set; }

    public string? OldValues { get; set; }

    public EntityChangeType Type { get; set; } = default!;
}
