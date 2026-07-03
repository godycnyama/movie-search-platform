using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Settings;

public class OllamaSettings
{
    [Required]
    public string OllamaBaseUrl { get; set; } = string.Empty;
}
