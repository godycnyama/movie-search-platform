using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Application.Requests;

/// <summary>
/// Query-string parameters for <c>GET /api/v1/movies/by-title</c> (README §9).
/// Bound with <c>[FromQuery]</c>.
/// </summary>
public class MovieByTitleRequest
{
    /// <summary>Title to look up; matched exactly (case-insensitive) first, then fuzzily.</summary>
    [Required(AllowEmptyStrings = false, ErrorMessage = "'title' is required.")]
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;
}
