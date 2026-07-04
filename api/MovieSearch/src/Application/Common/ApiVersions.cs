using Asp.Versioning;
using Asp.Versioning.Builder;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;

namespace Application.Common;

/// <summary>
/// Central definition of the API versions this service exposes. Endpoint slices map
/// their routes onto a versioned group so the literal <c>v1</c> in README §9 URLs is
/// resolved by <c>Asp.Versioning</c> (URL-segment reader) rather than hardcoded.
/// </summary>
internal static class ApiVersions
{
    public static readonly ApiVersion V1 = new(1);

    /// <summary>
    /// Creates a version-aware <c>/api/v{version}</c> route group for one API surface
    /// (<paramref name="apiName"/> groups the endpoints in OpenAPI, e.g. "Movies").
    /// The rate-limit (assessment §4.5: 60 req/min per user) and request-timeout
    /// (default 30s) policies are attached here so every public endpoint inherits them.
    /// </summary>
    public static RouteGroupBuilder MapApiGroup(this IEndpointRouteBuilder app, string apiName)
    {
        var group = app.NewVersionedApi(apiName)
                       .MapGroup("/api/v{version:apiVersion}")
                       .HasApiVersion(V1);

        // RequireRateLimiting / WithRequestTimeout return IEndpointConventionBuilder,
        // which would erase the RouteGroupBuilder type — call them for side effects
        // (they attach conventions to the group) and return the group itself.
        group.RequireRateLimiting(EndpointPolicies.RateLimit);
        group.WithRequestTimeout(EndpointPolicies.Timeout);

        return group;
    }
}
