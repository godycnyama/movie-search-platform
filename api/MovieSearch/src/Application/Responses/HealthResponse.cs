using System.Text.Json.Serialization;

namespace Application.Responses;

/// <summary>
/// Response for <c>GET /health</c> (README §9). Aggregates the API's own status with
/// per-dependency health. Dependency values are typically <c>"Healthy"</c>,
/// <c>"Degraded"</c>, or <c>"Unhealthy"</c>.
/// </summary>
public class HealthResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Downstream dependencies keyed by service name (e.g. <c>mcp-server</c>, <c>postgres</c>).
    /// A dictionary keeps the shape flexible as new dependencies are added.
    /// </summary>
    [JsonPropertyName("dependencies")]
    public Dictionary<string, string> Dependencies { get; set; } = new();
}
