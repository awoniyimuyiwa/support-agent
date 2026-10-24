namespace Domain;

public class DocumentCategory : AuditedEntity, IHasConcurrencyStamp
{
    protected List<Document> Documents { get; set; } = [];

    public string? Description { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? ConcurrencyStamp { get; set; }

    public DocumentCategory(
        Guid externalId,
        string? description,
        string name)
    {
        ExternalId = externalId;
        Description = description;
        Name = name;
    }
}
