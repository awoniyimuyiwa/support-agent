namespace Api.Contracts.Models.V1;

public record UserSessionResponse(
    string Id,
    string? AuthScheme,
    string? CorrelationId,
    string DeviceInfo,
    string? IpAddress,
    DateTimeOffset LastSeen,
    string Location,
    DateTimeOffset LoginTime);
