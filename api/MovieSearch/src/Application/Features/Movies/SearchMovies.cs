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

public sealed record SearchMoviesQuery(
    string Query,
    int TopK,
    string? Genre,
    double? MinImdbRating,
    string? MpaaRating,
    int? Decade);

public static class SearchMoviesHandler
{
    public static async Task<Result<SearchMoviesResponse>> Handle(
        SearchMoviesQuery query,
        IMovieCatalogService movieCatalog,
        ICacheService cacheService,
        ILogger<SearchMoviesQuery> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var cacheKey = CacheKeys.Search(query.Query, query.TopK, query.Genre, query.MinImdbRating, query.MpaaRating, query.Decade);

            var cached = await cacheService.GetAsync<SearchMoviesResponse>(cacheKey, cancellationToken);
            if (cached is not null)
            {
                return Result<SearchMoviesResponse>.Success(cached);
            }

            // The MCP server embeds the query and runs the pgvector search.
            var filters = new MovieSearchFilters(query.Genre, query.MinImdbRating, query.MpaaRating, query.Decade);
            var search = await movieCatalog.SearchAsync(query.Query, filters, query.TopK, cancellationToken);
            if (!search.IsSuccess)
            {
                return Result<SearchMoviesResponse>.Failure(search.Error!);
            }

            var hits = search.Value!;
            var response = new SearchMoviesResponse
            {
                Query = query.Query,
                Count = hits.Count,
                Results = hits.Select(hit => hit.ToSearchResultDto()).ToList(),
            };

            await cacheService.SetAsync(cacheKey, response, CacheKeys.SearchTtl, cancellationToken);
            return Result<SearchMoviesResponse>.Success(response);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Semantic search failed for query '{Query}' (top_k {TopK})", query.Query, query.TopK);
            return Result<SearchMoviesResponse>.Failure(Error.Unexpected);
        }
    }
}

public sealed class SearchMoviesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapApiGroup("Movies").MapGet("/movies/search", async (
                [FromQuery(Name = "q")] string? q,
                [FromQuery(Name = "top_k")] int? topK,
                [FromQuery(Name = "genre")] string? genre,
                [FromQuery(Name = "min_imdb_rating")] double? minImdbRating,
                [FromQuery(Name = "mpaa_rating")] string? mpaaRating,
                [FromQuery(Name = "decade")] int? decade,
                IMessageBus bus,
                CancellationToken cancellationToken) =>
            {
                var request = new SearchMoviesRequest
                {
                    Query = q ?? string.Empty,
                    Genre = genre,
                    MinImdbRating = minImdbRating,
                    MpaaRating = mpaaRating,
                    Decade = decade,
                };
                if (topK is { } requestedTopK)
                {
                    request.TopK = requestedTopK;
                }

                if (RequestValidation.HasErrors(request, out var errors))
                {
                    return Results.ValidationProblem(errors);
                }

                var result = await bus.InvokeAsync<Result<SearchMoviesResponse>>(
                    new SearchMoviesQuery(request.Query, request.TopK, request.Genre, request.MinImdbRating, request.MpaaRating, request.Decade),
                    cancellationToken);

                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : result.Error!.ToProblem(StatusCodes.Status500InternalServerError);
            })
           .RequireAuthorization()
           .WithName("SearchMovies")
           .WithTags("Movies")
           .Produces<SearchMoviesResponse>()
           .ProducesValidationProblem()
           .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
