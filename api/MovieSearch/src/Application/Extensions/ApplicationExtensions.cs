using Application.Common;
using Asp.Versioning;
using Carter;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Application.Extensions;

public static class ApplicationExtensions
{
    /// <summary>
    /// Wires up everything the Application layer contributes to the host: Wolverine as
    /// the in-process CQRS mediator, Carter for the endpoint slices under Features/,
    /// and URL-segment API versioning (README §9, <c>/api/v1/...</c>).
    /// </summary>
    public static WebApplicationBuilder AddApplicationServices(this WebApplicationBuilder builder)
    {
        // Handlers live in this assembly, not the entry assembly, so Wolverine's
        // discovery has to include it explicitly.
        builder.Host.UseWolverine(options =>
            options.Discovery.IncludeAssembly(typeof(ApplicationExtensions).Assembly));

        builder.Services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = ApiVersions.V1;
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
            options.ApiVersionReader = new UrlSegmentApiVersionReader();
        });

        // Carter discovers the ICarterModule endpoint slices in Features/.
        builder.Services.AddCarter();

        return builder;
    }
}
