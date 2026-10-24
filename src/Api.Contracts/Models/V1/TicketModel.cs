using Api.Contracts.Attributes;
using Domain.Shared;
using System.ComponentModel.DataAnnotations;

namespace Api.Contracts.Models.V1;

public record TicketModel(
    [RequiredNotDefault] Guid Id,
    [MinLength(Domain.Shared.Constants.StringMinLength)] [MaxLength(Constants.TextMaxLength)] string Description,
    [MinLength(Domain.Shared.Constants.StringMinLength)] [MaxLength(Domain.Shared.Constants.StringMaxLength)] string Title,
    [RequiredNotDefault] DateTimeOffset CreatedAt,
    [RequiredNotDefault] DateTimeOffset UpdatedAt,
    [MaxLength(Domain.Shared.Constants.StringMaxLength)] string? ConcurrencyStamp,
    bool IsDeleted = false) : ISyncableModel;

public record FullTicketModel(
    [RequiredNotDefault] Guid Id,
    [MinLength(Domain.Shared.Constants.StringMinLength)] [MaxLength(Constants.TextMaxLength)] string Description,
    [EnumDataType(typeof(TicketPriority))] TicketPriority Priority,
    [EnumDataType(typeof(TicketStatus))] TicketStatus Status,
    [MinLength(Domain.Shared.Constants.StringMinLength)] [MaxLength(Domain.Shared.Constants.StringMaxLength)] string Title,
    [RequiredNotDefault] DateTimeOffset CreatedAt,
    [RequiredNotDefault] DateTimeOffset UpdatedAt,
    [MaxLength(Domain.Shared.Constants.StringMaxLength)] string? ConcurrencyStamp,
    bool IsDeleted = false) : TicketModel(
        Id, 
        Description, 
        Title, 
        CreatedAt, 
        UpdatedAt, 
        ConcurrencyStamp, 
        IsDeleted);

public record TicketSyncModel(
    [UniqueMax(maxLength: Constants.MaxLengthOfList, UniquePropertyName = nameof(TicketModel.Id))] List<TicketModel> Items,
    [EnumDataType(typeof(SyncBehavior))] SyncBehavior Behavior = SyncBehavior.Default)
{
    public List<TicketModel> Items { get; init; } = Items ?? [];
}

public record FullTicketSyncModel(
    [UniqueMax(maxLength: Constants.MaxLengthOfList, UniquePropertyName = nameof(TicketModel.Id))] List<FullTicketModel> Items,
    [EnumDataType(typeof(SyncBehavior))] SyncBehavior Behavior = SyncBehavior.Default)
{
    public List<FullTicketModel> Items { get; init; } = Items ?? [];
}
