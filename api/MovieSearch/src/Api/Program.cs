using Api.Extensions;
using Api.Settings;
using Application.Extensions;
using Application.Responses;
using Carter;
using Infrastructure;
using Infrastructure.Extensions;
using Infrastructure.Services;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.AddObservability();
builder.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddApiServices(builder.Configuration);
builder.Services.AddRateLimiting(builder.Configuration);
builder.Services.AddRequestTimeoutServices(builder.Configuration);

builder.Services.AddOpenApi();

builder.Services.AddHealthChecks()
       .AddDbContextCheck<ApplicationDbContext>("postgres")     // users/auth store
       .AddCheck<McpServerHealthCheck>("mcp-server");           // movie data source

var app = builder.Build();

app.MapOpenApi();

app.UseHttpsRedirection();

app.UseCors(CorsSettings.PolicyName);

app.UseAuthentication();
app.UseAuthorization();

// Rate limiter must run after authentication so the partitioner can read the JWT "sub" claim.
app.UseRateLimiter();

// Enforce the per-request wall-clock budget.
app.UseRequestTimeouts();

app.MapCarter();

// Prometheus scrapes this endpoint (docker-compose monitoring/prometheus.yml).
// Skipped from rate limiting so scrapers can't be starved by a noisy neighbour.
app.MapPrometheusScrapingEndpoint().DisableRateLimiting();

// Liveness/readiness probe.
// Emits the HealthResponse contract: overall status + per-dependency status.
// Skipped from rate limiting/timeouts so probes always get a fast, unmetered answer.
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = (context, report) =>
        context.Response.WriteAsJsonAsync(new HealthResponse
        {
            Status = report.Status.ToString(),
            Dependencies = report.Entries.ToDictionary(entry => entry.Key, entry => entry.Value.Status.ToString()),
        }),
}).DisableRateLimiting();

app.Run();
