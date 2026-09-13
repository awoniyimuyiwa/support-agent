using Domain.Shared;

namespace Api.Contracts.Models.V1;

public record BackgroundServiceStatModel(
    Guid Id,
    object? Details,
    DateTimeOffset LastRunAt,
    long Total,
    long TotalInLastRun,
    BackgroundServiceStatType Type);
