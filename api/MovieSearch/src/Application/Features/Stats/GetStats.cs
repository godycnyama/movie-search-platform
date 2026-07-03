using Application.Common;
using Application.Repositories;
using Application.Responses;
using Application.Services;
using Carter;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Application.Features.Stats;

/// <summary>Dataset statistics (README §9, <c>GET /api/v1/stats</c>). Admin-only once auth lands.</summary>
public sealed record GetStatsQuery;

public static class GetStatsHandler
{
    public static async Task<StatsResponse> Handle(
        GetStatsQuery query,
        IMovieRepository movieRepository,
        ICacheService cacheService,
        ILogger<GetStatsQuery> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            return await cacheService.GetOrCreateAsync(
                CacheKeys.Stats(),
                async ct =>
                {
                    var statistics = await movieRepository.GetStatisticsAsync(ct);

                    return new StatsResponse
                    {
                        TotalMovies = statistics.TotalMovies,
                        WithEmbeddings = statistics.WithEmbeddings,
                        Genres = statistics.GenreCount,
                        YearRange = statistics is { MinReleaseYear: { } minYear, MaxReleaseYear: { } maxYear }
                            ? [minYear, maxYear]
                            : [],
                        AvgImdbRating = statistics.AverageImdbRating is { } avgRating ? Math.Round(avgRating, 2) : null,
                        PipelineVersion = statistics.PipelineVersion,
                    };
                },
                CacheKeys.StatsTtl,
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to compute dataset statistics");
            throw;
        }
    }
}

public sealed class GetStatsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapApiGroup("Stats").MapGet("/stats", (IMessageBus bus, CancellationToken cancellationToken) =>
                bus.InvokeAsync<StatsResponse>(new GetStatsQuery(), cancellationToken))
           .RequireAuthorization(AuthPolicies.AdminOnly) // README §9: stats is admin-only
           .WithName("GetStats")
           .WithTags("Stats")
           .Produces<StatsResponse>();
    }
}
