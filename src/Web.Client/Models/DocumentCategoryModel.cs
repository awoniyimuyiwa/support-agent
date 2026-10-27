using Domain.Shared;
using System.ComponentModel.DataAnnotations;

namespace Web.Client.Models;

public class DocumentCategoryModel
{
    [StringLength(Constants.StringMaxLength)]
    public string? Description { get; set; } = string.Empty;

    [Required]
    [StringLength(Constants.StringMaxLength)]
    public string Name { get; set; } = string.Empty;
}
