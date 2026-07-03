using System.Text.Json.Serialization;

namespace Application.Responses;

/// <summary>
/// Response for <c>POST /auth/token</c> (README §10).
/// Shape follows the standard OAuth 2.0 bearer-token response with an added <c>role</c> claim.
/// </summary>
public class TokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>Always <c>"Bearer"</c> for this API.</summary>
    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = "Bearer";

    /// <summary>Token lifetime in seconds.</summary>
    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    /// <summary>The role granted to the caller — <c>"reader"</c> or <c>"admin"</c>.</summary>
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;
}
