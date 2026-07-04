namespace Application.Services;

/// <summary>
/// Read port for the movie catalogue. The API never queries the movies tables
/// directly — Infrastructure implements this port against the platform's MCP
/// server (<c>mcp-server/</c>), which owns query embedding and pgvector search.
/// One method per movie/stats endpoint, one MCP tool per method.
/// </summary>
public interface IMovieCatalogService
{
    /// <summary>
    /// Natural-language semantic search (README §9, <c>GET /api/v1/movies/search</c>;
    /// tool <c>search_movies_by_description</c>). The query text is embedded by the
    /// MCP server, so callers pass it verbatim. Results are ranked best first.
    /// </summary>
    Task<IReadOnlyList<MovieCatalogItem>> SearchAsync(
        string query,
        MovieSearchFilters filters,
        int topK,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches a single movie by its stable identifier, or <c>null</c> when it does
    /// not exist (tool <c>get_movie_by_id</c>).
    /// </summary>
    Task<MovieCatalogItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches a single movie by exact (case-insensitive) or fuzzy title match, or
    /// <c>null</c> when nothing matches (tool <c>get_movie_by_title</c>).
    /// </summary>
    Task<MovieCatalogItem?> GetByTitleAsync(string title, CancellationToken cancellationToken = default);

    /// <summary>
    /// The <paramref name="topK"/> movies most similar to the given movie
    /// (README §9, <c>GET /api/v1/movies/{id}/similar</c>; tool <c>get_similar_movies</c>),
    /// excluding the movie itself. Returns <c>null</c> when the movie does not exist,
    /// and an empty list when it exists but has not been embedded yet.
    /// </summary>
    Task<IReadOnlyList<MovieCatalogItem>?> GetSimilarAsync(
        Guid id,
        int topK,
        CancellationToken cancellationToken = default);

    /// <summary>Distinct <c>major_genre</c> values, sorted alphabetically (tool <c>list_genres</c>).</summary>
    Task<IReadOnlyList<string>> GetGenresAsync(CancellationToken cancellationToken = default);

    /// <summary>Dataset-wide aggregates for the admin stats endpoint (tool <c>get_dataset_stats</c>).</summary>
    Task<MovieStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);
}
