using Domain.Shared;

namespace Api.Contracts.Models.V1;

public record DeltaResponse<T>(
    List<T> Items,
    [property: Protected] string? Next,
    DateTimeOffset Until) where T : ISyncableModel; 
