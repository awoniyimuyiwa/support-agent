namespace Domain.Shared;

/// <summary>
/// Specifies the status of a ticket.
/// </summary>
/// <remarks>
/// IMPORTANT: Always append new members to the end of the list to maintain 
/// database compatibility and prevent value shifts for existing records.
/// </remarks>
public enum TicketStatus
{
    Open,

    InProgress,

    Resolved,

    Closed
}