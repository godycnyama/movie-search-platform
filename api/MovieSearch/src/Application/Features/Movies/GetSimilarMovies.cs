using Application.Common;
using Application.Mappings;
using Application.Requests;
using Application.Responses;
using Application.Services;
using Carter;
using Domain.Common;
using Domain.Errors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Application.Features.Movies;

public sealed record GetSimilarMoviesQuery(Guid Id, int TopK);

public static class GetSimilarMoviesHandler
{
    public static async Task<Result<SimilarMoviesResponse>> Handle(
        GetSimilarMoviesQuery query,
        IMovieCatalogService movieCatalog,
        ICacheService cacheService,
        ILogger<GetSimilarMoviesQuery> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var cacheKey = CacheKeys.Similar(query.Id, query.TopK);

            var cached = await cacheService.GetAsync<SimilarMoviesResponse>(cacheKey, cancellationToken);
            if (cached is not null)
            {
                return Result<SimilarMoviesResponse>.Success(cached);
            }

            var similar = await movieCatalog.GetSimilarAsync(query.Id, query.TopK, cancellationToken);
            if (!similar.IsSuccess)
            {
                // An unknown movie (or a downstream failure) is never cached.
                return Result<SimilarMoviesResponse>.Failure(similar.Error!);
            }

            var response = new SimilarMoviesResponse
            {
                SourceId = query.Id.ToString(),
                Results = similar.Value!.Select(hit => hit.ToSimilarMovieDto()).ToList(),
            };

            await cacheService.SetAsync(cacheKey, response, CacheKeys.SimilarTtl, cancellationToken);
            return Result<SimilarMoviesResponse>.Success(response);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to fetch similar movies for {MovieId} (top_k {TopK})", query.Id, query.TopK);
            return Result<SimilarMoviesResponse>.Failure(Error.Unexpected);
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

                var result = await bus.InvokeAsync<Result<SimilarMoviesResponse>>(
                    new GetSimilarMoviesQuery(id, request.TopK), cancellationToken);

                if (result.IsSuccess)
                {
                    return Results.Ok(result.Value);
                }

                var statusCode = result.Error!.Code == MovieErrors.NotFound(id.ToString()).Code
                    ? StatusCodes.Status404NotFound
                    : StatusCodes.Status500InternalServerError;

                return result.Error!.ToProblem(statusCode);
            })
           .RequireAuthorization()
           .WithName("GetSimilarMovies")
           .WithTags("Movies")
           .Produces<SimilarMoviesResponse>()
           .ProducesValidationProblem()
           .ProducesProblem(StatusCodes.Status404NotFound)
           .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
