using Domain.Shared;
using System.ComponentModel.DataAnnotations;

namespace Web.Client.Models;

public class TicketModel
{
    [Required]
    [StringLength(Api.Contracts.Constants.TextMaxLength)]
    public string Description { get; set; } = string.Empty;

    public TicketPriority Priority { get; set; } = TicketPriority.Low;

    public TicketStatus Status { get; set; } = TicketStatus.Open;

    [Required]
    [StringLength(Constants.StringMaxLength)]
    public string Title { get; set; } = string.Empty;
}