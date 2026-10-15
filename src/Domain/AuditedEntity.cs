namespace Domain;

public interface IAuditedEntity : ICreationAuditedEntity, IHasDeltaCursor
{
    
}

public abstract class AuditedEntity : Entity, IAuditedEntity
{
    [NotAudited]
    public DateTimeOffset CreatedAt { get; set; }

    [NotAudited]
    public DateTimeOffset UpdatedAt { get; set; }

    public bool IsDeleted { get; set; } = false;
}
