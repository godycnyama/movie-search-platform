using System.ComponentModel.DataAnnotations;
using Application.Common;

namespace Api.Settings;

/// <summary>
/// Fixed-window rate-limit policy for the API, configurable via <c>appsettings.json</c>.
/// The assessment (§4.5) mandates <c>60 requests/minute per authenticated user</c>; those
/// defaults are set here so a fresh deployment complies out of the box, and can be tuned
/// per environment without a code change.
/// </summary>
public class RateLimitSettings
{
    /// <summary>Named policy applied to public endpoints (partitioned by user or IP).</summary>
    public const string PolicyName = EndpointPolicies.RateLimit;

    /// <summary>Maximum requests permitted per <see cref="WindowSeconds"/> per partition. Default 60.</summary>
    [Range(1, int.MaxValue, ErrorMessage = "'PermitLimit' must be at least 1.")]
    public int PermitLimit { get; set; } = 60;

    /// <summary>Length of the rolling window in seconds. Default 60 (i.e. per-minute limit).</summary>
    [Range(1, int.MaxValue, ErrorMessage = "'WindowSeconds' must be at least 1.")]
    public int WindowSeconds { get; set; } = 60;

    /// <summary>
    /// Extra requests to queue when the window is saturated. Default 0 — reject
    /// immediately so callers see 429 rather than piling up server-side latency.
    /// </summary>
    [Range(0, int.MaxValue, ErrorMessage = "'QueueLimit' must be zero or greater.")]
    public int QueueLimit { get; set; } = 0;
}
