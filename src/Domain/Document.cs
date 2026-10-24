namespace Domain;

public class Document : AuditedEntity, IHasConcurrencyStamp
{
    protected List<DocumentVersion> DocumentVersions { get; set; } = [];

    /// <summary>
    /// Gets or sets the foreign key identifier for the single parent category.
    /// Setter is public so it can be set without having to set document category
    /// </summary>
    /// <value>
    /// The unique identifier of the associated <see cref="DocumentCategory"/>.
    /// </value>
    /// <remarks>
    /// <para>
    /// **ARCHITECTURE: Strict Single-Category Constraint (One-to-Many)**
    /// </para>
    /// <para>
    /// A document is strictly bound to exactly ONE parent category folder. This design is 
    /// algorithmically and operationally superior to a many-to-many relationship for three reasons:
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// **Dense Composite Indexing (Performance &amp; Recall):** Allows the system to build ultra-efficient 
    /// composite database indices—specifically indexing <c>{ DocumentCategoryId, IsDeleted }</c> together 
    /// before traversing child vector trees. This lets the PostgreSQL engine instantly prune millions of 
    /// irrelevant rows at the root level, dramatically speeding up downstream HNSW vector similarity lookups.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// **Deterministic Agent Routing:** Simplifies AI Multi-Agent decision routing. Instead of an LLM calculating 
    /// complex intersections across multiple cross-referenced groups, it can deterministically target a single 
    /// contextual boundary, cutting down retrieval token usage and response latency.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// **Secure Multi-Tenancy & Compliance:** Enforces clean data lineage. Access controls, tenant isolation 
    /// rules, and data retention policies inherit perfectly from a single parent category node without the risk 
    /// of permission leaks caused by complex junction table overlaps.
    /// </description>
    /// </item>
    /// </list>
    /// </remarks>
    public long DocumentCategoryId { get; set; }

    /// <summary>
    /// Gets or sets the navigation property referencing the single parent category folder.
    /// </summary>
    public DocumentCategory DocumentCategory
    {
        get;
        set
        {
            field = value;
            if (value != null)
            {
                DocumentCategoryId = value.Id;
            }
        }
    } = null!;

    public string Name { get; set; } = string.Empty;

    public string? ConcurrencyStamp { get; set; }

    public IReadOnlyCollection<DocumentVersion> Versions => DocumentVersions;

    public Document() {}

    public Document(
       Guid externalId,
       DocumentCategory documentCategory,
       string name)
    {
        ExternalId = externalId;
        DocumentCategory = documentCategory;
        Name = name;
    }

    public Document(
       Guid externalId,
       long documentCategoryId,
       string name)
    {
        ExternalId = externalId;
        DocumentCategoryId = documentCategoryId;
        Name = name;
    }
}
