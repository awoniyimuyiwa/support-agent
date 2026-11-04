using System.ComponentModel.DataAnnotations;

namespace Web.Client.Data;

public class SyncMetadata
{
    public Guid Id { get; set; }
    
    public string EntityTypeName { get; set; } = string.Empty;

    public string? Cursor { get; set; } = string.Empty;

    public DateTime LastSyncedAt { get; set; }

    /// <summary>
    /// Null for admins; non-null for ordinary users because each user should have their own sync metadata when sharing a device.
    /// </summary>
    public Guid? UserId { get; set; }
}