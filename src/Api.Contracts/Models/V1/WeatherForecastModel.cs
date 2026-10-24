using Domain.Shared;

namespace Api.Contracts.Models.V1;

public record WeatherForecastModel(
    Guid Id,
    DateOnly Date,
    string? Summary,
    int TemperatureC,
    string? ConcurrencyStamp,
    // Added to serve as a cloud deletion tombstone flag
    bool IsDeleted = false) : ISyncableModel
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

public record WeatherForecastSyncModel(
    List<WeatherForecastModel> Items,
    SyncBehavior Behavior = SyncBehavior.Default);
