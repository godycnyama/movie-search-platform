using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Settings;

public class McpSettings
{
    [Required]
    public string ServerUrl { get; set; } = string.Empty;

    [Required]
    public string Transport { get; set; } = "sse";
}
