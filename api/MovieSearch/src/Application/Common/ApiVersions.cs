using Asp.Versioning;
using Asp.Versioning.Builder;
using Microsoft.AspNetCore.Builder;
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
    /// </summary>
    public static RouteGroupBuilder MapApiGroup(this IEndpointRouteBuilder app, string apiName)
    {
        return app.NewVersionedApi(apiName)
                  .MapGroup("/api/v{version:apiVersion}")
                  .HasApiVersion(V1);
    }
}
