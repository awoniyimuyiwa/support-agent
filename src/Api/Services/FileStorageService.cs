using Api.Options;
using Application.Contracts;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Microsoft.Extensions.Options;
using System.Collections.ObjectModel;

namespace Api.Services;

public class FileStorageService(
    BlobServiceClient blobServiceClient,
    IOptions<FileStorageOptions> fileStorageOptions,
    TimeProvider timeProvider) : IFileStorageService
{
    readonly FileStorageOptions _fileStorageOptions = fileStorageOptions.Value;

    public IReadOnlyDictionary<Guid, string> GenerateUploadUrls(
        IReadOnlyCollection<Guid> fileIds,
        string containerName)
    {
        if (fileIds.Count == 0) { return ReadOnlyDictionary<Guid, string>.Empty; }

        var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
        var urlMap = new Dictionary<Guid, string>(fileIds.Count);
        var expiresOn = timeProvider.GetUtcNow().AddMinutes(_fileStorageOptions.UploadUrlTTLMinutes);

        foreach (var fileId in fileIds)
        {
            var blobClient = containerClient.GetBlobClient(fileId.ToString());

            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = containerName,
                BlobName = blobClient.Name,
                Resource = "b",
                ExpiresOn = expiresOn
            };
            sasBuilder.SetPermissions(BlobSasPermissions.Write | BlobSasPermissions.Create);

            Uri sasUri = blobClient.GenerateSasUri(sasBuilder);
            urlMap.Add(fileId, sasUri.ToString());
        }

        return urlMap;
    }
}
