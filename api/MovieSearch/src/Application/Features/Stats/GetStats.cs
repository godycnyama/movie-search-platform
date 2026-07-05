using Application.Common;
using Application.Responses;
using Application.Services;
using Carter;
using Domain.Common;
using Domain.Errors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Application.Features.Stats;

public sealed record GetStatsQuery;

public static class GetStatsHandler
{
    public static async Task<Result<StatsResponse>> Handle(
        GetStatsQuery query,
        IMovieCatalogService movieCatalog,
        ICacheService cacheService,
        ILogger<GetStatsQuery> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var cached = await cacheService.GetAsync<StatsResponse>(CacheKeys.Stats(), cancellationToken);
            if (cached is not null)
            {
                return Result<StatsResponse>.Success(cached);
            }

            var stats = await movieCatalog.GetStatisticsAsync(cancellationToken);
            if (!stats.IsSuccess)
            {
                return Result<StatsResponse>.Failure(stats.Error!);
            }

            var statistics = stats.Value!;
            var response = new StatsResponse
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

            await cacheService.SetAsync(CacheKeys.Stats(), response, CacheKeys.StatsTtl, cancellationToken);
            return Result<StatsResponse>.Success(response);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to compute dataset statistics");
            return Result<StatsResponse>.Failure(Error.Unexpected);
        }
    }
}

public sealed class GetStatsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapApiGroup("Stats").MapGet("/stats", async (IMessageBus bus, CancellationToken cancellationToken) =>
            {
                var result = await bus.InvokeAsync<Result<StatsResponse>>(new GetStatsQuery(), cancellationToken);

                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : result.Error!.ToProblem(StatusCodes.Status500InternalServerError);
            })
           .RequireAuthorization(AuthPolicies.AdminOnly) // README §9: stats is admin-only
           .WithName("GetStats")
           .WithTags("Stats")
           .Produces<StatsResponse>()
           .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
