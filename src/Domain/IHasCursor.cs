namespace Domain;

public interface IHasCursor
{
    public long Id { get; } 

    [NotAudited]
    public DateTimeOffset CreatedAt { get; set; }
}
