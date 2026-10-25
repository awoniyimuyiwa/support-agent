namespace Api.Contracts.Models.V1;

public record SyncResponse<T>(List<T> Conflicts) where T : class, ISyncableModel;
