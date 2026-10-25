using Domain.Shared;

namespace Api.Contracts.Models.V1;

public record WeatherForecastSyncRequest(
    List<WeatherForecastSyncItem> Items,
    SyncBehavior Behavior = SyncBehavior.Default);

public record WeatherForecastSyncItem(
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