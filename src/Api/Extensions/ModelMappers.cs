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

    extension(CreateDocumentVersionRequestItem model)
    {
        public CreateDocumentVersionDto ToDto(IReadOnlyDictionary<Guid, long> documentIdMap) =>
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
                model.CreatedAt);
    }

    extension(DocumentVersionSyncRequest model)
    {
        public DocumentVersionSyncDto Dto =>
            new(
                [.. model.Items.Select(dv => dv.Dto)],
                model.Behavior);
    }

    extension(DocumentVersionSyncRequestItem model)
    {
        public DocumentVersionSyncItemDto Dto =>
            new(
                model.Id,
                model.UpdatedAt,
                model.ConcurrencyStamp,
                model.IsDeleted);
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

    extension (TicketSyncRequestItem model)
    {
        public TicketSyncItemDto Dto =>
            new(
                model.Id,
                model.Description,
                Domain.Shared.TicketPriority.Low, // default for non admins
                Domain.Shared.TicketStatus.Open, // default for non admins
                model.Title,
                model.CreatedAt,
                model.UpdatedAt,
                model.ConcurrencyStamp);
    }

    extension(FullTicketSyncRequestItem model)
    {
        public TicketSyncItemDto Dto =>
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
