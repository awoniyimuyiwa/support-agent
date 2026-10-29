using Application.Contracts;
using Domain;
using Domain.Shared;
using Microsoft.Extensions.DependencyInjection;
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
    public async Task<IReadOnlyCollection<TicketDto>> Sync(
        TicketSyncDto input,
        long userId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        if (input.Items.Count == 0) { return []; }

        var conflicts = new List<TicketDto>();

        var ids = input.Items
            .Select(x => x.Id)
            .ToList();

        var idTicketMap = await ticketRepository.GetByIds(
            ids,
            includes: [nameof(Ticket.User)],
            ignoreQueryFilters: true,
            cancellationToken);

        foreach (var ticketDto in input.Items)
        {
            if (idTicketMap.TryGetValue(ticketDto.Id, out var ticket))
            {
                // Non-admins cannot modify tickets that don't belong them
                if (!isAdmin && ticket.UserId != userId)
                {
                    // Handle edge cases where a user (e.g., a recently demoted admin) attempts to modify 
                    // an unauthorized ticket. Instead of silently skipping, return the DTO flagged as 
                    // deleted so the client application knows how to process the sync conflict.
                    conflicts.Add(new TicketDto(
                        ticketDto.Id,
                        ticket.User.ExternalId,
                        ticketDto.Description,
                        ticketDto.Priority,
                        ticketDto.Status,
                        ticketDto.Title,
                        ticketDto.CreatedAt,
                        ticketDto.UpdatedAt,
                        ticketDto.ConcurrencyStamp, // Same concurrency stamp
                        IsDeleted: true)); // Mark as deleted
                    continue;
                }

                // Non-admins cannot modify a ticket that is no longer Open
                if (!isAdmin && ticket.Status != TicketStatus.Open)
                {
                    // This forces the client to overwrite the local draft
                    conflicts.Add(new TicketDto(
                        ticket.ExternalId,
                        ticket.User.ExternalId,
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

                // True client-vs-server collision matching
                if (ticket.ConcurrencyStamp != ticketDto.ConcurrencyStamp
                    && input.Behavior != SyncBehavior.ClientWins)
                {
                    conflicts.Add(new TicketDto(
                        ticket.ExternalId,
                        ticket.User.ExternalId,
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

                ticket.Description = ticketDto.Description;
                ticket.Title = ticketDto.Title;
                ticket.IsDeleted = ticketDto.IsDeleted;

                // Only admins can change priority and status
                if (isAdmin)
                {
                    ticket.Priority = ticketDto.Priority;
                    ticket.Status = ticketDto.Status;
                }

                ticketRepository.Update(ticket);
            }
            else
            {
                // Row generated completely offline by field worker
                // Priority and status already sanitized to default values for non admins at the API via model mapping extensions
                var newTicket = new Ticket(
                    ticketDto.Id,
                    ticketDto.Description,
                    ticketDto.Priority,
                    ticketDto.Status,
                    ticketDto.Title,
                    userId)
                {
                    IsDeleted = ticketDto.IsDeleted
                };

                ticketRepository.Add(newTicket);
            }
        }

        await uow.SaveChanges(cancellationToken);

        return [.. conflicts];
    }
}

