using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Application.Requests;

/// <summary>
/// Body for <c>POST /api/v1/auth/signup</c>. New accounts always get the "reader"
/// role; admins are promoted out-of-band, never self-service.
/// </summary>
public class SignUpRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "'email' is required.")]
    [EmailAddress(ErrorMessage = "'email' must be a valid email address.")]
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false, ErrorMessage = "'password' is required.")]
    [MinLength(8, ErrorMessage = "'password' must be at least 8 characters.")]
    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;
}
