using Application.Common;
using Application.Mappings;
using Application.Requests;
using Application.Responses;
using Application.Services;
using Carter;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Application.Features.Movies;

/// <summary>Natural-language semantic search (README §9, <c>GET /api/v1/movies/search</c>).</summary>
public sealed record SearchMoviesQuery(
    string Q,
    int TopK,
    string? Genre,
    double? MinImdbRating,
    string? MpaaRating,
    int? Decade);

public static class SearchMoviesHandler
{
    public static async Task<SearchMoviesResponse> Handle(
        SearchMoviesQuery query,
        IMovieCatalogService movieCatalog,
        ICacheService cacheService,
        ILogger<SearchMoviesQuery> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            return await cacheService.GetOrCreateAsync(
                CacheKeys.Search(query.Q, query.TopK, query.Genre, query.MinImdbRating, query.MpaaRating, query.Decade),
                async ct =>
                {
                    // The MCP server embeds the query and runs the pgvector search.
                    var filters = new MovieSearchFilters(query.Genre, query.MinImdbRating, query.MpaaRating, query.Decade);
                    var hits = await movieCatalog.SearchAsync(query.Q, filters, query.TopK, ct);

                    return new SearchMoviesResponse
                    {
                        Query = query.Q,
                        Count = hits.Count,
                        Results = hits.Select(hit => hit.ToSearchResultDto()).ToList(),
                    };
                },
                CacheKeys.SearchTtl,
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Semantic search failed for query '{Query}' (top_k {TopK})", query.Q, query.TopK);
            throw;
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
                    Q = q ?? string.Empty,
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

                var response = await bus.InvokeAsync<SearchMoviesResponse>(
                    new SearchMoviesQuery(request.Q, request.TopK, request.Genre, request.MinImdbRating, request.MpaaRating, request.Decade),
                    cancellationToken);

                return Results.Ok(response);
            })
           .RequireAuthorization()
           .WithName("SearchMovies")
           .WithTags("Movies")
           .Produces<SearchMoviesResponse>()
           .ProducesValidationProblem();
    }
}
