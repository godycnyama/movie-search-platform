using System.ComponentModel.DataAnnotations;

namespace Api.Settings;

/// <summary>
/// OpenTelemetry settings. The standard OTel environment variables
/// (<c>OTEL_SERVICE_NAME</c>, <c>OTEL_EXPORTER_OTLP_ENDPOINT</c>) — which
/// docker-compose sets — take precedence over these values when present.
/// </summary>
public class OtelSettings
{
    /// <summary>Logical service name attached to all traces and metrics.</summary>
    [Required]
    public string ServiceName { get; set; } = "movie-search-api";

    /// <summary>
    /// OTLP collector endpoint (e.g. "http://jaeger:4317"). When neither this nor
    /// <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is set, the OTLP exporters are not registered
    /// (metrics remain available on <c>/metrics</c>).
    /// </summary>
    [Url]
    public string? OtlpEndpoint { get; set; }
}
