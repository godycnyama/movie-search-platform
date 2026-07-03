using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Application.Requests;

/// <summary>
/// Body for <c>POST /api/v1/auth/change-password</c>. The account is taken from the
/// bearer token's <c>sub</c> claim, never from the request body.
/// </summary>
public class ChangePasswordRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "'current_password' is required.")]
    [JsonPropertyName("current_password")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false, ErrorMessage = "'new_password' is required.")]
    [MinLength(8, ErrorMessage = "'new_password' must be at least 8 characters.")]
    [JsonPropertyName("new_password")]
    public string NewPassword { get; set; } = string.Empty;
}
