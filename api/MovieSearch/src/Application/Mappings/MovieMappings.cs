using Application.Contracts.Common;
using Application.Responses;
using Application.Services;

namespace Application.Mappings;

/// <summary>Projections from MCP catalogue results onto the public API contracts (README §9).</summary>
internal static class MovieMappings
{
    public static MovieResponse ToMovieResponse(this MovieCatalogItem movie) => new()
    {
        Id = movie.Id.ToString(),
        Title = movie.Title,
        ReleaseYear = movie.ReleaseYear,
        MajorGenre = movie.MajorGenre,
        Director = movie.Director,
        Distributor = movie.Distributor,
        MpaaRating = movie.MpaaRating,
        ImdbRating = movie.ImdbRating,
        RottenTomatoesRating = movie.RottenTomatoesRating,
        ProductionBudget = movie.ProductionBudget,
        RunningTimeMin = movie.RunningTimeMin,
        BudgetTier = movie.BudgetTier,
        Decade = movie.Decade,
    };

    public static MovieSearchResultDto ToSearchResultDto(this MovieCatalogItem hit) => new()
    {
        Id = hit.Id.ToString(),
        Title = hit.Title,
        ReleaseYear = hit.ReleaseYear,
        MajorGenre = hit.MajorGenre,
        Director = hit.Director,
        ImdbRating = hit.ImdbRating,
        RottenTomatoesRating = hit.RottenTomatoesRating,
        SimilarityScore = hit.SimilarityScore is { } score ? Math.Round(score, 4) : 0,
    };

    public static SimilarMovieDto ToSimilarMovieDto(this MovieCatalogItem hit) => new()
    {
        Id = hit.Id.ToString(),
        Title = hit.Title,
        SimilarityScore = hit.SimilarityScore is { } score ? Math.Round(score, 4) : 0,
    };
}
