using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace MovieSearch.Tests.IntegrationTests;

/// <summary>
/// End-to-end smoke tests over the booted API: they verify the request pipeline is
/// wired correctly (routing, health-check contract, JWT authorization) without any
/// external infrastructure. Dependencies report unhealthy under test — that is
/// expected; these assertions target the wiring, not dependency health.
/// </summary>
public sealed class HealthEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Readiness_endpoint_returns_the_health_contract()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        // Shape matches HealthResponse: overall status + per-dependency statuses.
        body.TryGetProperty("status", out _).ShouldBeTrue();
        body.TryGetProperty("dependencies", out var dependencies).ShouldBeTrue();
        dependencies.TryGetProperty("postgres", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Liveness_endpoint_reports_the_process_is_up()
    {
        var client = factory.CreateClient();

        // Liveness runs no dependency checks, so it is Healthy even when Postgres/MCP are unreachable.
        var response = await client.GetAsync("/health/live");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Protected_endpoint_returns_401_without_a_token()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/movies/genres");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
