using System.Text.Json.Serialization;
using Application.Contracts.Common;

namespace Application.Responses;

public class SimilarMoviesResponse
{
    [JsonPropertyName("source_id")]
    public string SourceId { get; set; } = string.Empty;

    [JsonPropertyName("results")]
    public List<SimilarMovieDto> Results { get; set; } = new();
}
