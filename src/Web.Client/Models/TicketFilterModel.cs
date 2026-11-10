using Domain.Shared;
using Web.Client.Data;

namespace Web.Client.Models;

public class TicketFilterModel : FilterModel    
{
    public TicketPriority? Priority { get; set; }

    public TicketStatus? Status { get; set; }

    public string SortColumn { get; set; } = nameof(Ticket.CreatedAt);

    public override void Reset()
    {
        // Don't reset PageSize
        base.Reset();
        Priority = null;
        SortColumn = nameof(Ticket.CreatedAt);
    }
}

