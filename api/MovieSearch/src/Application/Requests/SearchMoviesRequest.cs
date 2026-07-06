using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Application.Requests;

public class SearchMoviesRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "'query' is required.")]
    [JsonPropertyName("query")]
    public string Query { get; set; } = string.Empty;

    [Range(1, 50, ErrorMessage = "'top_k' must be between 1 and 50.")]
    [JsonPropertyName("top_k")]
    public int TopK { get; set; } = 10;

    [JsonPropertyName("genre")]
    public string? Genre { get; set; }

    [Range(0.0, 10.0, ErrorMessage = "'min_imdb_rating' must be between 0 and 10.")]
    [JsonPropertyName("min_imdb_rating")]
    public double? MinImdbRating { get; set; }

    [JsonPropertyName("mpaa_rating")]
    public string? MpaaRating { get; set; }

    [Range(1900, 2100, ErrorMessage = "'decade' must be a plausible year (e.g. 1990).")]
    [JsonPropertyName("decade")]
    public int? Decade { get; set; }
}
