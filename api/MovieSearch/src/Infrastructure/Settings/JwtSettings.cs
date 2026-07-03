using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Settings;

/// <summary>
/// JWT issuing/validation settings. The signing key is a secret and must come from
/// environment variables (docker-compose `.env`) or user secrets — never committed config.
/// </summary>
public class JwtSettings
{
    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    /// <summary>Symmetric HMAC-SHA256 signing key; at least 32 bytes of randomness.</summary>
    [Required]
    [MinLength(32, ErrorMessage = "The JWT signing key must be at least 32 characters.")]
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>Token lifetime in minutes.</summary>
    [Range(5, 1440)]
    public int ExpiryMinutes { get; set; } = 60;
}
