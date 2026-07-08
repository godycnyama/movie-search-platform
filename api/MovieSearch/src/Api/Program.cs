using Api.Extensions;
using Api.Middleware;
using Api.Settings;
using Application.Extensions;
using Application.Responses;
using Carter;
using Infrastructure;
using Infrastructure.Extensions;
using Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.OpenApi;
using Scalar.AspNetCore;


var builder = WebApplication.CreateBuilder(args);

builder.AddObservability();
builder.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddApiServices(builder.Configuration);
builder.Services.AddRateLimiting(builder.Configuration);
builder.Services.AddRequestTimeoutServices(builder.Configuration);
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Name = "Authorization",
            Description = "Enter your JWT token"
        };
        return Task.CompletedTask;
    });

    // Apply the Bearer requirement to every operation except those marked
    // [AllowAnonymous] (e.g. /auth/login, /auth/signup), so Scalar doesn't show
    // an "Authentication required" lock on endpoints that don't need a token.
    options.AddOperationTransformer((operation, context, cancellationToken) =>
    {
        if (context.Description.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any())
        {
            return Task.CompletedTask;
        }

        operation.Security ??= new List<OpenApiSecurityRequirement>();
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = []
        });
        return Task.CompletedTask;
    });
});

builder.Services.AddHealthChecks()
       .AddDbContextCheck<ApplicationDbContext>("postgres")     // users/auth store
       .AddCheck<McpServerHealthCheck>("mcp-server");           // movie data source

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

var app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

// Catches unhandled exceptions and converts them to a ProblemDetails response.
app.UseExceptionHandler();

// Converts empty-body error responses (404, 401, 403, 405, etc.) into ProblemDetails too.
app.UseStatusCodePages();

app.MapOpenApi();

app.MapScalarApiReference(options =>
{
    options.WithTitle("Movie Search API")
        .WithTheme(ScalarTheme.Moon)
        .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
});

// Land on the interactive API docs when hitting the root.
app.MapGet("/", () => Results.Redirect("/scalar")).ExcludeFromDescription();

app.UseHsts();
app.UseResponseCompression();

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

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false // no dependency checks, just "is the process running"
}).DisableRateLimiting();

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    ResponseWriter = (context, report) =>
        context.Response.WriteAsJsonAsync(new HealthResponse
        {
            Status = report.Status.ToString(),
            Dependencies = report.Entries.ToDictionary(e => e.Key, e => e.Value.Status.ToString()),
        }),
}).DisableRateLimiting();

app.Run();
