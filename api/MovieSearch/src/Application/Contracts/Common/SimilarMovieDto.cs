using System.Text.Json.Serialization;

namespace Application.Contracts.Common;

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
