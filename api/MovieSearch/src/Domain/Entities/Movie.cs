using Pgvector;

namespace Domain.Entities;

/// <summary>
/// A movie record from the Vega dataset, cleaned, augmented and embedded for semantic search.
/// Nullable properties reflect the imputation policy: fields that feed ranking/filtering
/// (ratings, budget, gross) are left NULL rather than fabricated when unknown, while
/// categorical fields are imputed with a real "Unknown"/"Not Rated" category.
/// </summary>
public class Movie
{
    /// <summary>Stable unique identifier (GUID) surfaced to clients and used for lookups.</summary>
    public Guid Id { get; set; }

    /// <summary>Movie title. Rows with no title are dropped by the pipeline, so this is always present.</summary>
    public required string Title { get; set; }

    /// <summary>Parsed release date; NULL when the source value was unparseable.</summary>
    public DateOnly? ReleaseDate { get; set; }

    /// <summary>Release year, derived from <see cref="ReleaseDate"/>.</summary>
    public int? ReleaseYear { get; set; }

    /// <summary>Primary genre. Imputed as "Unknown" when missing.</summary>
    public string MajorGenre { get; set; } = "Unknown";

    /// <summary>Director. Imputed as "Unknown" when missing.</summary>
    public string Director { get; set; } = "Unknown";

    /// <summary>Distributor. Imputed as "Unknown" when missing.</summary>
    public string Distributor { get; set; } = "Unknown";

    /// <summary>MPAA rating. Imputed as the real category "Not Rated" when missing.</summary>
    public string MpaaRating { get; set; } = "Not Rated";

    /// <summary>Creative type (e.g. Contemporary Fiction). Imputed as "Unknown" when missing.</summary>
    public string CreativeType { get; set; } = "Unknown";

    /// <summary>Source (e.g. Original Screenplay). Imputed as "Unknown" when missing.</summary>
    public string Source { get; set; } = "Unknown";

    /// <summary>IMDB rating in [0, 10]. Left NULL (never imputed) so ranking/filters stay truthful.</summary>
    public double? ImdbRating { get; set; }

    /// <summary>Number of IMDB votes; part of the embedding text.</summary>
    public int? ImdbVotes { get; set; }

    /// <summary>Rotten Tomatoes rating in [0, 100]. Left NULL when unknown.</summary>
    public int? RottenTomatoesRating { get; set; }

    /// <summary>Production budget in USD. Left NULL when unknown.</summary>
    public long? ProductionBudget { get; set; }

    /// <summary>Worldwide gross in USD. Left NULL when unknown; negatives coerced to NULL.</summary>
    public long? WorldwideGross { get; set; }

    /// <summary>Running time in minutes. Median-imputed within <see cref="MajorGenre"/> when missing.</summary>
    public int? RunningTimeMin { get; set; }

    /// <summary>True when <see cref="RunningTimeMin"/> was imputed rather than sourced.</summary>
    public bool RuntimeImputed { get; set; }

    // Derived features (see README §6 Data Decisions).

    /// <summary>Bucketised budget (e.g. low/mid/high/blockbuster); NULL when budget is unknown.</summary>
    public string? BudgetTier { get; set; }

    /// <summary>Integer decade derived from <see cref="ReleaseDate"/> (e.g. 1990); NULL when unknown.</summary>
    public int? Decade { get; set; }

    /// <summary>True when both budget and gross are high — a popularity signal.</summary>
    public bool BlockbusterFlag { get; set; }

    // Embedding.

    /// <summary>The serialised rich text block that was embedded (stored for transparency and re-embedding).</summary>
    public string AugmentedText { get; set; } = string.Empty;

    /// <summary>768-dimensional embedding vector (pgvector). NULL until the pipeline embeds the row.</summary>
    public Vector? Embedding { get; set; }

    // Audit columns.

    /// <summary>Timestamp the row was first loaded.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Timestamp the row was last upserted by the pipeline.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Version of the pipeline that produced this row (e.g. "0.1.0").</summary>
    public string? PipelineVersion { get; set; }
}
