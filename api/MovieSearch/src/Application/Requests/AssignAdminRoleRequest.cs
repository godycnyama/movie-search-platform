using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Application.Requests;

public class AssignAdminRoleRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "'email' is required.")]
    [EmailAddress(ErrorMessage = "'email' must be a valid email address.")]
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;
}
