namespace Web.Client.Data;

public class StoredFile
{
    public Guid Id { get; set; }

    public byte[] Bytes { get; set; } = [];

    public string Type { get; set; } = string.Empty;

    public StoredFileUploadDetails? StoredFileUploadDetails { get; set; }
}

public class StoredFileUploadDetails
{
    public Guid Id { get; set; }

    public string Url { get; set; } = string.Empty;

    public Guid StoredFileId { get; set; }
}