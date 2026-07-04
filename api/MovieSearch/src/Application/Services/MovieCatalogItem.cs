namespace Application.Services;

/// <summary>
/// A movie as served by the platform's MCP server — a public projection of the
/// catalogue row (mirrors <c>MovieResult</c> in <c>mcp-server/src/server/models.py</c>).
/// Ranked results carry a cosine similarity score; direct lookups leave it <c>null</c>.
/// </summary>
public sealed record MovieCatalogItem(
    Guid Id,
    string Title,
    int? ReleaseYear,
    string? MajorGenre,
    string? Director,
    string? Distributor,
    string? MpaaRating,
    double? ImdbRating,
    int? RottenTomatoesRating,
    long? ProductionBudget,
    int? RunningTimeMin,
    string? BudgetTier,
    int? Decade,
    double? SimilarityScore);
