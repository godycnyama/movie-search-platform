using Asp.Versioning;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Application.Common;

internal static class ApiVersions
{
    public static readonly ApiVersion V1 = new(1);

    public static RouteGroupBuilder MapApiGroup(this IEndpointRouteBuilder app, string apiName)
    {
        var group = app.NewVersionedApi(apiName)
                       .MapGroup("/api/v{version:apiVersion}")
                       .HasApiVersion(V1);
        group.RequireRateLimiting(EndpointPolicies.RateLimit);
        group.WithRequestTimeout(EndpointPolicies.Timeout);

        return group;
    }
}
