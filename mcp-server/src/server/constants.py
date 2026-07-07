"""All MCP-server constants in one place.

Environment-varying tunables (URLs, pool sizes, transport…) belong to
``config.Settings`` — this module holds the fixed facts: shared-schema
invariants, tool contract limits and wire/column definitions that must stay
consistent across runs (and, where noted, with the pipeline and .NET API).
"""

import os

# --- Shared schema ------------------------------------------------------------

EMBEDDING_DIM = int(os.environ.get("EMBEDDING_DIM", "768"))
"""Embedding dimensionality — must match the pgvector ``vector(N)`` column and the
active embedding model (local bge-base-en-v1.5 = 768; AWS Bedrock Titan Text
Embeddings V2 = 1024). Query and document embeddings must use the same model per
environment or cosine similarity is meaningless."""


class UserRoles:
    """Mirrors ``Domain.Entities.UserRoles`` in the .NET API."""

    READER = "reader"
    ADMIN = "admin"


# --- Tool contract -------------------------------------------------------------

MAX_TOP_K = 50
"""Upper bound on requested results; matches the .NET API's top_k contract."""

# --- Queries -------------------------------------------------------------------

# Columns exposed to MCP clients (everything except the raw vector/text blobs).
MOVIE_RESULT_COLUMNS = (
    "id, title, release_year, major_genre, director, distributor, mpaa_rating, "
    "imdb_rating, rotten_tomatoes_rating, production_budget, running_time_min, "
    "budget_tier, decade"
)

# --- Logging -------------------------------------------------------------------

# Log-record attributes that are call-site extras worth surfacing as JSON fields.
LOG_EXTRA_FIELDS = ("tool", "duration_ms", "result_count", "transport")
