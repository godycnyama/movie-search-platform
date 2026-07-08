using Application.Features.Movies;
using Application.Services;
using Domain.Common;
using Domain.Errors;
using Microsoft.Extensions.Logging.Abstractions;

namespace MovieSearch.Tests.UnitTests.Handlers.Movies;

/// <summary>
/// Covers <see cref="SearchMoviesHandler"/>: mapping catalogue hits to the response,
/// serving repeat queries from cache, propagating a downstream failure (never cached),
/// forwarding the metadata filters, and mapping an unexpected exception to a 500 error.
/// </summary>
public class SearchMoviesHandlerTests
{
    private readonly FakeMovieCatalogService _catalog = new();
    private readonly FakeCacheService _cache = new();

    private static SearchMoviesQuery Query() => new("space adventure", 10, null, null, null, null);

    private Task<Result<Application.Responses.SearchMoviesResponse>> Handle(SearchMoviesQuery query) =>
        SearchMoviesHandler.Handle(query, _catalog, _cache, NullLogger<SearchMoviesQuery>.Instance, CancellationToken.None);

    [Fact]
    public async Task Handle_MapsCatalogueHitsToTheResponse()
    {
        _catalog.OnSearch = () => Result<IReadOnlyList<MovieCatalogItem>>.Success(new List<MovieCatalogItem>
        {
            Fakes.SampleMovie(title: "Interstellar", similarity: 0.96124),
            Fakes.SampleMovie(Guid.NewGuid(), "The Martian", 0.94),
        });

        var result = await Handle(Query());

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Query.ShouldBe("space adventure");
        result.Value.Count.ShouldBe(2);
        result.Value.Results[0].Title.ShouldBe("Interstellar");
        result.Value.Results[0].SimilarityScore.ShouldBe(0.9612); // rounded to 4 dp
        _cache.Writes.ShouldBe(1);
    }

    [Fact]
    public async Task Handle_ServesARepeatQueryFromCache()
    {
        _catalog.OnSearch = () => Result<IReadOnlyList<MovieCatalogItem>>.Success(
            new List<MovieCatalogItem> { Fakes.SampleMovie() });

        await Handle(Query());
        var second = await Handle(Query());

        second.IsSuccess.ShouldBeTrue();
        _catalog.SearchCalls.ShouldBe(1); // second call hit the cache
        _cache.Hits.ShouldBe(1);
    }

    [Fact]
    public async Task Handle_PassesTheMetadataFiltersThrough()
    {
        await Handle(new SearchMoviesQuery("space", 5, "Adventure", 7.5, "PG-13", 2010));

        _catalog.LastTopK.ShouldBe(5);
        _catalog.LastFilters.ShouldBe(new MovieSearchFilters("Adventure", 7.5, "PG-13", 2010));
    }

    [Fact]
    public async Task Handle_PropagatesADownstreamFailure_AndDoesNotCacheIt()
    {
        _catalog.OnSearch = () => Result<IReadOnlyList<MovieCatalogItem>>.Failure(MovieErrors.SearchFailed());

        var first = await Handle(Query());
        await Handle(Query());

        first.IsSuccess.ShouldBeFalse();
        first.Error!.Code.ShouldBe("Movie.SearchFailed");
        _cache.Writes.ShouldBe(0);
        _catalog.SearchCalls.ShouldBe(2); // failure was not cached, so it ran again
    }

    [Fact]
    public async Task Handle_MapsAnUnexpectedException_ToA500Error()
    {
        _catalog.OnSearch = () => throw new InvalidOperationException("boom");

        var result = await Handle(Query());

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe(Error.Unexpected);
    }
}
