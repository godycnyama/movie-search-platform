using System.Text.Json.Serialization;
using Application.Services;

namespace Infrastructure.Contracts.Mcp;

/// <summary>
/// Wire shape of the MCP server's <c>MovieResult</c> model
/// (<c>mcp-server/src/server/models.py</c>); field names are the snake_case
/// tool-output schema. If a field is added there, add it here too.
/// </summary>
public class McpMovieResult
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("release_year")]
    public int? ReleaseYear { get; set; }

    [JsonPropertyName("major_genre")]
    public string? MajorGenre { get; set; }

    [JsonPropertyName("director")]
    public string? Director { get; set; }

    [JsonPropertyName("distributor")]
    public string? Distributor { get; set; }

    [JsonPropertyName("mpaa_rating")]
    public string? MpaaRating { get; set; }

    [JsonPropertyName("imdb_rating")]
    public double? ImdbRating { get; set; }

    [JsonPropertyName("rotten_tomatoes_rating")]
    public int? RottenTomatoesRating { get; set; }

    [JsonPropertyName("production_budget")]
    public long? ProductionBudget { get; set; }

    [JsonPropertyName("running_time_min")]
    public int? RunningTimeMin { get; set; }

    [JsonPropertyName("budget_tier")]
    public string? BudgetTier { get; set; }

    [JsonPropertyName("decade")]
    public int? Decade { get; set; }

    [JsonPropertyName("similarity_score")]
    public double? SimilarityScore { get; set; }

    public MovieCatalogItem ToCatalogItem() => new(
        Id,
        Title,
        ReleaseYear,
        MajorGenre,
        Director,
        Distributor,
        MpaaRating,
        ImdbRating,
        RottenTomatoesRating,
        ProductionBudget,
        RunningTimeMin,
        BudgetTier,
        Decade,
        SimilarityScore);
}
