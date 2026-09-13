using Application.Contracts;
using Domain;
using Domain.Shared;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Immutable;
using System.Text.Json;

namespace Application.Services;

class TicketService(
    ITicketRepository ticketRepository,
    IUnitOfWork uow,
    IAuditLogManager auditLogManager,
    TimeProvider timeProvider,
    [FromKeyedServices(Constants.AuditLogJsonSerializationOptionsName)] JsonSerializerOptions auditJsonSerializerOptions)
    : BaseService(auditLogManager, timeProvider, auditJsonSerializerOptions), ITicketService
{
    public async Task<ImmutableList<TicketDto>> Sync(
        TicketSyncDto input,
        CancellationToken cancellationToken = default)
    {
        var conflicts = new List<TicketDto>();

        var ids = input.Tickets
            .Select(x => x.Id)
            .ToImmutableList();

        var tickets = await ticketRepository.GetByIds(
            ids,
            cancellationToken);

        foreach (var ticketDto in input.Tickets)
        {
            if (tickets.TryGetValue(ticketDto.Id, out var ticket))
            {
                // True client-vs-server collision matching
                if (ticket.ConcurrencyStamp != ticketDto.ConcurrencyStamp
                    && input.Behavior != SyncBehavior.ForceClientWins)
                {
                    conflicts.Add(new TicketDto(
                        ticket.ExternalId,
                        ticket.Description,
                        ticket.Priority,
                        ticket.Status,
                        ticket.Title,
                        ticket.CreatedAt,
                        ticket.UpdatedAt,
                        ticket.ConcurrencyStamp,
                        ticket.IsDeleted));
                    continue;
                }

                ticket.Title = ticketDto.Title;
                ticket.Description = ticketDto.Description;
                ticket.Priority = ticketDto.Priority;
                ticket.Status = ticketDto.Status;

                if (input.Behavior == SyncBehavior.ForceClientWins)
                {
                    ticket.IsDeleted = false;
                }

                // No update to concurrency stamp from db
                ticketRepository.Update(ticket);
            }
            else
            {
                // Row generated completely offline by field worker
                var newTicket = new Ticket
                {
                    ExternalId = ticketDto.Id,
                    Title = ticketDto.Title,
                    Description = ticketDto.Description,
                    Priority = ticketDto.Priority,
                    Status = ticketDto.Status
                    //ClientCreatedAt = ticketDto.CreatedAt // Save the offline time here for UI history logs to display audit or sorting trails to your users like: "Drafted offline on Oct 7 at 9:00 PM, Synchronized to cloud at 9:33 PM."[1.1]
                };

                ticketRepository.Add(newTicket);
            }
        }

        await uow.SaveChanges(cancellationToken);

        return [.. conflicts];
    }
}

