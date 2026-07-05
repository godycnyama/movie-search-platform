using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Application.Requests;

public class LoginRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "'email' is required.")]
    [EmailAddress(ErrorMessage = "'email' must be a valid email address.")]
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false, ErrorMessage = "'password' is required.")]
    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;
}
