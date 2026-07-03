using Api.Extensions;
using Api.Settings;
using Application.Extensions;
using Application.Responses;
using Carter;
using Infrastructure;
using Infrastructure.Extensions;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.AddObservability();
builder.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddApiServices(builder.Configuration);

builder.Services.AddOpenApi();

builder.Services.AddHealthChecks()
       .AddDbContextCheck<ApplicationDbContext>("postgres");

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseCors(CorsSettings.PolicyName);

app.UseAuthentication();
app.UseAuthorization();

app.MapCarter();

// Prometheus scrapes this endpoint (docker-compose monitoring/prometheus.yml).
app.MapPrometheusScrapingEndpoint();

// Liveness/readiness probe (README §9; docker-compose healthcheck curls this).
// Emits the HealthResponse contract: overall status + per-dependency status.
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = (context, report) =>
        context.Response.WriteAsJsonAsync(new HealthResponse
        {
            Status = report.Status.ToString(),
            Dependencies = report.Entries.ToDictionary(entry => entry.Key, entry => entry.Value.Status.ToString()),
        }),
});

app.Run();
