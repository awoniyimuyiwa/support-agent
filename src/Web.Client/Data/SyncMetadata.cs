using System.ComponentModel.DataAnnotations;

namespace Web.Client.Data;

public class SyncMetadata
{
    [Key]
    public string EntityTypeName { get; set; } = string.Empty;

    public string? Cursor { get; set; } = string.Empty;

    public DateTime LastSyncedAt { get; set; }
}