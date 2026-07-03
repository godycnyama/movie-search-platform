using System.Globalization;

namespace Application.Common;

/// <summary>
/// Central cache key builders and TTLs for the query slices, so key shapes live in one
/// place and can't drift apart. Keys are colon-delimited (Redis convention); the
/// instance-wide prefix is applied by the cache implementation, not here. Free-text
/// parts are trimmed and lower-cased so trivially different requests share an entry.
/// </summary>
internal static class CacheKeys
{
    /// <summary>Search results change only when the pipeline reloads, but keep this short to bound staleness of a large key space.</summary>
    public static readonly TimeSpan SearchTtl = TimeSpan.FromMinutes(5);

    public static readonly TimeSpan MovieTtl = TimeSpan.FromMinutes(30);

    public static readonly TimeSpan SimilarTtl = TimeSpan.FromMinutes(30);

    /// <summary>The genre list is tiny and near-static.</summary>
    public static readonly TimeSpan GenresTtl = TimeSpan.FromHours(1);

    public static readonly TimeSpan StatsTtl = TimeSpan.FromMinutes(5);

    public static string Search(string q, int topK, string? genre, double? minImdbRating, string? mpaaRating, int? decade) =>
        $"search:q={Normalize(q)}:k={topK}" +
        $":g={Normalize(genre)}" +
        $":r={minImdbRating?.ToString(CultureInfo.InvariantCulture) ?? "-"}" +
        $":m={Normalize(mpaaRating)}" +
        $":d={decade?.ToString(CultureInfo.InvariantCulture) ?? "-"}";

    public static string Movie(Guid id) => $"movie:{id}";

    public static string Similar(Guid id, int topK) => $"similar:{id}:k={topK}";

    public static string Genres() => "genres";

    public static string Stats() => "stats";

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "-" : value.Trim().ToLowerInvariant();
}
