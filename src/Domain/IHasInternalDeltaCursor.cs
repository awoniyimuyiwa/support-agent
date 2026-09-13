namespace Domain;

public interface IHasInternalDeltaCursor
{
    long InternalId { get; }

    DateTimeOffset UpdatedAt { get; }
}
