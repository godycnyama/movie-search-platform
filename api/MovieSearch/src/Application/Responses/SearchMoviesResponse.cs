using System.Text.Json.Serialization;
using Application.Contracts.Common;

namespace Application.Responses;

/// <summary>
/// Response for <c>GET /api/v1/movies/search</c> (README §9). Echoes the original query,
/// carries the result count, and returns the ranked hits.
/// </summary>
public class SearchMoviesResponse
{
    [JsonPropertyName("query")]
    public string Query { get; set; } = string.Empty;

    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("results")]
    public List<MovieSearchResultDto> Results { get; set; } = new();
}
