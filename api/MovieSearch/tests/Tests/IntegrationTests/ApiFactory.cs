using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MovieSearch.Tests.IntegrationTests;

/// <summary>
/// Boots the real API in-memory via <see cref="WebApplicationFactory{TEntryPoint}"/>.
///
/// The API reads several settings eagerly while Program.cs registers services (the
/// JWT signing key, the DbContext connection string, etc.) — that happens *before*
/// <see cref="ConfigureWebHost"/> runs, so overriding via ConfigureAppConfiguration
/// is too late. The required values are therefore set as environment variables in
/// the constructor, which <c>WebApplication.CreateBuilder</c> reads when the host is
/// built. External dependencies are NOT reachable under test — the integration tests
/// here exercise the request pipeline (routing, auth, health-check wiring), not the
/// health of those dependencies.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public ApiFactory()
    {
        // "__" is the configuration hierarchy delimiter (Section__Key -> Section:Key).
        Environment.SetEnvironmentVariable(
            "PostgresSettings__PostgresConnectionString",
            "Host=localhost;Port=5432;Database=movies;Username=test;Password=test");
        Environment.SetEnvironmentVariable("McpSettings__ServerUrl", "http://localhost:8000");
        Environment.SetEnvironmentVariable("McpSettings__Transport", "sse");
        Environment.SetEnvironmentVariable("JwtSettings__Issuer", "movie-search-platform");
        Environment.SetEnvironmentVariable("JwtSettings__Audience", "movie-search-clients");
        Environment.SetEnvironmentVariable(
            "JwtSettings__SigningKey", "integration-tests-signing-key-at-least-32-bytes");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
    }
}
