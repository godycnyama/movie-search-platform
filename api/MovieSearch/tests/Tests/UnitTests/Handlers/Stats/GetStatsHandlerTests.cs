using Application.Features.Stats;
using Application.Services;
using Domain.Common;
using Domain.Errors;
using Microsoft.Extensions.Logging.Abstractions;

namespace MovieSearch.Tests.UnitTests.Handlers.Stats;

/// <summary>
/// Covers <see cref="GetStatsHandler"/>: mapping statistics (year-range bounds and the
/// avg-rating rounding), the empty-catalogue case, and propagating a downstream failure.
/// </summary>
public class GetStatsHandlerTests
{
    private readonly FakeMovieCatalogService _catalog = new();
    private readonly FakeCacheService _cache = new();

    private Task<Result<Application.Responses.StatsResponse>> Handle() =>
        GetStatsHandler.Handle(new GetStatsQuery(), _catalog, _cache,
            NullLogger<GetStatsQuery>.Instance, CancellationToken.None);

    [Fact]
    public async Task Handle_MapsTheYearRange_AndRoundsTheAverageRating()
    {
        _catalog.OnGetStatistics = () => Result<MovieStatistics>.Success(
            new MovieStatistics(3201, 3201, 12, 1915, 2010, 6.2849, "0.1.0"));

        var result = await Handle();

        result.IsSuccess.ShouldBeTrue();
        result.Value!.TotalMovies.ShouldBe(3201);
        result.Value.Genres.ShouldBe(12);
        result.Value.YearRange.ShouldBe(new[] { 1915, 2010 });
        result.Value.AvgImdbRating.ShouldBe(6.28); // rounded to 2 dp
        result.Value.PipelineVersion.ShouldBe("0.1.0");
        _cache.Writes.ShouldBe(1);
    }

    [Fact]
    public async Task Handle_ReturnsEmptyBounds_WhenTheCatalogueIsEmpty()
    {
        _catalog.OnGetStatistics = () => Result<MovieStatistics>.Success(
            new MovieStatistics(0, 0, 0, null, null, null, null));

        var result = await Handle();

        result.IsSuccess.ShouldBeTrue();
        result.Value!.YearRange.ShouldBeEmpty();
        result.Value.AvgImdbRating.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_PropagatesADownstreamFailure()
    {
        _catalog.OnGetStatistics = () => Result<MovieStatistics>.Failure(MovieErrors.StatsUnavailable());

        var result = await Handle();

        result.IsSuccess.ShouldBeFalse();
        result.Error!.Code.ShouldBe("Movie.StatsUnavailable");
        _cache.Writes.ShouldBe(0);
    }
}
