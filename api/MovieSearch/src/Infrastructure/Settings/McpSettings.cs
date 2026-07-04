using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Settings;

/// <summary>
/// Connection settings for the platform's MCP server — the API's only source of
/// movie data (the movies tables are never queried directly; see
/// <c>docker-compose.yml</c> and README §9).
/// </summary>
public class McpSettings
{
    /// <summary>Base URL of the MCP server, e.g. <c>http://mcp-server:8000</c>.</summary>
    [Required]
    public string ServerUrl { get; set; } = string.Empty;

    /// <summary>
    /// MCP HTTP transport; must match the server's <c>MCP_TRANSPORT</c>:
    /// <c>sse</c> (local default) or <c>streamable-http</c> (production).
    /// </summary>
    [Required]
    [RegularExpression("^(sse|streamable-http)$")]
    public string Transport { get; set; } = "sse";
}
