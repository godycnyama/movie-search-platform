using System.Text.Json.Serialization;
using Application.Contracts.Common;

namespace Application.Responses;

public class SearchMoviesResponse
{
    [JsonPropertyName("query")]
    public string Query { get; set; } = string.Empty;

    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("results")]
    public List<MovieSearchResultDto> Results { get; set; } = new();
}
