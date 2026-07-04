namespace Application.Services;

/// <summary>
/// Optional metadata filters applied before vector ranking in
/// <see cref="IMovieCatalogService.SearchAsync"/>. All members are combined with AND;
/// a <c>null</c> member means "no filter on this field".
/// </summary>
/// <param name="Genre">Exact match on <c>major_genre</c>.</param>
/// <param name="MinImdbRating">Inclusive lower bound on IMDB rating; rows with a NULL rating are excluded.</param>
/// <param name="MpaaRating">Exact match on MPAA rating (e.g. "PG-13", "Not Rated").</param>
/// <param name="Decade">Exact match on the derived release decade (e.g. 1990).</param>
public sealed record MovieSearchFilters(
    string? Genre = null,
    double? MinImdbRating = null,
    string? MpaaRating = null,
    int? Decade = null)
{
    /// <summary>An unfiltered search.</summary>
    public static readonly MovieSearchFilters None = new();
}
