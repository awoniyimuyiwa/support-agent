namespace Web.Client.Data;

public class WeatherForecast : ISyncableEntity
{
    public Guid Id { get; set; }

    public DateOnly Date { get; set; }

    public int TemperatureC { get; set; }

    public string? Summary { get; set; }

    public string? ConcurrencyStamp { get; set; } = string.Empty;
    
    public SyncStatus SyncStatus { get; set; } = SyncStatus.Synced;

    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
