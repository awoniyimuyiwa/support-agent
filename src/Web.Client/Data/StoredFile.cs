namespace Web.Client.Data;

public class StoredFile
{
    public Guid Id { get; set; }

    public byte[] Bytes { get; set; } = Array.Empty<byte>();

    public string Type { get; set; } = string.Empty;
}