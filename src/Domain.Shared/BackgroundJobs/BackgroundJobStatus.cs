namespace Domain.Shared.BackgroundJobs;

/// <summary>
/// Represents the current lifecycle state of a background job.
/// </summary>
/// <remarks>
/// IMPORTANT: Append new members to the end of the list to maintain 
/// database compatibility and prevent value shifts for existing records.
/// </remarks>
public enum BackgroundJobStatus
{
    Queued,
    InProgress,
    Failed
}

