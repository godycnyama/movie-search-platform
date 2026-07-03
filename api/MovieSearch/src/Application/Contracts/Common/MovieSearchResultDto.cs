using System.Text.Json.Serialization;

namespace Application.Contracts.Common;

/// <summary>
/// A single ranked hit in a semantic search result set.
/// Field selection matches the example payload in README §9
/// (<c>GET /api/v1/movies/search</c>).
/// </summary>
public class MovieSearchResultDto
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

    [JsonPropertyName("imdb_rating")]
    public double? ImdbRating { get; set; }

    [JsonPropertyName("rt_rating")]
    public int? RottenTomatoesRating { get; set; }

    /// <summary>Cosine similarity in <c>[0, 1]</c>; higher is more similar.</summary>
    [JsonPropertyName("similarity_score")]
    public double SimilarityScore { get; set; }
}
