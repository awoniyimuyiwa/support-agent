using System.ComponentModel.DataAnnotations;

namespace Web.Client.Models;

public record LoginModel
{
    // Public properties with 'set' allows two-way data binding
    [Required(ErrorMessage = "Email address or first name is required.")]
    //[EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    public string? Email { get; set; } = null;

    [Required(ErrorMessage = "Password is required.")]
    public string? Password { get; set; } = null;
}
