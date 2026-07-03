"""All pipeline constants in one place.

Tunables that vary per environment (URLs, batch size, model name…) do NOT live
here — those are configuration and belong to ``pipeline.settings``. This module
holds the fixed facts of the pipeline: schema mappings, data policies,
thresholds and identities that must stay consistent across runs (and, where
noted, with the .NET API).
"""

import uuid

# --- Shared schema ----------------------------------------------------------

EMBEDDING_DIM = 768
"""Must match the pgvector ``vector(768)`` column and the embedding model."""


class UserRoles:
    """Mirrors ``Domain.Entities.UserRoles`` in the .NET API."""

    READER = "reader"
    ADMIN = "admin"


# --- Cleaning ----------------------------------------------------------------

# Raw Vega column -> shared schema column (models.Movie field / DB column).
RAW_TO_SCHEMA = {
    "Title": "title",
    "Release_Date": "release_date",
    "Major_Genre": "major_genre",
    "Director": "director",
    "Distributor": "distributor",
    "MPAA_Rating": "mpaa_rating",
    "Creative_Type": "creative_type",
    "Source": "source",
    "IMDB_Rating": "imdb_rating",
    "IMDB_Votes": "imdb_votes",
    "Rotten_Tomatoes_Rating": "rotten_tomatoes_rating",
    "Production_Budget": "production_budget",
    "Worldwide_Gross": "worldwide_gross",
    "Running_Time_min": "running_time_min",
}

STRING_COLUMNS = [
    "title", "major_genre", "director", "distributor",
    "mpaa_rating", "creative_type", "source",
]

# The static Vega movies dataset contains no releases after 2010/2011; later
# years are two-digit-year parsing artifacts (e.g. '46 parsed as 2046).
DATASET_MAX_YEAR = 2011

# --- Imputation ---------------------------------------------------------------

UNKNOWN = "Unknown"
NOT_RATED = "Not Rated"

UNKNOWN_IMPUTED_COLUMNS = [
    "major_genre", "director", "distributor", "creative_type", "source",
]

# --- Augmentation --------------------------------------------------------------

# Budget buckets (USD), checked in order. NULL budget -> NULL tier (never guessed).
BUDGET_TIERS = [
    (10_000_000, "low"),
    (50_000_000, "mid"),
    (100_000_000, "high"),
]
TOP_BUDGET_TIER = "blockbuster"

# A movie is a "blockbuster" when both spend and takings are high.
BLOCKBUSTER_BUDGET = 100_000_000
BLOCKBUSTER_GROSS = 200_000_000

# --- Embedding -----------------------------------------------------------------

EMBED_RETRIES = 3
EMBED_BACKOFF_SECONDS = 2.0

# --- Loading -------------------------------------------------------------------

# Namespace for deterministic movie ids: uuid5(ns, "title|release_date").
# (Schema DDL — table, vector extension, HNSW index — is owned by Alembic:
# see database/migrations.)
MOVIE_ID_NAMESPACE = uuid.uuid5(uuid.NAMESPACE_URL, "movie-search-platform/movies")
