using Api.Settings;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Api.Extensions;

public static class ApiExtensions
{
    /// <summary>
    /// OpenTelemetry traces and metrics. Traces are exported over OTLP (Jaeger in
    /// docker-compose) only when <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is configured;
    /// metrics are always exposed on <c>/metrics</c> for the Prometheus scraper.
    /// </summary>
    public static WebApplicationBuilder AddObservability(this WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<OtelSettings>()
               .BindConfiguration(nameof(OtelSettings))
               .ValidateDataAnnotations()
               .ValidateOnStart();

        var otelSettings = builder.Configuration.GetSection(nameof(OtelSettings)).Get<OtelSettings>() ?? new OtelSettings();

        // The standard OTel env vars (set by docker-compose) win over appsettings.
        var serviceName = builder.Configuration["OTEL_SERVICE_NAME"] ?? otelSettings.ServiceName;
        var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] ?? otelSettings.OtlpEndpoint;

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation()
                       .AddHttpClientInstrumentation()
                       .AddNpgsql()             // pgvector similarity queries
                       .AddSource("Wolverine"); // CQRS handler spans

                if (!string.IsNullOrEmpty(otlpEndpoint))
                {
                    tracing.AddOtlpExporter(otlp => otlp.Endpoint = new Uri(otlpEndpoint));
                }
            })
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                       .AddHttpClientInstrumentation()
                       .AddMeter("Wolverine*")  // Wolverine names its meter "Wolverine:<service>"
                       .AddPrometheusExporter();

                if (!string.IsNullOrEmpty(otlpEndpoint))
                {
                    metrics.AddOtlpExporter(otlp => otlp.Endpoint = new Uri(otlpEndpoint));
                }
            });

        return builder;
    }

    /// <summary>
    /// Wires up the Api project's own concerns — currently the CORS policy built
    /// from <see cref="CorsSettings"/> (environment-specific origins, empty by default).
    /// </summary>
    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<CorsSettings>()
                .BindConfiguration(nameof(CorsSettings))
                .ValidateDataAnnotations()
                .ValidateOnStart();

        var corsSettings = configuration.GetSection(nameof(CorsSettings)).Get<CorsSettings>()
            ?? throw new InvalidOperationException($"Missing '{nameof(CorsSettings)}' configuration section.");

        services.AddCors(options =>
            options.AddPolicy(CorsSettings.PolicyName, policy =>
                policy.WithOrigins(corsSettings.AllowedOrigins)
                      .AllowAnyHeader()
                      .AllowAnyMethod()));

        return services;
    }
}
