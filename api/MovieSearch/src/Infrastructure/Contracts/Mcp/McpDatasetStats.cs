using System.Text.Json.Serialization;
using Application.Services;

namespace Infrastructure.Contracts.Mcp;

public class McpDatasetStats
{
    [JsonPropertyName("total_movies")]
    public int TotalMovies { get; set; }

    [JsonPropertyName("with_embeddings")]
    public int WithEmbeddings { get; set; }

    [JsonPropertyName("genres")]
    public int Genres { get; set; }

    /// <summary>[min, max] release year; empty when the catalogue has no dated movies.</summary>
    [JsonPropertyName("year_range")]
    public List<int> YearRange { get; set; } = [];

    [JsonPropertyName("avg_imdb_rating")]
    public double? AvgImdbRating { get; set; }

    [JsonPropertyName("pipeline_version")]
    public string? PipelineVersion { get; set; }

    public MovieStatistics ToStatistics() => new(
        TotalMovies,
        WithEmbeddings,
        Genres,
        YearRange.Count == 2 ? YearRange[0] : null,
        YearRange.Count == 2 ? YearRange[1] : null,
        AvgImdbRating,
        PipelineVersion);
}
