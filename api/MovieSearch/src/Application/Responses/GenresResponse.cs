using System.Text.Json.Serialization;

namespace Application.Responses;

/// <summary>
/// Response for <c>GET /api/v1/movies/genres</c> (README §9).
/// </summary>
public class GenresResponse
{
    [JsonPropertyName("genres")]
    public List<string> Genres { get; set; } = new();
}
