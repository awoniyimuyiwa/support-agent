namespace Domain.Shared;

/// <summary>
/// Identifies the specific background service for health and performance tracking.
/// </summary>
/// <remarks>
/// IMPORTANT: Always append new members to the end of the list to maintain 
/// database compatibility and prevent value shifts for existing records.
/// </remarks>
public enum BackgroundServiceStatType
{
    AuditLogCleanUp,
    BackgroundJob
}
