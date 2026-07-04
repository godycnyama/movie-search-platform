"""The five MCP tools from the assessment spec (§3.1), plus ``get_movie_by_id``.

The extra tool exists because the platform's .NET API serves ALL movie reads
through this server (it never queries the movies tables directly), and its
``GET /api/v1/movies/{id}`` endpoint looks movies up by UUID rather than title.

Each tool is fully type-annotated (FastMCP derives the input schema from the
signature and validates arguments with Pydantic), returns Pydantic models, and
logs a traced JSON record with timing. ``top_k`` is clamped to [1, 50] to match
the platform's API contract.
"""

import logging
import time
import uuid

from fastmcp import FastMCP

from config import Settings
from server.constants import MAX_TOP_K
from server.db import Database
from server.embeddings import OllamaEmbeddingsClient
from server.logging_config import request_id_var
from server.metrics import TOOL_CALLS, TOOL_DURATION_SECONDS
from server.models import DatasetStats, MovieResult

logger = logging.getLogger(__name__)


def register_tools(
    mcp: FastMCP,
    db: Database,
    embeddings: OllamaEmbeddingsClient,
    settings: Settings,
) -> None:
    """Registers the spec's five tools against the given server/dependencies."""

    def _trace(tool: str) -> tuple[str, float]:
        request_id = str(uuid.uuid4())
        request_id_var.set(request_id)
        return tool, time.perf_counter()

    def _done(tool: str, started: float, result_count: int | None = None) -> None:
        duration = time.perf_counter() - started
        TOOL_CALLS.labels(tool=tool).inc()
        TOOL_DURATION_SECONDS.labels(tool=tool).observe(duration)
        logger.info(
            "tool completed",
            extra={
                "tool": tool,
                "duration_ms": round(duration * 1000, 1),
                "result_count": result_count,
            },
        )

    @mcp.tool()
    async def search_movies_by_description(
        query: str,
        top_k: int = 10,
        genre_filter: str | None = None,
        min_imdb_rating: float | None = None,
        mpaa_rating: str | None = None,
        decade: int | None = None,
    ) -> list[MovieResult]:
        """Search movies using natural language description.

        Performs semantic vector similarity search with optional metadata filters.
        Returns ranked results with similarity scores.
        """
        tool, started = _trace("search_movies_by_description")

        query_embedding = await embeddings.embed_query(query)
        hits = await db.search_movies_by_embedding(
            query_embedding,
            top_k=max(1, min(top_k, MAX_TOP_K)),
            genre=genre_filter,
            min_imdb_rating=min_imdb_rating,
            mpaa_rating=mpaa_rating,
            decade=decade,
        )

        results = [MovieResult.from_record(row, similarity) for row, similarity in hits]
        _done(tool, started, len(results))
        return results

    @mcp.tool()
    async def get_movie_by_id(movie_id: str) -> MovieResult | None:
        """Retrieve a specific movie by its stable UUID identifier.

        Returns null when no movie has that id. Backs the platform API's
        GET /api/v1/movies/{id} endpoint.
        """
        tool, started = _trace("get_movie_by_id")

        try:
            parsed_id = uuid.UUID(movie_id)
        except ValueError as error:
            raise ValueError(f"'{movie_id}' is not a valid movie id (UUID expected)") from error

        record = await db.get_movie_by_id(parsed_id)
        result = MovieResult.from_record(record) if record is not None else None
        _done(tool, started, 1 if result else 0)
        return result

    @mcp.tool()
    async def get_movie_by_title(title: str) -> MovieResult | None:
        """Retrieve a specific movie by exact or fuzzy title match."""
        tool, started = _trace("get_movie_by_title")

        record = await db.get_movie_by_title(title)
        result = MovieResult.from_record(record) if record is not None else None
        _done(tool, started, 1 if result else 0)
        return result

    @mcp.tool()
    async def get_similar_movies(movie_id: str, top_k: int = 5) -> list[MovieResult]:
        """Given a movie ID, return the most semantically similar movies."""
        tool, started = _trace("get_similar_movies")

        try:
            parsed_id = uuid.UUID(movie_id)
        except ValueError as error:
            raise ValueError(f"'{movie_id}' is not a valid movie id (UUID expected)") from error

        hits = await db.get_similar_movies(parsed_id, top_k=max(1, min(top_k, MAX_TOP_K)))
        if hits is None:
            raise ValueError(f"Movie '{movie_id}' does not exist")

        results = [MovieResult.from_record(row, similarity) for row, similarity in hits]
        _done(tool, started, len(results))
        return results

    @mcp.tool()
    async def list_genres() -> list[str]:
        """Return all distinct genres available in the dataset."""
        tool, started = _trace("list_genres")

        genres = await db.list_genres()
        _done(tool, started, len(genres))
        return genres

    @mcp.tool()
    async def get_dataset_stats() -> DatasetStats:
        """Return summary statistics about the movie dataset."""
        tool, started = _trace("get_dataset_stats")

        row = await db.get_dataset_stats()
        stats = DatasetStats(
            total_movies=row["total_movies"],
            with_embeddings=row["with_embeddings"],
            genres=row["genres"],
            year_range=[row["min_year"], row["max_year"]] if row["min_year"] is not None else [],
            avg_imdb_rating=round(float(row["avg_imdb_rating"]), 2)
            if row["avg_imdb_rating"] is not None
            else None,
            pipeline_version=row["pipeline_version"],
        )
        _done(tool, started)
        return stats
