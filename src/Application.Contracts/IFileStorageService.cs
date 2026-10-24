namespace Application.Contracts;

public interface IFileStorageService
{
    IReadOnlyDictionary<Guid, string> GenerateUploadUrls(
        IReadOnlyCollection<Guid> fileIds,
        string containerName);
}

