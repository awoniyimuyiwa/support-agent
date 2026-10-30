namespace Web.Client.Data;

public class StoredFile
{
    public Guid Id { get; set; }

    public byte[] Bytes { get; set; } = [];

    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Set when file has been successfully uploaded
    /// </summary>
    public Guid? ServerFileId { get; set; }
}