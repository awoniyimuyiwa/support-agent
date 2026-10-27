using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore; 
using System.Security.Cryptography;
using Web.Client.Data;

namespace Web.Client.Extensions;

public static class BrowserFile
{
    const int MaxMB = 4;
    const long MaxBytesAllowed = MaxMB * 1024 * 1024;

    extension(IBrowserFile file)
    { 
        /// <summary>
        /// Validates extensions and byte limits directly on the browser file instance.
        /// </summary>
        public string Validate()
        {
            var ext = Path.GetExtension(file.Name).ToLower();

            if (ext != ".pdf" && ext != ".docx")
            {
                return "Unsupported File format! Only PDF and DOCX structural assets are allowed.";
            }

            if (file.Size > MaxBytesAllowed)
            {
                return $"File size cannot exceed {MaxMB}MB";
            }

            return string.Empty;
        }

        /// <summary>
        /// Reads file data once, runs database duplicate checks, and returns a DocumentVersion
        /// </summary>
        public async Task<(DocumentVersion? Version, string Error)> ComputeAndCreateVersion(
            IQueryable<DocumentVersion> dbSet,
            Guid documentId)
        {
            var ext = Path.GetExtension(file.Name).ToLower();

            using var memoryStream = new MemoryStream();
            await file.OpenReadStream(file.Size).CopyToAsync(memoryStream);
            var fileBytes = memoryStream.ToArray();

            var hashBytes = SHA256.HashData(fileBytes);
            var hash = Convert.ToHexStringLower(hashBytes);
            var hashExistsLocally = await dbSet.AnyAsync(dv => dv.ContentHash == hash && !dv.IsDeleted);

            if (hashExistsLocally)
            {
                return (null, "Duplicate version detected. This file content matches that of an existing version.");
            }

            var storedFile = new StoredFile
            {
                Id = Guid.NewGuid(),
                Bytes = fileBytes,
                Type = ext == ".pdf" ? "application/pdf" : "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
            };

            var now = DateTime.UtcNow;

            var documentVersion = new DocumentVersion
            {
                Id = Guid.NewGuid(),
                DocumentId = documentId,
                ContentHash = hash,
                File = storedFile,
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = false,
                SyncStatus = SyncStatus.PendingInsert
            };

            return (documentVersion, string.Empty);
        }
    }
}
