using System.Text.Json.Serialization;

namespace Application.Responses;

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

    [JsonPropertyName("budget_tier")]
    public string? BudgetTier { get; set; }

    [JsonPropertyName("decade")]
    public int? Decade { get; set; }
}
