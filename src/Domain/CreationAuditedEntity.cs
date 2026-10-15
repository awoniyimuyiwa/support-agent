namespace Domain;

public interface ICreationAuditedEntity : IHasCursor, ISoftDeleted
{

}

public abstract class CreationAuditedEntity : Entity, ICreationAuditedEntity
{
    [NotAudited]
    public DateTimeOffset CreatedAt { get; set; }

    public bool IsDeleted { get; set; } = false;
}