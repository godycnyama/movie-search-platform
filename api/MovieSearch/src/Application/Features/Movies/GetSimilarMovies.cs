using Application.Common;
using Application.Mappings;
using Application.Requests;
using Application.Responses;
using Application.Services;
using Carter;
using Domain.Errors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Application.Features.Movies;

/// <summary>Nearest neighbours of a movie (README §9, <c>GET /api/v1/movies/{id}/similar</c>).</summary>
public sealed record GetSimilarMoviesQuery(Guid Id, int TopK);

public static class GetSimilarMoviesHandler
{
    /// <summary>Returns <c>null</c> when the source movie does not exist (the endpoint maps that to 404).</summary>
    public static async Task<SimilarMoviesResponse?> Handle(
        GetSimilarMoviesQuery query,
        IMovieCatalogService movieCatalog,
        ICacheService cacheService,
        ILogger<GetSimilarMoviesQuery> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            // A null (unknown movie) result is never cached, so a movie added later is picked up immediately.
            return await cacheService.GetOrCreateAsync(
                CacheKeys.Similar(query.Id, query.TopK),
                async ct =>
                {
                    var hits = await movieCatalog.GetSimilarAsync(query.Id, query.TopK, ct);
                    if (hits is null)
                    {
                        return null;
                    }

                    return new SimilarMoviesResponse
                    {
                        SourceId = query.Id.ToString(),
                        Results = hits.Select(hit => hit.ToSimilarMovieDto()).ToList(),
                    };
                },
                CacheKeys.SimilarTtl,
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to fetch similar movies for {MovieId} (top_k {TopK})", query.Id, query.TopK);
            throw;
        }
    }
}

public sealed class GetSimilarMoviesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapApiGroup("Movies").MapGet("/movies/{id:guid}/similar", async (
                Guid id,
                [FromQuery(Name = "top_k")] int? topK,
                IMessageBus bus,
                CancellationToken cancellationToken) =>
            {
                var request = new SimilarMoviesRequest();
                if (topK is { } requestedTopK)
                {
                    request.TopK = requestedTopK;
                }

                if (RequestValidation.HasErrors(request, out var errors))
                {
                    return Results.ValidationProblem(errors);
                }

                var response = await bus.InvokeAsync<SimilarMoviesResponse?>(
                    new GetSimilarMoviesQuery(id, request.TopK), cancellationToken);

                return response is not null
                    ? Results.Ok(response)
                    : MovieErrors.NotFound(id.ToString()).ToProblem(StatusCodes.Status404NotFound);
            })
           .RequireAuthorization()
           .WithName("GetSimilarMovies")
           .WithTags("Movies")
           .Produces<SimilarMoviesResponse>()
           .ProducesValidationProblem()
           .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
