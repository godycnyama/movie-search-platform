using Infrastructure.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pgvector.EntityFrameworkCore;

namespace Infrastructure.Extensions;

public static class InfrastructureExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<InfrastructureSettings>()
                 .BindConfiguration(nameof(InfrastructureSettings))
                 .ValidateDataAnnotations()
                 .ValidateOnStart();

        var infrastructureSettings = configuration.GetSection(nameof(InfrastructureSettings)).Get<InfrastructureSettings>()
            ?? throw new InvalidOperationException($"Missing '{nameof(InfrastructureSettings)}' configuration section.");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(
                infrastructureSettings.PostgresConnectionString,
                npgsqlOptions => npgsqlOptions.UseVector())); // Maps the Vector type

        return services;
    }
}
