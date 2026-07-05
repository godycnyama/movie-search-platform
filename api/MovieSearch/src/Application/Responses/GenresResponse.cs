using System.Text.Json.Serialization;

namespace Application.Responses;

public class GenresResponse
{
    [JsonPropertyName("genres")]
    public List<string> Genres { get; set; } = new();
}
