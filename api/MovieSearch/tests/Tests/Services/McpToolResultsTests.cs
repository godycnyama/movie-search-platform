using System.Text.Json;
using Infrastructure.Contracts.Mcp;
using Infrastructure.Services;
using ModelContextProtocol.Protocol;

namespace Tests.Services;

/// <summary>
/// Covers decoding of FastMCP tool results: the {"result": ...} envelope around
/// non-object payloads, direct object payloads, the text-content fallback, and
/// null/error results. Payload shapes mirror mcp-server/src/server/models.py.
/// </summary>
public class McpToolResultsTests
{
    private const string MovieJson = """
        {
            "id": "9f8b6a4e-1c2d-4e3f-8a5b-6c7d8e9f0a1b",
            "title": "Heat",
            "release_year": 1995,
            "major_genre": "Drama",
            "director": "Michael Mann",
            "distributor": "Warner Bros.",
            "mpaa_rating": "R",
            "imdb_rating": 8.3,
            "rotten_tomatoes_rating": 88,
            "production_budget": 60000000,
            "running_time_min": 170,
            "budget_tier": "high",
            "decade": 1990,
            "similarity_score": 0.9134
        }
        """;

    [Fact]
    public void Deserialize_UnwrapsTheResultEnvelope_ForListPayloads()
    {
        var result = StructuredResult($$"""{"result": [{{MovieJson}}]}""");

        var movies = McpToolResults.Deserialize<List<McpMovieResult>>(result);

        Assert.NotNull(movies);
        var movie = Assert.Single(movies);
        Assert.Equal(Guid.Parse("9f8b6a4e-1c2d-4e3f-8a5b-6c7d8e9f0a1b"), movie.Id);
        Assert.Equal("Heat", movie.Title);
        Assert.Equal(60_000_000, movie.ProductionBudget);
        Assert.Equal(0.9134, movie.SimilarityScore);
    }

    [Fact]
    public void Deserialize_ReadsObjectPayloads_WithoutAnEnvelope()
    {
        var result = StructuredResult("""
            {
                "total_movies": 3201,
                "with_embeddings": 3200,
                "genres": 12,
                "year_range": [1920, 2010],
                "avg_imdb_rating": 6.28,
                "pipeline_version": "0.1.0"
            }
            """);

        var stats = McpToolResults.Deserialize<McpDatasetStats>(result);

        Assert.NotNull(stats);
        var statistics = stats.ToStatistics();
        Assert.Equal(3201, statistics.TotalMovies);
        Assert.Equal(12, statistics.GenreCount);
        Assert.Equal(1920, statistics.MinReleaseYear);
        Assert.Equal(2010, statistics.MaxReleaseYear);
        Assert.Equal("0.1.0", statistics.PipelineVersion);
    }

    [Fact]
    public void Deserialize_ReportsEmptyYearRange_AsNullBounds()
    {
        var result = StructuredResult("""
            {"total_movies": 0, "with_embeddings": 0, "genres": 0, "year_range": []}
            """);

        var statistics = McpToolResults.Deserialize<McpDatasetStats>(result)!.ToStatistics();

        Assert.Null(statistics.MinReleaseYear);
        Assert.Null(statistics.MaxReleaseYear);
        Assert.Null(statistics.AverageImdbRating);
    }

    [Fact]
    public void Deserialize_FallsBackToTextContent_WhenStructuredContentIsMissing()
    {
        var result = new CallToolResult
        {
            Content = [new TextContentBlock { Text = """{"result": ["Action", "Drama"]}""" }],
        };

        var genres = McpToolResults.Deserialize<List<string>>(result);

        Assert.Equal(["Action", "Drama"], genres);
    }

    [Fact]
    public void Deserialize_ReturnsNull_ForANullResultEnvelope()
    {
        // A `MovieResult | None` tool returning None.
        var result = StructuredResult("""{"result": null}""");

        Assert.Null(McpToolResults.Deserialize<McpMovieResult>(result));
    }

    [Fact]
    public void Deserialize_ReturnsNull_WhenTheResultCarriesNoPayload()
    {
        Assert.Null(McpToolResults.Deserialize<McpMovieResult>(new CallToolResult()));
    }

    [Fact]
    public void ErrorMessage_ReturnsTheTextBlock_OfAFailedCall()
    {
        var result = new CallToolResult
        {
            IsError = true,
            Content = [new TextContentBlock { Text = "Movie '123' does not exist" }],
        };

        Assert.Equal("Movie '123' does not exist", McpToolResults.ErrorMessage(result));
    }

    [Fact]
    public void ErrorMessage_HasAFallback_WhenTheFailedCallCarriesNoText()
    {
        var message = McpToolResults.ErrorMessage(new CallToolResult { IsError = true });

        Assert.False(string.IsNullOrWhiteSpace(message));
    }

    private static CallToolResult StructuredResult(string json) => new()
    {
        StructuredContent = JsonSerializer.Deserialize<JsonElement>(json),
    };
}
