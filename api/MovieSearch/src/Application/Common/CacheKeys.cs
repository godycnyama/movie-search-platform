using System.Globalization;

namespace Application.Common;

internal static class CacheKeys
{
    public static readonly TimeSpan SearchTtl = TimeSpan.FromMinutes(5);

    public static readonly TimeSpan MovieTtl = TimeSpan.FromMinutes(30);

    public static readonly TimeSpan SimilarTtl = TimeSpan.FromMinutes(30);

    public static readonly TimeSpan GenresTtl = TimeSpan.FromHours(1);

    public static readonly TimeSpan StatsTtl = TimeSpan.FromMinutes(5);

    public static string Search(string q, int topK, string? genre, double? minImdbRating, string? mpaaRating, int? decade) =>
        $"search:q={Normalize(q)}:k={topK}" +
        $":g={Normalize(genre)}" +
        $":r={minImdbRating?.ToString(CultureInfo.InvariantCulture) ?? "-"}" +
        $":m={Normalize(mpaaRating)}" +
        $":d={decade?.ToString(CultureInfo.InvariantCulture) ?? "-"}";

    public static string Movie(Guid id) => $"movie:{id}";

    public static string MovieByTitle(string title) => $"movie:title={Normalize(title)}";

    public static string Similar(Guid id, int topK) => $"similar:{id}:k={topK}";

    public static string Genres() => "genres";

    public static string Stats() => "stats";

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "-" : value.Trim().ToLowerInvariant();
}
