using System.Text;
using Application.Common;
using Application.Repositories;
using Application.Services;
using Domain.Entities;
using Infrastructure.Repositories;
using Infrastructure.Services;
using Infrastructure.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Extensions;

public static class InfrastructureExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<PostgresSettings>()
                .BindConfiguration(nameof(PostgresSettings))
                .ValidateDataAnnotations()
                .ValidateOnStart();

        services.AddOptions<RedisSettings>()
                .BindConfiguration(nameof(RedisSettings))
                .ValidateDataAnnotations()
                .ValidateOnStart();

        // Bound from the McpSettings__* env vars (docker-compose). Without this the
        // IOptionsMonitor<McpSettings> injected into McpMovieCatalogService returns a
        // default instance with an empty ServerUrl, so the transport endpoint becomes
        // a relative URI ("/sse") and fails with "Endpoint must use HTTP or HTTPS scheme".
        services.AddOptions<McpSettings>()
                .BindConfiguration(nameof(McpSettings))
                .ValidateDataAnnotations()
                .ValidateOnStart();

        var postgresSettings = configuration.GetSection(nameof(PostgresSettings)).Get<PostgresSettings>()
            ?? throw new InvalidOperationException($"Missing '{nameof(PostgresSettings)}' configuration section.");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(postgresSettings.PostgresConnectionString)
                   .UseSnakeCaseNamingConvention(), // Pipeline-owned schema uses snake_case (README §6)
            contextLifetime: ServiceLifetime.Scoped,
            // Singleton options (they capture no scoped state) let Wolverine's codegen
            // construct the DbContext inline instead of falling back to service location,
            // which its default ServiceLocationPolicy.NotAllowed forbids.
            optionsLifetime: ServiceLifetime.Singleton);

        services.AddScoped<IUserRepository, UserRepository>();

        // Movie reads go through the MCP server, never straight to the movies tables.
        // The concrete type is also registered so McpServerHealthCheck can ping the
        // same shared session the endpoints use.
        services.AddSingleton<McpMovieCatalogService>();
        services.AddSingleton<IMovieCatalogService>(provider => provider.GetRequiredService<McpMovieCatalogService>());

        services.AddSingleton<ICacheService, CacheService>(); // Shares one Redis multiplexer app-wide
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();

        services.AddAuthServices(configuration);

        return services;
    }

    private static IServiceCollection AddAuthServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtSettings>()
                .BindConfiguration(nameof(JwtSettings))
                .ValidateDataAnnotations()
                .ValidateOnStart();

        var jwtSettings = configuration.GetSection(nameof(JwtSettings)).Get<JwtSettings>()
            ?? throw new InvalidOperationException($"Missing '{nameof(JwtSettings)}' configuration section.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    // Keep the raw JWT claim names ("sub", "role") instead of the
                    // legacy SOAP-style remapping; JwtTokenService issues the same names.
                    options.MapInboundClaims = false;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = jwtSettings.Issuer,
                        ValidateAudience = true,
                        ValidAudience = jwtSettings.Audience,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SigningKey)),
                        ValidateLifetime = true,
                        NameClaimType = "sub",
                        RoleClaimType = "role",
                    };
                });

        services.AddAuthorizationBuilder()
                .AddPolicy(AuthPolicies.AdminOnly, policy => policy.RequireRole(UserRoles.Admin));

        return services;
    }
}
