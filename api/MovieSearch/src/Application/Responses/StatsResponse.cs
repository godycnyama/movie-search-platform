using System.Text.Json.Serialization;

namespace Application.Responses;

public class StatsResponse
{
    [JsonPropertyName("total_movies")]
    public int TotalMovies { get; set; }

    [JsonPropertyName("with_embeddings")]
    public int WithEmbeddings { get; set; }

    [JsonPropertyName("genres")]
    public int Genres { get; set; }

    [JsonPropertyName("year_range")]
    public int[] YearRange { get; set; } = Array.Empty<int>();

    [JsonPropertyName("avg_imdb_rating")]
    public double? AvgImdbRating { get; set; }

    [JsonPropertyName("pipeline_version")]
    public string? PipelineVersion { get; set; }
}
