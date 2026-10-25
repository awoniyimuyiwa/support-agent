namespace Application.Contracts;

public interface IFileStorageService
{
    /// <summary>
    /// Generates pre-signed upload URLs for a collection of files with built-in type restrictions.
    /// Locks down the allowed MIME type directly on the storage signature to prevent cloud-edge circumvention.
    /// </summary>
    /// <param name="files">The collection of file metadata requests containing target identifiers and MIME types.</param>
    /// <param name="containerName">The target cloud blob storage container name destination.</param>
    /// <returns>A read-only dictionary mapping each unique file identifier to its authorized upload destination string.</returns>
    IReadOnlyDictionary<Guid, string> GenerateUploadUrls(
        IReadOnlyCollection<FileDto> files,
        string containerName);

    /// <summary>
    /// Executes a post-upload security inspection by verifying actual file sizes and scanning structural magic byte sequences.
    /// Automatically purges any files that violate configured restrictions or contain corrupted signatures.
    /// </summary>
    /// <param name="file">The file metadata instance to match and evaluate.</param>
    /// <param name="containerName">The target cloud blob storage container hosting the uploaded data.</param>
    /// <param name="cancellationToken">A token to monitor for operation cancellation requests.</param>
    /// <returns>True if the file completely satisfies all security, size, and header constraints; otherwise, false.</returns>
    Task<bool> Validate(
        FileDto file,
        string containerName,
        CancellationToken cancellationToken = default);
}

public record FileDto(
    Guid Id,
    string Type){}
