using Application.Features.Movies;
using Domain.Common;
using Domain.Errors;
using Microsoft.Extensions.Logging.Abstractions;

namespace MovieSearch.Tests.UnitTests.Handlers.Movies;

/// <summary>
/// Covers <see cref="GetGenresHandler"/>: returning and caching the genre list, and
/// propagating a downstream failure.
/// </summary>
public class GetGenresHandlerTests
{
    private readonly FakeMovieCatalogService _catalog = new();
    private readonly FakeCacheService _cache = new();

    private Task<Result<Application.Responses.GenresResponse>> Handle() =>
        GetGenresHandler.Handle(new GetGenresQuery(), _catalog, _cache,
            NullLogger<GetGenresQuery>.Instance, CancellationToken.None);

    [Fact]
    public async Task Handle_ReturnsAndCachesTheGenres()
    {
        _catalog.OnGetGenres = () => Result<IReadOnlyList<string>>.Success(new List<string> { "Action", "Drama" });

        var first = await Handle();
        await Handle();

        first.IsSuccess.ShouldBeTrue();
        first.Value!.Genres.ShouldBe(new[] { "Action", "Drama" });
        _catalog.GetGenresCalls.ShouldBe(1); // second call served from cache
    }

    [Fact]
    public async Task Handle_PropagatesADownstreamFailure()
    {
        _catalog.OnGetGenres = () => Result<IReadOnlyList<string>>.Failure(MovieErrors.McpServerUnavailable());

        var result = await Handle();

        result.IsSuccess.ShouldBeFalse();
        result.Error!.Code.ShouldBe("Movie.McpServerUnavailable");
        _cache.Writes.ShouldBe(0);
    }
}
