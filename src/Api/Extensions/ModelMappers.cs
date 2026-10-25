using Api.Contracts.Models.V1;
using Api.Contracts.Models.V1.BackgroundJobs;
using Application.Contracts;
using Domain;
using Domain.BackgroundJobs;

namespace Api.Extensions;

public static class ModelMappers
{
    extension(AuditLogFilterRequest model)
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

    extension(DocumentCategorySyncItem model)
    {
        public DocumentCategoryDto Dto =>
            new(
                model.Id,
                model.Description,
                model.Name,
                model.CreatedAt,
                model.UpdatedAt,
                model.ConcurrencyStamp);
    }

    extension(DocumentCategorySyncRequest model)
    {
        public DocumentCategorySyncDto Dto =>
            new(
                [.. model.Items.Select(dc => dc.Dto)],
                model.Behavior);
    }

    extension (DocumentSyncItem model)
    {
        public DocumentSyncItemDto ToSyncItemDto(IReadOnlyDictionary<Guid, long> categoryIdMap) =>
            new(
                model.Id,
                new EntityDto()
                {
                    Id = categoryIdMap[model.CategoryId],
                    ExternalId = model.CategoryId
                },
                model.Name,
                model.CreatedAt,
                model.UpdatedAt,
                model.ConcurrencyStamp);
    }

    extension(DocumentSyncRequest model)
    {
        public DocumentSyncDto ToDto(IReadOnlyDictionary<Guid, long> categoryIdMap) =>
            new(
                [.. model.Items.Select(d => d.ToSyncItemDto(categoryIdMap))],
                model.Behavior);
    }

    extension(DocumentVersionSyncItem model)
    {
        public DocumentVersionSyncItemDto ToSyncItemDto(IReadOnlyDictionary<Guid, long> documentIdMap) =>
            new(
                model.Id,
                model.ContentHash,
                new EntityDto()
                {
                    Id = documentIdMap[model.DocumentId],
                    ExternalId = model.DocumentId
                },
                model.FileId,
                model.FileType,
                model.CreatedAt,
                model.UpdatedAt,
                model.ConcurrencyStamp);
    }

    extension(DocumentVersionSyncRequest model)
    {
        public DocumentVersionSyncDto ToDto(IReadOnlyDictionary<Guid, long> docuemntIdMap) =>
            new(
                [.. model.Items.Select(dv => dv.ToSyncItemDto(docuemntIdMap))],
                model.Behavior);
    }

    extension(GetBackgroundJobRequest model)
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

    extension(RequeueBackgroundJobRequest model)
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

    extension (TicketSyncItem model)
    {
        public TicketDto Dto =>
            new(
                model.Id,
                model.Description,
                Domain.Shared.TicketPriority.Low,
                Domain.Shared.TicketStatus.Open,
                model.Title,
                model.CreatedAt,
                model.UpdatedAt,
                model.ConcurrencyStamp);
    }

    extension(FullTicketSyncItem model)
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

    extension(TicketSyncRequest model)
    {
        public TicketSyncDto Dto =>
            new(
                [.. model.Items.Select(t => t.Dto)],
                model.Behavior);
    }

    extension(FullTicketSyncRequest model)
    {
        public TicketSyncDto Dto =>
            new(
                [.. model.Items.Select(t => t.Dto)],
                model.Behavior);
    }

    extension(UserFilterRequest model)
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
