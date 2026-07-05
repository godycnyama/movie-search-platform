using Application.Common;
using Asp.Versioning;
using Carter;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Application.Extensions;

public static class ApplicationExtensions
{
    public static WebApplicationBuilder AddApplicationServices(this WebApplicationBuilder builder)
    {
        // Register Wolverine handlers
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
