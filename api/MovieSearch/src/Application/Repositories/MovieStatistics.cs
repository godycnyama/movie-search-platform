namespace Application.Repositories;

/// <summary>
/// Dataset-wide aggregates backing <c>GET /api/v1/stats</c> (README §9).
/// Year and rating aggregates are <c>null</c> when the catalogue is empty
/// or the underlying column is NULL for every row.
/// </summary>
public sealed record MovieStatistics(
    int TotalMovies,
    int WithEmbeddings,
    int GenreCount,
    int? MinReleaseYear,
    int? MaxReleaseYear,
    double? AverageImdbRating,
    string? PipelineVersion);
