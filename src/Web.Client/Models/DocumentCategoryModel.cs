using System.ComponentModel.DataAnnotations;

namespace Web.Client.Models;

public class DocumentCategoryModel
{
    [StringLength(Domain.Shared.Constants.StringMaxLength)]
    public string? Description { get; set; } = string.Empty;

    [Required]
    [StringLength(Domain.Shared.Constants.StringMaxLength)]
    public string Name { get; set; } = string.Empty;

    public string? ConcurrencyStamp { get; set; }
}
