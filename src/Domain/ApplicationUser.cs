using Microsoft.AspNetCore.Identity;

namespace Domain;

public class ApplicationUser : IdentityUser<long>, IAuditedEntity
{
    public Guid ExternalId { get; set; }
    
    public string FirstName { get; set; } = "";

    public string LastName { get; set; } = "";

    [NotAudited]
    public DateTimeOffset CreatedAt { get; set; }

    [NotAudited]
    public DateTimeOffset UpdatedAt { get; set; }

    // Should be audited
    public bool IsDeleted { get; set; } = false;
}
