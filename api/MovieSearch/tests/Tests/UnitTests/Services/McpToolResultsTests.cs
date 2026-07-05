using System.Text.Json;
using Infrastructure.Contracts.Mcp;
using Infrastructure.Services;
using ModelContextProtocol.Protocol;

namespace MovieSearch.Tests.UnitTests.Services;

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

        var movie = movies.ShouldNotBeNull().ShouldHaveSingleItem();
        movie.Id.ShouldBe(Guid.Parse("9f8b6a4e-1c2d-4e3f-8a5b-6c7d8e9f0a1b"));
        movie.Title.ShouldBe("Heat");
        movie.ProductionBudget.ShouldBe(60_000_000);
        movie.SimilarityScore.ShouldBe(0.9134);
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

        var statistics = stats.ShouldNotBeNull().ToStatistics();
        statistics.TotalMovies.ShouldBe(3201);
        statistics.GenreCount.ShouldBe(12);
        statistics.MinReleaseYear.ShouldBe(1920);
        statistics.MaxReleaseYear.ShouldBe(2010);
        statistics.PipelineVersion.ShouldBe("0.1.0");
    }

    [Fact]
    public void Deserialize_ReportsEmptyYearRange_AsNullBounds()
    {
        var result = StructuredResult("""
            {"total_movies": 0, "with_embeddings": 0, "genres": 0, "year_range": []}
            """);

        var statistics = McpToolResults.Deserialize<McpDatasetStats>(result)!.ToStatistics();

        statistics.MinReleaseYear.ShouldBeNull();
        statistics.MaxReleaseYear.ShouldBeNull();
        statistics.AverageImdbRating.ShouldBeNull();
    }

    [Fact]
    public void Deserialize_FallsBackToTextContent_WhenStructuredContentIsMissing()
    {
        var result = new CallToolResult
        {
            Content = [new TextContentBlock { Text = """{"result": ["Action", "Drama"]}""" }],
        };

        var genres = McpToolResults.Deserialize<List<string>>(result);

        genres.ShouldBe(["Action", "Drama"]);
    }

    [Fact]
    public void Deserialize_ReturnsNull_ForANullResultEnvelope()
    {
        // A `MovieResult | None` tool returning None.
        var result = StructuredResult("""{"result": null}""");

        McpToolResults.Deserialize<McpMovieResult>(result).ShouldBeNull();
    }

    [Fact]
    public void Deserialize_ReturnsNull_WhenTheResultCarriesNoPayload()
    {
        McpToolResults.Deserialize<McpMovieResult>(new CallToolResult()).ShouldBeNull();
    }

    [Fact]
    public void ErrorMessage_ReturnsTheTextBlock_OfAFailedCall()
    {
        var result = new CallToolResult
        {
            IsError = true,
            Content = [new TextContentBlock { Text = "Movie '123' does not exist" }],
        };

        McpToolResults.ErrorMessage(result).ShouldBe("Movie '123' does not exist");
    }

    [Fact]
    public void ErrorMessage_HasAFallback_WhenTheFailedCallCarriesNoText()
    {
        var message = McpToolResults.ErrorMessage(new CallToolResult { IsError = true });

        message.ShouldNotBeNullOrWhiteSpace();
    }

    private static CallToolResult StructuredResult(string json) => new()
    {
        StructuredContent = JsonSerializer.Deserialize<JsonElement>(json),
    };
}
