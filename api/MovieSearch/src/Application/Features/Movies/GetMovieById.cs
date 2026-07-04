using Application.Common;
using Application.Mappings;
using Application.Responses;
using Application.Services;
using Carter;
using Domain.Errors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Application.Features.Movies;

/// <summary>Full movie detail by id (README §9, <c>GET /api/v1/movies/{id}</c>).</summary>
public sealed record GetMovieByIdQuery(Guid Id);

public static class GetMovieByIdHandler
{
    public static async Task<MovieResponse?> Handle(
        GetMovieByIdQuery query,
        IMovieCatalogService movieCatalog,
        ICacheService cacheService,
        ILogger<GetMovieByIdQuery> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            // A null (not found) result is never cached, so a movie added later is picked up immediately.
            return await cacheService.GetOrCreateAsync(
                CacheKeys.Movie(query.Id),
                async ct =>
                {
                    var movie = await movieCatalog.GetByIdAsync(query.Id, ct);
                    return movie?.ToMovieResponse();
                },
                CacheKeys.MovieTtl,
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to fetch movie {MovieId}", query.Id);
            throw;
        }
    }
}

public sealed class GetMovieByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapApiGroup("Movies").MapGet("/movies/{id:guid}", async (Guid id, IMessageBus bus, CancellationToken cancellationToken) =>
                await bus.InvokeAsync<MovieResponse?>(new GetMovieByIdQuery(id), cancellationToken) is { } movie
                    ? Results.Ok(movie)
                    : MovieErrors.NotFound(id.ToString()).ToProblem(StatusCodes.Status404NotFound))
           .RequireAuthorization()
           .WithName("GetMovieById")
           .WithTags("Movies")
           .Produces<MovieResponse>()
           .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
