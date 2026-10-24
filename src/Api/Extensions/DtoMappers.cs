using Api.Contracts.Models.V1;
using Api.Contracts.Models.V1.BackgroundJobs;
using Api.Services;
using Domain;
using Domain.BackgroundJobs;
namespace Api.Extensions;

public static class DtoMappers
{
    extension(AuditLogDto dto)
    {
        // var model = myDto.Model;
        public AuditLogModel Model =>
            new(dto.Id,
                dto.BrowserInfo,
                dto.Duration,
                dto.Exception,
                dto.HttpMethod,
                dto.IpAddress,
                dto.RequestId,
                dto.StatusCode,
                dto.Timestamp,
                dto.Url,
                dto.UserId);
    }

    extension(AuditLogActionDto dto)
    {
        // Converted into an expression-bodied instance property
        public AuditLogActionModel Model =>
            new(
                dto.ExecutionTime,
                dto.MethodName,
                dto.Parameters,
                dto.ServiceName);
    }

    extension(BackgroundJobDto dto)
    {
        public BackgroundJobModel Model =>
            new(
                dto.Id,
                dto.Attempts,
                dto.Error,
                dto.MaxRetries,
                dto.Payload,
                dto.Priority,
                dto.ScheduledFor,
                dto.SourceId,
                dto.Status,
                dto.TraceId,
                dto.Type,
                dto.CreatedAt,
                dto.UpdatedAt,
                dto.ConcurrencyStamp);
    }

    extension(BackgroundServiceStatDto dto)
    {
        public BackgroundServiceStatModel Model =>
            new(
                dto.Id,
                dto.Details,
                dto.LastRunAt,
                dto.Total,
                dto.TotalInLastRun,
                dto.Type);
    }

    extension(EntityChangeDto dto)
    {
        public EntityChangeModel Model =>
            new(
                dto.EntityName,
                dto.NewValues,
                dto.OldValues,
                dto.Type);
    }

    extension(FullAuditLogDto dto)
    {
        public FullAuditLogModel Model =>
            new(
                dto.Id,
                dto.BrowserInfo,
                dto.Duration,
                dto.Exception,
                dto.HttpMethod,
                dto.IpAddress,
                dto.RequestId,
                dto.StatusCode,
                dto.Timestamp,
                dto.Url,
                dto.UserId,
                [.. dto.AuditLogActions.Select(a => a.Model)],
                [.. dto.EntityChanges.Select(e => e.Model)]);
    }

    extension(DocumentCategoryDto dto)
    {
        public DocumentCategoryModel Model =>
            new(
                dto.Id,
                dto.Description,
                dto.Name,
                dto.CreatedAt,
                dto.UpdatedAt,
                dto.ConcurrencyStamp,
                dto.IsDeleted);
    }

    extension(DocumentDto dto)
    {
        public DocumentModel Model =>
            new(
                dto.Id,
                dto.CategoryId,
                dto.Name,
                dto.CreatedAt,
                dto.UpdatedAt,
                dto.ConcurrencyStamp,
                dto.IsDeleted);
    }

    extension(DocumentVersionDto dto)
    {
        public DocumentVersionModel Model => 
            new(    
                dto.Id,
                dto.ContentHash,
                dto.DocumentId,
                dto.FileId,
                dto.FileType,
                dto.CreatedAt,
                dto.UpdatedAt,
                dto.ConcurrencyStamp,
                dto.IsDeleted);
    }

    extension(TicketDto dto)
    {
        public FullTicketModel Model =>
            new(
                dto.Id,
                dto.Description,
                dto.Priority,
                dto.Status,
                dto.Title,
                dto.CreatedAt,
                dto.UpdatedAt,
                dto.ConcurrencyStamp,
                dto.IsDeleted);
    }

    extension(UserDto dto)
    {
        public UserModel Model =>
            new(
                dto.Email,
                dto.FirstName,
                dto.Id,
                dto.IsEmailConfirmed,
                dto.LastName,
                dto.LockoutEnd,
                dto.UserName,
                dto.CreatedAt,
                dto.UpdatedAt,
                dto.ConcurrencyStamp);
    }

    extension(UserSessionDto dto)
    {
        public UserSessionModel Model =>
            new(
                Id: dto.SessionIdHash,
                AuthScheme: dto.AuthScheme,
                CorrelationId: dto.CorrelationId,
                DeviceInfo: dto.DeviceInfo,
                IpAddress: dto.IpAddress,
                LastSeen: dto.LastSeen,
                Location: dto.Location,
                LoginTime: dto.LoginTime);
    }

    extension<TSource>(CursorList<TSource> source) where TSource : class
    {
        // Exposes a generic method to handle the destination mapping projection
        public CursorListModel<TDestination> ToModelList<TDestination>(Func<TSource, TDestination> selector)
            where TDestination : class
        {
            var mappedItems = source.Items
                .Select(selector)
                .ToList();

            return new CursorListModel<TDestination>(
                Items: mappedItems,
                Next: source.Next,
                PageSize: source.PageSize);
        }
    }

    extension<TSource>(PaginatedList<TSource> source) where TSource : class
    {
        public PaginatedListModel<TDestination> ToModelList<TDestination>(Func<TSource, TDestination> selector)
            where TDestination : class
        {
            var mappedItems = source.Items
                .Select(selector)
                .ToList();

            return new PaginatedListModel<TDestination>(
                Items: mappedItems,
                TotalCount: source.TotalCount,
                PageNumber: source.PageNumber,
                TotalPages: source.TotalPages,
                HasPreviousPage: source.HasPreviousPage,
                HasNextPage: source.HasNextPage);
        }
    }
}
