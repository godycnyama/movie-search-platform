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

public sealed record GetMovieByTitleQuery(string Title);

public static class GetMovieByTitleHandler
{
    public static async Task<Result<MovieResponse>> Handle(
        GetMovieByTitleQuery query,
        IMovieCatalogService movieCatalog,
        ICacheService cacheService,
        ILogger<GetMovieByTitleQuery> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var cacheKey = CacheKeys.MovieByTitle(query.Title);

            var cached = await cacheService.GetAsync<MovieResponse>(cacheKey, cancellationToken);
            if (cached is not null)
            {
                return Result<MovieResponse>.Success(cached);
            }

            var movie = await movieCatalog.GetByTitleAsync(query.Title, cancellationToken);
            if (!movie.IsSuccess)
            {
                // No match (or a downstream failure) is never cached, so a movie added later is picked up immediately.
                return Result<MovieResponse>.Failure(movie.Error!);
            }

            var response = movie.Value!.ToMovieResponse();
            await cacheService.SetAsync(cacheKey, response, CacheKeys.MovieTtl, cancellationToken);
            return Result<MovieResponse>.Success(response);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to fetch movie by title '{Title}'", query.Title);
            return Result<MovieResponse>.Failure(Error.Unexpected);
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

                var result = await bus.InvokeAsync<Result<MovieResponse>>(
                    new GetMovieByTitleQuery(request.Title), cancellationToken);

                if (result.IsSuccess)
                {
                    return Results.Ok(result.Value);
                }

                var statusCode = result.Error!.Code == MovieErrors.TitleNotFound(request.Title).Code
                    ? StatusCodes.Status404NotFound
                    : StatusCodes.Status500InternalServerError;

                return result.Error!.ToProblem(statusCode);
            })
           .RequireAuthorization()
           .WithName("GetMovieByTitle")
           .WithTags("Movies")
           .Produces<MovieResponse>()
           .ProducesValidationProblem()
           .ProducesProblem(StatusCodes.Status404NotFound)
           .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
