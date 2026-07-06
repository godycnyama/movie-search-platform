using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Application.Requests;

public class MovieByTitleRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "'title' is required.")]
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;
}
