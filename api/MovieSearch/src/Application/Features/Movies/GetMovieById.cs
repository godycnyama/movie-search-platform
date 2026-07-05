using Application.Common;
using Application.Mappings;
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

namespace Application.Features.Movies;

public sealed record GetMovieByIdQuery(Guid Id);

public static class GetMovieByIdHandler
{
    public static async Task<Result<MovieResponse>> Handle(
        GetMovieByIdQuery query,
        IMovieCatalogService movieCatalog,
        ICacheService cacheService,
        ILogger<GetMovieByIdQuery> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var cacheKey = CacheKeys.Movie(query.Id);

            var cached = await cacheService.GetAsync<MovieResponse>(cacheKey, cancellationToken);
            if (cached is not null)
            {
                return Result<MovieResponse>.Success(cached);
            }

            var movie = await movieCatalog.GetByIdAsync(query.Id, cancellationToken);
            if (!movie.IsSuccess)
            {
                // Not-found (or a downstream failure) is never cached, so a movie added later is picked up immediately.
                return Result<MovieResponse>.Failure(movie.Error!);
            }

            var response = movie.Value!.ToMovieResponse();
            await cacheService.SetAsync(cacheKey, response, CacheKeys.MovieTtl, cancellationToken);
            return Result<MovieResponse>.Success(response);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to fetch movie {MovieId}", query.Id);
            return Result<MovieResponse>.Failure(Error.Unexpected);
        }
    }
}

public sealed class GetMovieByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapApiGroup("Movies").MapGet("/movies/{id:guid}", async (Guid id, IMessageBus bus, CancellationToken cancellationToken) =>
            {
                var result = await bus.InvokeAsync<Result<MovieResponse>>(new GetMovieByIdQuery(id), cancellationToken);

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
           .WithName("GetMovieById")
           .WithTags("Movies")
           .Produces<MovieResponse>()
           .ProducesProblem(StatusCodes.Status404NotFound)
           .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
