using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Application.Requests;

public class SimilarMoviesRequest
{
    [Range(1, 50, ErrorMessage = "'top_k' must be between 1 and 50.")]
    [JsonPropertyName("top_k")]
    public int TopK { get; set; } = 5;
}
