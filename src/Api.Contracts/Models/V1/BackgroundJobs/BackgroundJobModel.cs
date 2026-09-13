using Domain.Shared.BackgroundJobs;

namespace Api.Contracts.Models.V1.BackgroundJobs;

public record BackgroundJobModel(
    Guid Id,
    uint Attempts,
    string? Error,
    uint MaxRetries,
    string? Payload,
    BackgroundJobPriority Priority,
    DateTimeOffset ScheduledFor,
    Guid? SourceId,
    BackgroundJobStatus Status,
    string? TraceId,
    BackgroundJobType Type,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    string? ConcurrencyStamp);
