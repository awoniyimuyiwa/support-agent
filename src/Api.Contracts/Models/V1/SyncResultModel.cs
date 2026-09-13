namespace Api.Contracts.Models.V1;

public record SyncResultModel<T>(List<T> Conflicts) where T : class, ISyncableModel;
