using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Infrastructure.Services;

/// <summary>
/// Health check for the MCP server — the API's movie data source. Round-trips an
/// MCP ping over the shared session, so it also detects a dead SSE stream.
/// </summary>
public sealed class McpServerHealthCheck(McpMovieCatalogService movieCatalog) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await movieCatalog.PingAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("MCP server unreachable", exception);
        }
    }
}
