using System.Text.Json.Serialization;

namespace Application.Responses;

/// <summary>
/// Generic wrapper for paginated list responses. Kept for parity with the reference API
/// so future admin/list endpoints can adopt it without introducing a new shape.
/// </summary>
public class PaginatedResponse<T>
{
    [JsonPropertyName("items")]
    public List<T> Items { get; set; } = new();

    [JsonPropertyName("total_count")]
    public int TotalCount { get; set; }

    [JsonPropertyName("page_number")]
    public int PageNumber { get; set; }

    [JsonPropertyName("page_size")]
    public int PageSize { get; set; }

    [JsonPropertyName("total_pages")]
    public int TotalPages { get; set; }

    [JsonPropertyName("has_next_page")]
    public bool HasNextPage { get; set; }

    [JsonPropertyName("has_previous_page")]
    public bool HasPreviousPage { get; set; }
}
