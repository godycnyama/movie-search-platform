using System.Text.Json.Serialization;

namespace Application.Responses;

/// <summary>
/// Response for <c>GET /api/v1/stats</c> (README §9). Admin-only in production.
/// </summary>
public class StatsResponse
{
    [JsonPropertyName("total_movies")]
    public int TotalMovies { get; set; }

    [JsonPropertyName("with_embeddings")]
    public int WithEmbeddings { get; set; }

    [JsonPropertyName("genres")]
    public int Genres { get; set; }

    /// <summary>Inclusive <c>[min, max]</c> release-year range across the dataset.</summary>
    [JsonPropertyName("year_range")]
    public int[] YearRange { get; set; } = Array.Empty<int>();

    [JsonPropertyName("avg_imdb_rating")]
    public double? AvgImdbRating { get; set; }

    [JsonPropertyName("pipeline_version")]
    public string? PipelineVersion { get; set; }
}
