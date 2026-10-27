using Domain.Shared;
using Web.Client.Data;

namespace Web.Client.Models;

public class TicketFilterModel
{
    public string SearchTerm { get; set; } = string.Empty;

    public TicketPriority? PriorityFilter { get; set; }

    public TicketStatus? StatusFilter { get; set; }

    public SyncStatus? SyncStatusFilter { get; set; }

    public string SortColumn { get; set; } = nameof(Ticket.CreatedAt);

    public bool SortDescending { get; set; } = true;

    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 100;

    public void Reset()
    {
        // Don't reset PageSize
        PageNumber = 1; 
        PriorityFilter = null;
        SearchTerm = string.Empty;
        SortColumn = nameof(Ticket.CreatedAt);
        SortDescending = true;
        StatusFilter = null;
        SyncStatusFilter = null;
    }
}

