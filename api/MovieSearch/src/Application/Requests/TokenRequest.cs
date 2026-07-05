using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Application.Requests;

public class TokenRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "'client_id' is required.")]
    [JsonPropertyName("client_id")]
    public string ClientId { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false, ErrorMessage = "'client_secret' is required.")]
    [JsonPropertyName("client_secret")]
    public string ClientSecret { get; set; } = string.Empty;
}
