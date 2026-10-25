using Domain.Shared;

namespace Api.Contracts.Models.V1;

public record AuditLogResponse(
    Guid Id,
    string? BrowserInfo,
    double Duration,
    string? Exception,
    string? HttpMethod,
    string? IpAddress,
    string? RequestId,
    int StatusCode,
    DateTimeOffset Timestamp,
    string? Url,
    Guid? UserId);

public record AuditLogActionResponse(
    DateTimeOffset ExecutionTime,
    string MethodName,
    string? Parameters,
    string ServiceName);

public record EntityChangeResponse(
    string EntityName,
    string? NewValues,
    string? OldValues,
    EntityChangeType Type);

public record FullAuditLogResponse(
    Guid Id,
    string? BrowserInfo,
    double Duration,
    string? Exception,
    string? HttpMethod,
    string? IpAddress,
    string? RequestId,
    int StatusCode,
    DateTimeOffset Timestamp,
    string? Url,
    Guid? UserId,
    IReadOnlyCollection<AuditLogActionResponse> AuditLogActions,
    IReadOnlyCollection<EntityChangeResponse> EntityChanges)
    : AuditLogResponse(
        Id, 
        BrowserInfo, 
        Duration, 
        Exception, 
        HttpMethod, 
        IpAddress, 
        RequestId, 
        StatusCode, 
        Timestamp, 
        Url, 
        UserId);
