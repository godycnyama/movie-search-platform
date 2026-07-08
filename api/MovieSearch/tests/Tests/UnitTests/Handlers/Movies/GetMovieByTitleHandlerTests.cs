using Application.Features.Movies;
using Application.Services;
using Domain.Common;
using Domain.Errors;
using Microsoft.Extensions.Logging.Abstractions;

namespace MovieSearch.Tests.UnitTests.Handlers.Movies;

/// <summary>
/// Covers <see cref="GetMovieByTitleHandler"/>: mapping a match and propagating the
/// title-not-found failure (which is never cached).
/// </summary>
public class GetMovieByTitleHandlerTests
{
    private readonly FakeMovieCatalogService _catalog = new();
    private readonly FakeCacheService _cache = new();

    private Task<Result<Application.Responses.MovieResponse>> Handle(string title) =>
        GetMovieByTitleHandler.Handle(new GetMovieByTitleQuery(title), _catalog, _cache,
            NullLogger<GetMovieByTitleQuery>.Instance, CancellationToken.None);

    [Fact]
    public async Task Handle_ReturnsAndCachesTheMatch()
    {
        _catalog.OnGetByTitle = () => Result<MovieCatalogItem>.Success(Fakes.SampleMovie());

        var first = await Handle("Interstellar");
        await Handle("Interstellar");

        first.IsSuccess.ShouldBeTrue();
        first.Value!.Title.ShouldBe("Interstellar");
        _catalog.GetByTitleCalls.ShouldBe(1); // repeat lookup served from cache
    }

    [Fact]
    public async Task Handle_PropagatesTitleNotFound()
    {
        _catalog.OnGetByTitle = () => Result<MovieCatalogItem>.Failure(MovieErrors.TitleNotFound("nope"));

        var result = await Handle("nope");

        result.IsSuccess.ShouldBeFalse();
        result.Error!.Code.ShouldBe("Movie.TitleNotFound");
        _cache.Writes.ShouldBe(0);
    }
}
