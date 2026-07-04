using System.ComponentModel.DataAnnotations;
using Application.Common;

namespace Api.Settings;

/// <summary>
/// Per-request timeout policy for the API. The assessment (§4.5) mandates a
/// <c>configurable request timeout, default 30s</c>. This safety net cancels handlers
/// that exceed the budget so a hung downstream (Ollama, MCP server, pgvector) cannot
/// tie up the ASP.NET Core thread pool; the target p95 remains &lt; 500ms (README §11).
/// </summary>
public class RequestTimeoutSettings
{
    /// <summary>Named timeout policy attached to public endpoints.</summary>
    public const string PolicyName = EndpointPolicies.Timeout;

    /// <summary>Maximum wall-clock seconds a request may take. Default 30 per the assessment.</summary>
    [Range(1, 600, ErrorMessage = "'DefaultTimeoutSeconds' must be between 1 and 600.")]
    public int DefaultTimeoutSeconds { get; set; } = 30;
}
