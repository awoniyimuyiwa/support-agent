using Domain.Shared;

namespace Domain;

public record TicketDto(
    Guid Id,
    string Description,
    TicketPriority Priority,
    TicketStatus Status,
    string Title,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? ConcurrencyStamp,
    bool IsDeleted = false);

public record InternalTicketDto(
    long InternalId,
    Guid ExternalId,
    string Description,
    TicketPriority Priority,
    TicketStatus Status,
    string Title,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? ConcurrencyStamp,
    bool IsDeleted = false) : TicketDto(
        Id: ExternalId,
        Description,
        Priority,
        Status,
        Title,
        CreatedAt,
        UpdatedAt,
        ConcurrencyStamp,
        IsDeleted), IHasInternalCursor, IHasInternalDeltaCursor;

public record TicketFilterDto(
    DateTimeOffset? CreatedFrom = null,
    DateTimeOffset? CreatedTo = null,
    TicketPriority[]? Priorities = null, // ?Priorities=High&Priorities=Normal
    TicketStatus[]? Statuses = null,
    DateTimeOffset? UpdatedFrom = null,
    DateTimeOffset? UpdatedTo = null,
    string? SearchTerm = null);
