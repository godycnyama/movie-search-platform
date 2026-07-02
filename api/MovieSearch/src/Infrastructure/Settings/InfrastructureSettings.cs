using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Settings;

public class InfrastructureSettings
{
    [Required]
    public string PostgresConnectionString { get; set; } = string.Empty;

    [Required]
    public string OllamaBaseUrl { get; set; } = string.Empty;
}
