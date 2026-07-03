using System.ComponentModel.DataAnnotations;

namespace Api.Settings;

/// <summary>
/// Cross-origin settings for browser clients. Origins are environment-specific
/// configuration (appsettings / env vars), never hardcoded; an empty list means
/// no cross-origin access is granted.
/// </summary>
public class CorsSettings
{
    /// <summary>Name of the CORS policy built from these settings.</summary>
    public const string PolicyName = "Frontend";

    /// <summary>Origins allowed to call the API from a browser, e.g. "https://app.example.com".</summary>
    [Required]
    public string[] AllowedOrigins { get; set; } = [];
}
