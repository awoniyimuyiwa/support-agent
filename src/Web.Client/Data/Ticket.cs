using Domain.Shared;

namespace Web.Client.Data;

public class Ticket : ISyncableEntity
{
    public Guid Id { get; set; }

    public string Description { get; set; } = string.Empty;

    public TicketPriority Priority { get; set; }

    public TicketStatus Status { get; set; }

    public string Title { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public string? ConcurrencyStamp { get; set; }

    public bool IsDeleted { get; set; }

    public string? SerializedErrors { get; set; }

    public SyncStatus SyncStatus { get; set; } = SyncStatus.Synced;
}
