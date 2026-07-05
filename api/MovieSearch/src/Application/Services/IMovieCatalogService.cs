using Domain.Common;

namespace Application.Services;

/// <summary>
/// Read port for the movie catalogue. The API never queries the movies tables
/// directly — Infrastructure implements this port against the platform's MCP
/// server (<c>mcp-server/</c>), which owns query embedding and pgvector search.
/// One method per movie/stats endpoint, one MCP tool per method. Every method
/// returns a <see cref="Result{T}"/>: downstream failures and "not found" are
/// carried as <see cref="Domain.Errors.Error"/>s rather than exceptions or nulls.
/// </summary>
public interface IMovieCatalogService
{
    /// <summary>
    /// Natural-language semantic search (README §9, <c>GET /api/v1/movies/search</c>;
    /// tool <c>search_movies_by_description</c>). The query text is embedded by the
    /// MCP server, so callers pass it verbatim. Results are ranked best first.
    /// </summary>
    Task<Result<IReadOnlyList<MovieCatalogItem>>> SearchAsync(
        string query,
        MovieSearchFilters filters,
        int topK,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches a single movie by its stable identifier (tool <c>get_movie_by_id</c>);
    /// a <c>Movie.NotFound</c> failure when it does not exist.
    /// </summary>
    Task<Result<MovieCatalogItem>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches a single movie by exact (case-insensitive) or fuzzy title match
    /// (tool <c>get_movie_by_title</c>); a <c>Movie.TitleNotFound</c> failure when
    /// nothing matches.
    /// </summary>
    Task<Result<MovieCatalogItem>> GetByTitleAsync(string title, CancellationToken cancellationToken = default);

    /// <summary>
    /// The <paramref name="topK"/> movies most similar to the given movie
    /// (README §9, <c>GET /api/v1/movies/{id}/similar</c>; tool <c>get_similar_movies</c>),
    /// excluding the movie itself. A <c>Movie.NotFound</c> failure when the movie does
    /// not exist, and a successful empty list when it exists but has no embedding yet.
    /// </summary>
    Task<Result<IReadOnlyList<MovieCatalogItem>>> GetSimilarAsync(
        Guid id,
        int topK,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<string>>> GetGenresAsync(CancellationToken cancellationToken = default);

    Task<Result<MovieStatistics>> GetStatisticsAsync(CancellationToken cancellationToken = default);
}
