namespace Api.Contracts.Models.V1;

public record DocumentVersionSyncItem(
     Guid Id,
     string ContentHash,
     Guid DocumentId,
     DateTimeOffset CreatedAt,
     DateTimeOffset UpdatedAt,
     string? ConcurrencyStamp,
     bool IsDeleted = false) : DocumentVersionSyncRequestItem(
         Id,
         UpdatedAt, 
         ConcurrencyStamp, 
         IsDeleted);

