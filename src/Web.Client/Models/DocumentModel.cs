using Domain.Shared;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace Web.Client.Models;

public class DocumentModel
{
    [Required]
    [StringLength(Constants.StringMaxLength)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string DocumentCategoryId { get; set; } = string.Empty;
}