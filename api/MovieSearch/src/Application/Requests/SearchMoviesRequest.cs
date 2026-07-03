using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Application.Requests;

/// <summary>
/// Query-string parameters for <c>GET /api/v1/movies/search</c> (README §9).
/// Bound with <c>[FromQuery]</c>.
/// </summary>
public class SearchMoviesRequest
{
    /// <summary>Natural-language query, e.g. "action movies from the 90s with high IMDB ratings".</summary>
    [Required(AllowEmptyStrings = false, ErrorMessage = "'q' is required.")]
    [JsonPropertyName("q")]
    public string Q { get; set; } = string.Empty;

    /// <summary>Maximum number of results to return. Default 10, max 50 per the API contract.</summary>
    [Range(1, 50, ErrorMessage = "'top_k' must be between 1 and 50.")]
    [JsonPropertyName("top_k")]
    public int TopK { get; set; } = 10;

    /// <summary>Optional exact match on <c>major_genre</c>.</summary>
    [JsonPropertyName("genre")]
    public string? Genre { get; set; }

    /// <summary>Optional minimum IMDB rating in <c>[0, 10]</c>.</summary>
    [Range(0.0, 10.0, ErrorMessage = "'min_imdb_rating' must be between 0 and 10.")]
    [JsonPropertyName("min_imdb_rating")]
    public double? MinImdbRating { get; set; }

    /// <summary>Optional exact match on MPAA rating (e.g. "PG-13", "R", "Not Rated").</summary>
    [JsonPropertyName("mpaa_rating")]
    public string? MpaaRating { get; set; }

    /// <summary>Optional release decade filter, e.g. 1990.</summary>
    [Range(1900, 2100, ErrorMessage = "'decade' must be a plausible year (e.g. 1990).")]
    [JsonPropertyName("decade")]
    public int? Decade { get; set; }
}
