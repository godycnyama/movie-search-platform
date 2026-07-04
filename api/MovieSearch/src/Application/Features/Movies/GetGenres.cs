using Application.Common;
using Application.Responses;
using Application.Services;
using Carter;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Application.Features.Movies;

/// <summary>Distinct genres in the catalogue (README §9, <c>GET /api/v1/movies/genres</c>).</summary>
public sealed record GetGenresQuery;

public static class GetGenresHandler
{
    public static async Task<GenresResponse> Handle(
        GetGenresQuery query,
        IMovieCatalogService movieCatalog,
        ICacheService cacheService,
        ILogger<GetGenresQuery> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            return await cacheService.GetOrCreateAsync(
                CacheKeys.Genres(),
                async ct =>
                {
                    var genres = await movieCatalog.GetGenresAsync(ct);
                    return new GenresResponse { Genres = genres.ToList() };
                },
                CacheKeys.GenresTtl,
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to fetch the genre list");
            throw;
        }
    }
}

public sealed class GetGenresEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapApiGroup("Movies").MapGet("/movies/genres", (IMessageBus bus, CancellationToken cancellationToken) =>
                bus.InvokeAsync<GenresResponse>(new GetGenresQuery(), cancellationToken))
           .RequireAuthorization()
           .WithName("GetGenres")
           .WithTags("Movies")
           .Produces<GenresResponse>();
    }
}
