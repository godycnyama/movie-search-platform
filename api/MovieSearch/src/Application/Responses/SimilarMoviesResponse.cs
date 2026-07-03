using System.Text.Json.Serialization;
using Application.Contracts.Common;

namespace Application.Responses;

/// <summary>
/// Response for <c>GET /api/v1/movies/{id}/similar</c> (README §9).
/// </summary>
public class SimilarMoviesResponse
{
    /// <summary>The id of the movie whose neighbours were requested.</summary>
    [JsonPropertyName("source_id")]
    public string SourceId { get; set; } = string.Empty;

    [JsonPropertyName("results")]
    public List<SimilarMovieDto> Results { get; set; } = new();
}
