using Application.Repositories;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace Infrastructure.Repositories;

/// <summary>
/// EF Core + pgvector implementation of <see cref="IMovieRepository"/>.
/// All queries are read-only (the pipeline owns writes), so tracking is disabled throughout.
/// </summary>
public sealed class MovieRepository(ApplicationDbContext dbContext) : IMovieRepository
{
    public Task<Movie?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Movies
                 .AsNoTracking()
                 .SingleOrDefaultAsync(m => m.Id == id, cancellationToken);

    public Task<IReadOnlyList<MovieSimilarityResult>> SearchAsync(
        float[] queryEmbedding,
        MovieSearchFilters filters,
        int topK,
        CancellationToken cancellationToken = default)
    {
        var candidates = dbContext.Movies.AsNoTracking().Where(m => m.Embedding != null);

        if (!string.IsNullOrWhiteSpace(filters.Genre))
        {
            candidates = candidates.Where(m => m.MajorGenre == filters.Genre);
        }

        if (filters.MinImdbRating is { } minImdbRating)
        {
            candidates = candidates.Where(m => m.ImdbRating >= minImdbRating);
        }

        if (!string.IsNullOrWhiteSpace(filters.MpaaRating))
        {
            candidates = candidates.Where(m => m.MpaaRating == filters.MpaaRating);
        }

        if (filters.Decade is { } decade)
        {
            candidates = candidates.Where(m => m.Decade == decade);
        }

        return RankBySimilarityAsync(candidates, new Vector(queryEmbedding), topK, cancellationToken);
    }

    public async Task<IReadOnlyList<MovieSimilarityResult>?> GetSimilarAsync(
        Guid id,
        int topK,
        CancellationToken cancellationToken = default)
    {
        var source = await dbContext.Movies
                                    .AsNoTracking()
                                    .Where(m => m.Id == id)
                                    .Select(m => new { m.Embedding })
                                    .SingleOrDefaultAsync(cancellationToken);

        if (source is null)
        {
            return null;
        }

        if (source.Embedding is null)
        {
            return [];
        }

        var candidates = dbContext.Movies
                                  .AsNoTracking()
                                  .Where(m => m.Id != id && m.Embedding != null);

        return await RankBySimilarityAsync(candidates, source.Embedding, topK, cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetGenresAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Movies
                       .AsNoTracking()
                       .Select(m => m.MajorGenre)
                       .Distinct()
                       .OrderBy(genre => genre)
                       .ToListAsync(cancellationToken);

    public async Task<MovieStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        // Single-row aggregate over the whole table; GroupBy on a constant lets EF
        // translate everything into one SELECT with aggregate functions.
        var aggregates = await dbContext.Movies
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalMovies = g.Count(),
                WithEmbeddings = g.Count(m => m.Embedding != null),
                MinReleaseYear = g.Min(m => m.ReleaseYear),
                MaxReleaseYear = g.Max(m => m.ReleaseYear),
                AverageImdbRating = g.Average(m => m.ImdbRating),
                // The pipeline stamps every row on upsert, so the max is the newest version.
                PipelineVersion = g.Max(m => m.PipelineVersion),
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (aggregates is null)
        {
            return new MovieStatistics(0, 0, 0, null, null, null, null);
        }

        var genreCount = await dbContext.Movies
                                        .AsNoTracking()
                                        .Select(m => m.MajorGenre)
                                        .Distinct()
                                        .CountAsync(cancellationToken);

        return new MovieStatistics(
            aggregates.TotalMovies,
            aggregates.WithEmbeddings,
            genreCount,
            aggregates.MinReleaseYear,
            aggregates.MaxReleaseYear,
            aggregates.AverageImdbRating,
            aggregates.PipelineVersion);
    }

    /// <summary>
    /// Orders <paramref name="candidates"/> by cosine distance to <paramref name="queryEmbedding"/>
    /// (served by the HNSW index) and converts distance to the similarity score the API exposes.
    /// Callers must have already excluded rows with a NULL embedding.
    /// </summary>
    private static async Task<IReadOnlyList<MovieSimilarityResult>> RankBySimilarityAsync(
        IQueryable<Movie> candidates,
        Vector queryEmbedding,
        int topK,
        CancellationToken cancellationToken)
    {
        var hits = await candidates
            .Select(m => new { Movie = m, Distance = m.Embedding!.CosineDistance(queryEmbedding) })
            .OrderBy(hit => hit.Distance)
            .Take(topK)
            .ToListAsync(cancellationToken);

        return hits.Select(hit => new MovieSimilarityResult(hit.Movie, 1 - hit.Distance)).ToList();
    }
}
