namespace Domain.Shared.BackgroundJobs;

/// <summary>
/// Defines the processing priority for background jobs.
/// </summary>
/// <remarks>
/// IMPORTANT: Always append new members to the end of the list to maintain 
/// database compatibility and prevent value shifts for existing records.
/// </remarks>
public enum BackgroundJobPriority
{
    Low,
    Normal,
    High,
    Critical
}

