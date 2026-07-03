using Application.Contracts.Common;
using Application.Repositories;
using Application.Responses;
using Domain.Entities;

namespace Application.Mappings;

/// <summary>Projections from domain entities / repository results onto the public API contracts (README §9).</summary>
internal static class MovieMappings
{
    public static MovieResponse ToMovieResponse(this Movie movie) => new()
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

    public static MovieSearchResultDto ToSearchResultDto(this MovieSimilarityResult hit) => new()
    {
        Id = hit.Movie.Id.ToString(),
        Title = hit.Movie.Title,
        ReleaseYear = hit.Movie.ReleaseYear,
        MajorGenre = hit.Movie.MajorGenre,
        Director = hit.Movie.Director,
        ImdbRating = hit.Movie.ImdbRating,
        RottenTomatoesRating = hit.Movie.RottenTomatoesRating,
        SimilarityScore = Math.Round(hit.SimilarityScore, 4),
    };

    public static SimilarMovieDto ToSimilarMovieDto(this MovieSimilarityResult hit) => new()
    {
        Id = hit.Movie.Id.ToString(),
        Title = hit.Movie.Title,
        SimilarityScore = Math.Round(hit.SimilarityScore, 4),
    };
}
