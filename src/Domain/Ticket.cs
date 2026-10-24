using Domain.Shared;

namespace Domain;

public class Ticket : AuditedEntity, IHasConcurrencyStamp
{
    public string Description { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
   
    public TicketPriority Priority { get; set; }

    public TicketStatus Status { get; set; }

    public string? ConcurrencyStamp { get; set; }

    public long UserId { get; protected set; }

    public ApplicationUser User
    {
        get;
        protected set
        {
            field = value;
            if (value != null)
            {
                UserId = value.Id;
            }
        }
    } = null!;

    public Ticket() {}

    public Ticket(
        Guid externalId,
        string description,
        TicketPriority priority,
        TicketStatus status,
        string title,
        long userId)
    {
        ExternalId = externalId;
        Description = description;
        Priority = priority;
        Status = status;
        Title = title;
        UserId = userId;
    }
}
