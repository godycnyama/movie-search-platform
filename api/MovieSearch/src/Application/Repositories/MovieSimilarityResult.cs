using Domain.Entities;

namespace Application.Repositories;

/// <summary>
/// A ranked vector-search hit: the movie plus its cosine similarity to the query,
/// in <c>[0, 1]</c> with higher meaning more similar (similarity = 1 − cosine distance).
/// </summary>
public sealed record MovieSimilarityResult(Movie Movie, double SimilarityScore);
