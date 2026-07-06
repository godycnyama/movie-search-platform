using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Application.Requests;

public class ChangePasswordRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "'current_password' is required.")]
    [JsonPropertyName("current_password")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false, ErrorMessage = "'password' is required.")]
    [MinLength(8, ErrorMessage = "'password' must be at least 8 characters.")]
    [RegularExpression(
    @"^(?=.*[A-Z])(?=.*[a-z])(?=.*\d)(?=.*[^a-zA-Z0-9]).+$",
    ErrorMessage = "'password' must contain at least 1 uppercase letter, 1 lowercase letter, 1 number, and 1 special character.")]
    [JsonPropertyName("new_password")]
    public string NewPassword { get; set; } = string.Empty;
}
