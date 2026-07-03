using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Application.Requests;

/// <summary>
/// Query-string parameters for <c>GET /api/v1/movies/{id}/similar</c> (README §9).
/// Bound with <c>[FromQuery]</c>. The <c>id</c> is a route value and lives on the endpoint,
/// not here.
/// </summary>
public class SimilarMoviesRequest
{
    /// <summary>Number of similar movies to return. Default 5 per the API contract.</summary>
    [Range(1, 50, ErrorMessage = "'top_k' must be between 1 and 50.")]
    [JsonPropertyName("top_k")]
    public int TopK { get; set; } = 5;
}
