using Api.Settings;
using Microsoft.AspNetCore.Http.Timeouts;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;

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
                       .AddNpgsql()
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

    /// <summary>
    /// Fixed-window rate limiting (assessment §4.5: 60 req/min per authenticated user).
    /// The partition key is the JWT <c>sub</c> claim when the caller is authenticated,
    /// otherwise the remote IP — that way the login endpoint is still protected against
    /// credential stuffing. Register <c>UseRateLimiter</c> after auth so claims are visible.
    /// </summary>
    public static IServiceCollection AddRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RateLimitSettings>()
                .BindConfiguration(nameof(RateLimitSettings))
                .ValidateDataAnnotations()
                .ValidateOnStart();

        var settings = configuration.GetSection(nameof(RateLimitSettings)).Get<RateLimitSettings>() ?? new RateLimitSettings();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Emit RFC 6585 Retry-After so clients can back off politely.
            options.OnRejected = (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString(NumberFormatInfo.InvariantInfo);
                }

                return ValueTask.CompletedTask;
            };

            options.AddPolicy(RateLimitSettings.PolicyName, httpContext =>
            {
                var partitionKey = httpContext.User.FindFirstValue("sub")
                                   ?? httpContext.Connection.RemoteIpAddress?.ToString()
                                   ?? "anonymous";

                return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = settings.PermitLimit,
                    Window = TimeSpan.FromSeconds(settings.WindowSeconds),
                    QueueLimit = settings.QueueLimit,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    AutoReplenishment = true,
                });
            });
        });

        return services;
    }

    /// <summary>
    /// Configurable request-timeout middleware (assessment §4.5: default 30s). Cancels
    /// handlers that exceed the wall-clock budget, returning HTTP 504 so a hung downstream
    /// cannot exhaust ASP.NET Core threads. The p95 target of &lt; 500ms (README §11) stays
    /// the SLO; this policy is the pathological-case safety net.
    /// </summary>
    public static IServiceCollection AddRequestTimeoutServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RequestTimeoutSettings>()
                .BindConfiguration(nameof(RequestTimeoutSettings))
                .ValidateDataAnnotations()
                .ValidateOnStart();

        var settings = configuration.GetSection(nameof(RequestTimeoutSettings)).Get<RequestTimeoutSettings>() ?? new RequestTimeoutSettings();

        services.AddRequestTimeouts(options =>
        {
            options.DefaultPolicy = new RequestTimeoutPolicy
            {
                Timeout = TimeSpan.FromSeconds(settings.DefaultTimeoutSeconds),
                TimeoutStatusCode = StatusCodes.Status504GatewayTimeout,
            };

            options.AddPolicy(RequestTimeoutSettings.PolicyName, TimeSpan.FromSeconds(settings.DefaultTimeoutSeconds));
        });

        return services;
    }
}
