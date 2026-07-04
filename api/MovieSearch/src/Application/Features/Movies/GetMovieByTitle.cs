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

/// <summary>
/// Full movie detail by exact or fuzzy title match (<c>GET /api/v1/movies/by-title</c>;
/// MCP tool <c>get_movie_by_title</c>).
/// </summary>
public sealed record GetMovieByTitleQuery(string Title);

public static class GetMovieByTitleHandler
{
    public static async Task<MovieResponse?> Handle(
        GetMovieByTitleQuery query,
        IMovieCatalogService movieCatalog,
        ICacheService cacheService,
        ILogger<GetMovieByTitleQuery> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            // A null (no match) result is never cached, so a movie added later is picked up immediately.
            return await cacheService.GetOrCreateAsync(
                CacheKeys.MovieByTitle(query.Title),
                async ct =>
                {
                    var movie = await movieCatalog.GetByTitleAsync(query.Title, ct);
                    return movie?.ToMovieResponse();
                },
                CacheKeys.MovieTtl,
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to fetch movie by title '{Title}'", query.Title);
            throw;
        }
    }
}

public sealed class GetMovieByTitleEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapApiGroup("Movies").MapGet("/movies/by-title", async (
                [FromQuery(Name = "title")] string? title,
                IMessageBus bus,
                CancellationToken cancellationToken) =>
            {
                var request = new MovieByTitleRequest { Title = title?.Trim() ?? string.Empty };

                if (RequestValidation.HasErrors(request, out var errors))
                {
                    return Results.ValidationProblem(errors);
                }

                var response = await bus.InvokeAsync<MovieResponse?>(
                    new GetMovieByTitleQuery(request.Title), cancellationToken);

                return response is not null
                    ? Results.Ok(response)
                    : MovieErrors.TitleNotFound(request.Title).ToProblem(StatusCodes.Status404NotFound);
            })
           .RequireAuthorization()
           .WithName("GetMovieByTitle")
           .WithTags("Movies")
           .Produces<MovieResponse>()
           .ProducesValidationProblem()
           .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
