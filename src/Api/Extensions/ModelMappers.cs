using Api.Contracts.Models.V1;
using Api.Contracts.Models.V1.BackgroundJobs;
using Application.Contracts;
using Domain;
using Domain.BackgroundJobs;
using System.Collections.Immutable;

namespace Api.Extensions;

public static class ModelMappers
{
    extension(AuditLogFilterModel model)
    {
        public AuditLogFilterDto Dto =>
           new(
               From: model.From,
               HttpMethod: model.HttpMethod,
               IpAddress: model.IpAddress,
               IsSuccessful: model.IsSuccessful,
               RequestId: model.RequestId,
               StatusCode: model.StatusCode,
               To: model.To,
               Url: model.Url,
               UserId: model.UserId);
    }

    extension(GetBackgroundJobFilterModel model)
    {
        public GetBackgroundJobFilterDto Dto =>
           new(
               CreatedFrom: model.CreatedFrom,
               CreatedTo: model.CreatedTo,
               Priorities: model.Priorities,
               ScheduledFrom: model.ScheduledFrom,
               ScheduledTo: model.ScheduledTo,
               Statuses: model.Statuses,
               Types: model.Types,
               UpdatedFrom: model.UpdatedFrom,
               UpdatedTo: model.UpdatedTo);
    }

    extension(RequeueBackgroundJobFilterModel model)
    {
        public RequeueBackgroundJobFilterDto Dto =>
           new(
               CreatedFrom: model.CreatedFrom,
               CreatedTo: model.CreatedTo,
               Ids: model.Ids,
               Priorities: model.Priorities,
               ScheduledFrom: model.ScheduledFrom,
               ScheduledTo: model.ScheduledTo,
               SourceIds: model.SourceIds,
               TraceIds: model.TraceIds,
               Types: model.Types,
               UpdatedFrom: model.UpdatedFrom,
               UpdatedTo: model.UpdatedTo);
    }

    extension (TicketSyncModel model)
    {
        public TicketSyncDto Dto =>
            new(
                [.. model.Items.Select(t => t.Dto)],
                model.Behavior);
    } 

    extension (TicketModel model)
    {
        public TicketDto Dto =>
            new(
                model.Id,
                model.Description,
                model.Priority,
                model.Status,
                model.Title,
                model.CreatedAt,
                model.UpdatedAt,
                model.ConcurrencyStamp);
    }

    extension(UserFilterModel model)
    {
        public UserFilterDto Dto =>
            new(
                SearchTerm: model.Search,
                CreatedFrom: model.CreatedFrom,
                CreatedTo: model.CreatedTo,
                IsEmailConfirmed: model.IsEmailConfirmed,
                UpdatedFrom: model.UpdatedFrom,
                UpdatedTo: model.UpdatedTo);
    }
}
