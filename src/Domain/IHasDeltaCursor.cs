namespace Domain;

public interface IHasDeltaCursor
{
    public long Id { get; }

    [NotAudited]
    public DateTimeOffset UpdatedAt { get; set; }
}
