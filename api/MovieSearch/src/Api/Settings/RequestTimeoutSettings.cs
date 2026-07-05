using System.ComponentModel.DataAnnotations;
using Application.Common;

namespace Api.Settings;

public class RequestTimeoutSettings
{
    public const string PolicyName = EndpointPolicies.Timeout;

    [Range(1, 600, ErrorMessage = "'DefaultTimeoutSeconds' must be between 1 and 600.")]
    public int DefaultTimeoutSeconds { get; set; } = 30;
}
