using Domain.Entities;

namespace Application.Repositories;

/// <summary>
/// Persistence port for the movie catalogue. Implemented in Infrastructure with
/// EF Core + pgvector; the Application layer depends only on this abstraction.
/// </summary>
public interface IMovieRepository
{
    /// <summary>Fetches a single movie by its stable identifier, or <c>null</c> when it does not exist.</summary>
    Task<Movie?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Semantic search: ranks embedded movies by cosine similarity to
    /// <paramref name="queryEmbedding"/>, after applying the metadata
    /// <paramref name="filters"/> (README §9, <c>GET /api/v1/movies/search</c>).
    /// Movies without an embedding are never returned.
    /// </summary>
    /// <param name="queryEmbedding">768-dimensional embedding of the natural-language query.</param>
    /// <param name="filters">Optional metadata filters; use <see cref="MovieSearchFilters.None"/> for an unfiltered search.</param>
    /// <param name="topK">Maximum number of results, best first.</param>
    Task<IReadOnlyList<MovieSimilarityResult>> SearchAsync(
        float[] queryEmbedding,
        MovieSearchFilters filters,
        int topK,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The <paramref name="topK"/> movies most similar to the given movie
    /// (README §9, <c>GET /api/v1/movies/{id}/similar</c>), excluding the movie itself.
    /// Returns <c>null</c> when the movie does not exist, and an empty list when it
    /// exists but has not been embedded yet.
    /// </summary>
    Task<IReadOnlyList<MovieSimilarityResult>?> GetSimilarAsync(
        Guid id,
        int topK,
        CancellationToken cancellationToken = default);

    /// <summary>Distinct <c>major_genre</c> values, sorted alphabetically (README §9, <c>GET /api/v1/movies/genres</c>).</summary>
    Task<IReadOnlyList<string>> GetGenresAsync(CancellationToken cancellationToken = default);

    /// <summary>Dataset-wide aggregates for the admin stats endpoint (README §9, <c>GET /api/v1/stats</c>).</summary>
    Task<MovieStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);
}
