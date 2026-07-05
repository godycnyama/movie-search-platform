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

namespace Application.Features.Movies;

public sealed record GetGenresQuery;

public static class GetGenresHandler
{
    public static async Task<Result<GenresResponse>> Handle(
        GetGenresQuery query,
        IMovieCatalogService movieCatalog,
        ICacheService cacheService,
        ILogger<GetGenresQuery> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var cached = await cacheService.GetAsync<GenresResponse>(CacheKeys.Genres(), cancellationToken);
            if (cached is not null)
            {
                return Result<GenresResponse>.Success(cached);
            }

            var genres = await movieCatalog.GetGenresAsync(cancellationToken);
            if (!genres.IsSuccess)
            {
                return Result<GenresResponse>.Failure(genres.Error!);
            }

            var response = new GenresResponse { Genres = genres.Value!.ToList() };
            await cacheService.SetAsync(CacheKeys.Genres(), response, CacheKeys.GenresTtl, cancellationToken);
            return Result<GenresResponse>.Success(response);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to fetch the genre list");
            return Result<GenresResponse>.Failure(Error.Unexpected);
        }
    }
}

public sealed class GetGenresEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapApiGroup("Movies").MapGet("/movies/genres", async (IMessageBus bus, CancellationToken cancellationToken) =>
            {
                var result = await bus.InvokeAsync<Result<GenresResponse>>(new GetGenresQuery(), cancellationToken);

                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : result.Error!.ToProblem(StatusCodes.Status500InternalServerError);
            })
           .RequireAuthorization()
           .WithName("GetGenres")
           .WithTags("Movies")
           .Produces<GenresResponse>()
           .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
