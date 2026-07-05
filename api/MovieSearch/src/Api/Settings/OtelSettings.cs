using System.ComponentModel.DataAnnotations;

namespace Api.Settings;

public class OtelSettings
{
    [Required]
    public string ServiceName { get; set; } = "movie-search-api";

    [Url]
    public string? OtlpEndpoint { get; set; }
}
