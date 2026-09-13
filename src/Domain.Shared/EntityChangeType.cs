namespace Domain.Shared;

/// <summary>
/// Specifies the type of modification made to an entity.
/// </summary>
/// <remarks>
/// IMPORTANT: Always append new members to the end of the list to maintain 
/// database compatibility and prevent value shifts for existing records.
/// </remarks>
public enum EntityChangeType
{
    /// <summary>
    /// Created
    /// </summary>
    Created,

    /// <summary>
    /// Updated
    /// </summary>
    Updated,

    /// <summary>
    /// Delete
    /// </summary>
    Deleted
}
