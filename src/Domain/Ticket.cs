using Domain.Shared;

namespace Domain;

public class Ticket : AuditedEntity, IHasConcurrencyStamp
{
    public string Description { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
   
    public TicketPriority Priority { get; set; }

    public TicketStatus Status { get; set; }

    public string? ConcurrencyStamp { get; set; }
   
    public Ticket() {}

    public Ticket(
        string description,
        string title, 
        TicketPriority priority,
        TicketStatus status)
    {
        Description = description;
        Title = title;
        Priority = priority;
        Status = status;
    }
}
