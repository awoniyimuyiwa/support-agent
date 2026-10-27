namespace Domain.Shared.BackgroundJobs;

/// <summary>
/// Specifies the type of work to be performed by a background job.
/// </summary>
/// <remarks>
/// IMPORTANT: Always append new members to the end of the list to maintain 
/// database compatibility and prevent value shifts for existing records.
/// </remarks>
public enum BackgroundJobType
{
    Email,
    DocumentVersionCreated
}

