using Application.Features.Movies;
using Application.Services;
using Domain.Common;
using Domain.Errors;
using Microsoft.Extensions.Logging.Abstractions;

namespace MovieSearch.Tests.UnitTests.Handlers.Movies;

/// <summary>
/// Covers <see cref="GetSimilarMoviesHandler"/>: mapping results (with the source id
/// echoed back), caching, and propagating a not-found source.
/// </summary>
public class GetSimilarMoviesHandlerTests
{
    private readonly FakeMovieCatalogService _catalog = new();
    private readonly FakeCacheService _cache = new();

    private Task<Result<Application.Responses.SimilarMoviesResponse>> Handle(Guid id, int topK) =>
        GetSimilarMoviesHandler.Handle(new GetSimilarMoviesQuery(id, topK), _catalog, _cache,
            NullLogger<GetSimilarMoviesQuery>.Instance, CancellationToken.None);

    [Fact]
    public async Task Handle_MapsResults_AndEchoesTheSourceId()
    {
        _catalog.OnGetSimilar = () => Result<IReadOnlyList<MovieCatalogItem>>.Success(new List<MovieCatalogItem>
        {
            Fakes.SampleMovie(Guid.NewGuid(), "The Martian", 0.95012),
            Fakes.SampleMovie(Guid.NewGuid(), "Gravity", 0.92),
        });

        var result = await Handle(Fakes.SampleId, 5);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.SourceId.ShouldBe(Fakes.SampleId.ToString());
        result.Value.Results.Count.ShouldBe(2);
        result.Value.Results[0].Title.ShouldBe("The Martian");
        result.Value.Results[0].SimilarityScore.ShouldBe(0.9501); // rounded to 4 dp
        _catalog.LastTopK.ShouldBe(5);
        _cache.Writes.ShouldBe(1);
    }

    [Fact]
    public async Task Handle_ServesRepeatRequestsFromCache()
    {
        _catalog.OnGetSimilar = () => Result<IReadOnlyList<MovieCatalogItem>>.Success(
            new List<MovieCatalogItem> { Fakes.SampleMovie() });

        await Handle(Fakes.SampleId, 5);
        await Handle(Fakes.SampleId, 5);

        _catalog.GetSimilarCalls.ShouldBe(1);
    }

    [Fact]
    public async Task Handle_PropagatesNotFound_WhenTheSourceIsUnknown()
    {
        _catalog.OnGetSimilar = () => Result<IReadOnlyList<MovieCatalogItem>>.Failure(
            MovieErrors.NotFound(Fakes.SampleId.ToString()));

        var result = await Handle(Fakes.SampleId, 5);

        result.IsSuccess.ShouldBeFalse();
        result.Error!.Code.ShouldBe("Movie.NotFound");
        _cache.Writes.ShouldBe(0);
    }
}
