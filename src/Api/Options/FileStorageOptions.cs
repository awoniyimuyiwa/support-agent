namespace Api.Options;


using System.ComponentModel.DataAnnotations;

public class FileStorageOptions
{
    public const string SectionName = "FileStorageOptions";

    [Required(AllowEmptyStrings = false, ErrorMessage = "'DocumentsContainerName' must be specified and cannot be empty.")]
    public string DocumentsContainerName { get; set; } = string.Empty;

    [Range(1, 15, ErrorMessage = "'UploadUrlTTLMinutes' must be a positive integer between 1 and 15.")]
    public int UploadUrlTTLMinutes { get; set; }
}

