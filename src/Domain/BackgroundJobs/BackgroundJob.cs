using Domain.Shared.BackgroundJobs;

namespace Domain.BackgroundJobs;

public class BackgroundJob : AuditedEntity, IHasConcurrencyStamp
{
    public uint Attempts { get; set; }

    public string? Error { get; set; }

    public uint MaxRetries { get; set; }

    public required string Payload { get; set; }

    public BackgroundJobPriority Priority { get; set; }

    public DateTimeOffset ScheduledFor { get; set; }

    public Guid? SourceId { get; set; }

    public BackgroundJobStatus Status { get; set; }

    public string? TraceId { get; set; }

    public BackgroundJobType Type { get; set; }

    public string? ConcurrencyStamp { get; set; }   
}

