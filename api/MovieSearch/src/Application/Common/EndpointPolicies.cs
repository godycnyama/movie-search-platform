namespace Application.Common;

/// <summary>
/// Well-known policy names attached to public endpoints. Defined here (rather than in
/// <c>Api.Settings</c>) so Carter modules in this project can reference them without
/// taking a dependency on the composition-root <c>Api</c> project.
/// </summary>
public static class EndpointPolicies
{
    /// <summary>
    /// Rate-limit policy applied to every public endpoint (assessment §4.5:
    /// 60 requests/minute per authenticated user). Registered in <c>ApiExtensions</c>.
    /// </summary>
    public const string RateLimit = "AuthenticatedUser";

    /// <summary>
    /// Request-timeout policy applied to every public endpoint (assessment §4.5:
    /// configurable, default 30s). Registered in <c>ApiExtensions</c>.
    /// </summary>
    public const string Timeout = "Default";
}
