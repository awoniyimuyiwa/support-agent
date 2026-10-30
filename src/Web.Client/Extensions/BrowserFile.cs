using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Web.Client.Data;

namespace Web.Client.Extensions;

public static class BrowserFile
{
    public const int MaxMB = 4;
    const long MaxBytesAllowed = MaxMB * 1024 * 1024;

    extension(IBrowserFile file)
    {
        public string Validate()
        {
            var ext = Path.GetExtension(file.Name).ToLower();

            if (ext != ".pdf" && ext != ".docx")
            {
                return "Unsupported File format! Only PDF and DOCX files are allowed.";
            }

            if (file.Size > MaxBytesAllowed)
            {
                return $"File size cannot exceed {MaxMB}MB";
            }

            return string.Empty;
        }

        public async Task<(DocumentVersion? Version, string Error)> ComputeAndCreateVersion(
            IQueryable<DocumentVersion> dbSet,
            Guid documentId)
        {
            var (fileBytes, hash, mimeType) = await file.GetFileData();

            var hashExistsLocally = await dbSet.AnyAsync(dv => dv.ContentHash == hash && !dv.IsDeleted);
            if (hashExistsLocally)
            {
                return (null, "Duplicate version detected. This file content matches that of an existing version.");
            }

            var now = DateTime.UtcNow;
            var documentVersion = new DocumentVersion
            {
                Id = Guid.NewGuid(),
                DocumentId = documentId,
                ContentHash = hash,
                StoredFile = new StoredFile
                {
                    Id = Guid.NewGuid(),
                    Bytes = fileBytes,
                    Type = mimeType
                },
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = false,
                SyncStatus = SyncStatus.PendingInsert
            };

            return (documentVersion, string.Empty);
        }

        public async Task<string> ComputeAndUpdateVersion(
            IQueryable<DocumentVersion> dbSet,
            DocumentVersion existingVersion)
        {
            var (fileBytes, hash, mimeType) = await file.GetFileData();

            var hashExistsElsewhere = await dbSet.AnyAsync(dv => dv.ContentHash == hash
                                                              && dv.Id != existingVersion.Id
                                                              && !dv.IsDeleted);
            if (hashExistsElsewhere)
            {
                return "Duplicate version detected. This file content matches that of another existing version.";
            }

            var storedFile = existingVersion.StoredFile ?? new StoredFile { Id = Guid.NewGuid() };
            storedFile.Bytes = fileBytes;
            storedFile.Type = mimeType;
            // Mark as not uploaded
            storedFile.ServerFileId = null;

            existingVersion.ContentHash = hash;
            existingVersion.StoredFile = storedFile;
            existingVersion.UpdatedAt = DateTime.UtcNow;

            // Re-queue back into the synchronization loop
            existingVersion.SerializedErrors = null;
            existingVersion.SyncStatus = SyncStatus.PendingInsert;

            return string.Empty;
        }

        private async Task<(byte[] Bytes, string Hash, string MimeType)> GetFileData()
        {
            var ext = Path.GetExtension(file.Name).ToLower();

            using var memoryStream = new MemoryStream();
            await file.OpenReadStream(file.Size).CopyToAsync(memoryStream);
            var fileBytes = memoryStream.ToArray();

            var hashBytes = SHA256.HashData(fileBytes);
            var hash = Convert.ToHexStringLower(hashBytes);

            var mimeType = ext == ".pdf"
                ? "application/pdf"
                : "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

            return (fileBytes, hash, mimeType);
        }
    }
}