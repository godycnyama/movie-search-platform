using System.ComponentModel.DataAnnotations;
using Application.Common;

namespace Api.Settings;

public class RateLimitSettings
{
    public const string PolicyName = EndpointPolicies.RateLimit;

    [Range(1, int.MaxValue, ErrorMessage = "'PermitLimit' must be at least 1.")]
    public int PermitLimit { get; set; } = 60;

    [Range(1, int.MaxValue, ErrorMessage = "'WindowSeconds' must be at least 1.")]
    public int WindowSeconds { get; set; } = 60;

    [Range(0, int.MaxValue, ErrorMessage = "'QueueLimit' must be zero or greater.")]
    public int QueueLimit { get; set; } = 0;
}
