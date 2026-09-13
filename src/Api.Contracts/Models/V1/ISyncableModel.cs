namespace Api.Contracts.Models.V1;

public interface ISyncableModel : IModel
{
    /// <summary>
    /// Added to serve as a cloud deletion tombstone flag
    /// </summary>
    bool IsDeleted { get; }
}


