using System.ComponentModel.DataAnnotations;

namespace Api.Settings;

public class CorsSettings
{
    public const string PolicyName = "Frontend";

    [Required]
    public string[] AllowedOrigins { get; set; } = [];
}
