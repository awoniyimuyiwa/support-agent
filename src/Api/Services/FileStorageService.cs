using Api.Options;
using Application.Contracts;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Options;
using System.Collections.ObjectModel;
using System.Net.Mime;

namespace Api.Services;

public class FileStorageService(
    BlobServiceClient blobServiceClient,
    IOptions<FileStorageOptions> fileStorageOptions,
    TimeProvider timeProvider) : IFileStorageService
{
    readonly FileStorageOptions _fileStorageOptions = fileStorageOptions.Value;

    public IReadOnlyDictionary<Guid, string> GenerateUploadUrls(
        IReadOnlyCollection<FileDto> files,
        string containerName)
    {
        if (files.Count == 0) { return ReadOnlyDictionary<Guid, string>.Empty; }

        var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
        var urlMap = new Dictionary<Guid, string>(files.Count);
        var expiresOn = timeProvider.GetUtcNow().AddMinutes(_fileStorageOptions.UploadUrlTTLMinutes);

        foreach (var file in files)
        {
            var blobName = $"{file.Id}{GetExtensionFromMime(file.Type)}";
            var blobClient = containerClient.GetBlobClient(blobName);

            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = containerName,
                BlobName = blobClient.Name,
                Resource = "b",
                ExpiresOn = expiresOn,

                // Forces Azure to reject the upload unless the client 
                // provides this exact content-type header during the HTTP PUT request.
                ContentType = file.Type
            };
            sasBuilder.SetPermissions(BlobSasPermissions.Write | BlobSasPermissions.Create);

            Uri sasUri = blobClient.GenerateSasUri(sasBuilder); 
            urlMap.Add(file.Id, sasUri.ToString());
        }

        return urlMap;
    }

    public async Task<bool> Validate(
        FileDto file,
        string containerName,
        CancellationToken cancellationToken = default)
    {
        if (!Contracts.Constants.AllowedFileTypes.Contains(file.Type, StringComparer.OrdinalIgnoreCase)) 
        { 
            return false; 
        }

        var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
        string blobName = $"{file.Id}{GetExtensionFromMime(file.Type)}";
        var blobClient = containerClient.GetBlobClient(blobName);
        if (!await blobClient.ExistsAsync(cancellationToken))
        {
            return false;
        }

        BlobProperties properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
        var maxBytesAllowed = (long)_fileStorageOptions.MaxFileSizeMB * 1024 * 1024;

        if (properties.ContentLength > maxBytesAllowed)
        {
            await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
            return false;
        }

        // 16 bytes is more than enough to capture the 4-5 byte headers securely
        const int sampleSize = 16;

        using var stream = await blobClient.OpenReadAsync(
            new BlobOpenReadOptions(allowModifications: false)
            {
                BufferSize = sampleSize
            },
            cancellationToken: cancellationToken);

        byte[] buffer = new byte[sampleSize];
        int bytesRead = await stream.ReadAsync(buffer, cancellationToken);

        // Enforce 5 byte minimum requirements for pattern match indexing
        if (bytesRead < 5 || 
            !IsValidFileContent(buffer, bytesRead, file.Type))
        {
            await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
            return false;
        }

        return true;
    }

    private static bool IsValidFileContent(
        byte[] buffer, 
        int bytesRead, 
        string fileType)
    {
        if (buffer is null 
            || bytesRead < 5
            || string.IsNullOrWhiteSpace(fileType))
        {
            return false; 
        }

        fileType = fileType.Trim();

        // Validate PDF magic numbers (%PDF-)
        if (string.Equals(fileType, MediaTypeNames.Application.Pdf, StringComparison.OrdinalIgnoreCase))
        {
            return buffer[..5] is [0x25, 0x50, 0x44, 0x46, 0x2D];
        }

        // Validate DOCX magic numbers (PK..)
        if (string.Equals(
            fileType,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document", 
            StringComparison.OrdinalIgnoreCase))
        {
            return buffer[..4] is [0x50, 0x4B, 0x03, 0x04];
        }

        return false;
    }

    private static string GetExtensionFromMime(string type)
    {
        if (string.IsNullOrWhiteSpace(type)) return string.Empty;

        var provider = new FileExtensionContentTypeProvider();

        // Query the internal dictionary mappings directly for a match
        return provider.Mappings
            .FirstOrDefault(kvp => string.Equals(kvp.Value, type.Trim(), StringComparison.OrdinalIgnoreCase))
            .Key ?? string.Empty;
    }
}