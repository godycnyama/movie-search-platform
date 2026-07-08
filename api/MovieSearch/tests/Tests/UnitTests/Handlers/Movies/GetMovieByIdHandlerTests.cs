using Application.Features.Movies;
using Application.Services;
using Domain.Common;
using Domain.Errors;
using Microsoft.Extensions.Logging.Abstractions;

namespace MovieSearch.Tests.UnitTests.Handlers.Movies;

/// <summary>
/// Covers <see cref="GetMovieByIdHandler"/>: mapping a hit, caching repeat lookups, and
/// propagating a not-found failure without caching it.
/// </summary>
public class GetMovieByIdHandlerTests
{
    private readonly FakeMovieCatalogService _catalog = new();
    private readonly FakeCacheService _cache = new();

    private Task<Result<Application.Responses.MovieResponse>> Handle(Guid id) =>
        GetMovieByIdHandler.Handle(new GetMovieByIdQuery(id), _catalog, _cache,
            NullLogger<GetMovieByIdQuery>.Instance, CancellationToken.None);

    [Fact]
    public async Task Handle_ReturnsAndCachesTheMovie()
    {
        _catalog.OnGetById = () => Result<MovieCatalogItem>.Success(Fakes.SampleMovie());

        var first = await Handle(Fakes.SampleId);
        var second = await Handle(Fakes.SampleId);

        first.IsSuccess.ShouldBeTrue();
        first.Value!.Id.ShouldBe(Fakes.SampleId.ToString());
        first.Value.Title.ShouldBe("Interstellar");
        second.IsSuccess.ShouldBeTrue();
        _catalog.GetByIdCalls.ShouldBe(1); // second lookup served from cache
    }

    [Fact]
    public async Task Handle_PropagatesNotFound_AndDoesNotCacheIt()
    {
        _catalog.OnGetById = () => Result<MovieCatalogItem>.Failure(MovieErrors.NotFound(Fakes.SampleId.ToString()));

        var first = await Handle(Fakes.SampleId);
        await Handle(Fakes.SampleId);

        first.IsSuccess.ShouldBeFalse();
        first.Error!.Code.ShouldBe("Movie.NotFound");
        _cache.Writes.ShouldBe(0);
        _catalog.GetByIdCalls.ShouldBe(2); // not-found is re-checked, not cached
    }
}
