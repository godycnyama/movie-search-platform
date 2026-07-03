using System.Text.Json.Serialization;

namespace Application.Responses;

/// <summary>
/// Response for <c>GET /api/v1/movies/{id}</c> (README §9). Full movie detail with the
/// fields exposed to API clients. Note that this is a public-facing projection of
/// <c>Domain.Entities.Movie</c> — internal audit columns (<c>CreatedAt</c>,
/// <c>UpdatedAt</c>, <c>PipelineVersion</c>) and the raw <c>Embedding</c> are omitted.
/// </summary>
public class MovieResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

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

    [JsonPropertyName("rt_rating")]
    public int? RottenTomatoesRating { get; set; }

    [JsonPropertyName("production_budget")]
    public long? ProductionBudget { get; set; }

    [JsonPropertyName("running_time_min")]
    public int? RunningTimeMin { get; set; }

    /// <summary>Bucketised budget (e.g. low/mid/high/blockbuster); null when budget is unknown.</summary>
    [JsonPropertyName("budget_tier")]
    public string? BudgetTier { get; set; }

    /// <summary>Integer decade derived from the release date (e.g. 1990); null when unknown.</summary>
    [JsonPropertyName("decade")]
    public int? Decade { get; set; }
}
