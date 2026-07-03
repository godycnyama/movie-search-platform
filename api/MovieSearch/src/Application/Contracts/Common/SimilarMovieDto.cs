using System.Text.Json.Serialization;

namespace Application.Contracts.Common;

/// <summary>
/// A single item in a "similar movies" list — a minimal projection of a movie plus
/// its cosine similarity to the source movie. Shape matches README §9
/// (<c>GET /api/v1/movies/{id}/similar</c>).
/// </summary>
public class SimilarMovieDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>Cosine similarity in <c>[0, 1]</c>; higher is more similar.</summary>
    [JsonPropertyName("similarity_score")]
    public double SimilarityScore { get; set; }
}
