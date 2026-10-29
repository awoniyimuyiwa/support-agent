namespace Web.Client.Data;

public interface IHasFile : ISyncableEntity
{
    public Guid StoredFileId { get; set; }

    StoredFile? StoredFile { get; set; }
}
