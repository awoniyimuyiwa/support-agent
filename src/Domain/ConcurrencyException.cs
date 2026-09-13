namespace Domain;

public class ConcurrencyException : Exception
{
    public string EntityName { get; }
    public object EntityId { get; }
    public object? CurrentDatabaseValues { get; }

    public ConcurrencyException(
        string entityName, 
        object entityId, 
        object? dbValues = null)
        : base($"Concurrency conflict detected on {entityName} (ID: {entityId}).")
    {
        EntityName = entityName;
        EntityId = entityId;
        CurrentDatabaseValues = dbValues;
    }

    // Optional: Only if you want to see EF Core's exact save stack trace in your logger
    public ConcurrencyException(
        string entityName, 
        object entityId, 
        object? dbValues, 
        Exception innerException)
        : base($"Concurrency conflict detected on {entityName} (ID: {entityId}).", innerException)
    {
        EntityName = entityName;
        EntityId = entityId;
        CurrentDatabaseValues = dbValues;
    }
}

