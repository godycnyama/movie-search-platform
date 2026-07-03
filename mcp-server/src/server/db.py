"""Database access for the MCP server — asyncpg connection pool over pgvector.

Read-only (the pipeline owns ``movies``, the API owns ``users``). The pool is
created lazily on first use and shared across tool calls (spec §3.2: connection
pooling with asyncpg). The search query is the platform's canonical HYBRID
query: metadata filters (WHERE) combined with vector similarity ordering
(``embedding <=> $1``, cosine, served by the pipeline's HNSW index).
"""

import asyncio
from typing import Any
from uuid import UUID

import asyncpg
from pgvector import Vector
from pgvector.asyncpg import register_vector

from server.constants import MOVIE_RESULT_COLUMNS as _MOVIE_COLUMNS


async def _init_connection(connection: asyncpg.Connection) -> None:
    await register_vector(connection)


class Database:
    """Lazy asyncpg pool + the read-only queries backing the MCP tools."""

    def __init__(self, database_url: str, min_size: int = 1, max_size: int = 10):
        # asyncpg speaks postgresql:// directly (strip any SQLAlchemy driver suffix).
        self._dsn = database_url.replace("postgresql+psycopg://", "postgresql://", 1)
        self._min_size = min_size
        self._max_size = max_size
        self._pool: asyncpg.Pool | None = None
        self._lock = asyncio.Lock()

    async def pool(self) -> asyncpg.Pool:
        if self._pool is None:
            async with self._lock:
                if self._pool is None:
                    self._pool = await asyncpg.create_pool(
                        self._dsn,
                        min_size=self._min_size,
                        max_size=self._max_size,
                        init=_init_connection,
                    )
        return self._pool

    async def close(self) -> None:
        if self._pool is not None:
            await self._pool.close()
            self._pool = None

    async def ping(self) -> None:
        pool = await self.pool()
        await pool.fetchval("SELECT 1")

    async def search_movies_by_embedding(
        self,
        query_embedding: list[float],
        top_k: int = 10,
        genre: str | None = None,
        min_imdb_rating: float | None = None,
        mpaa_rating: str | None = None,
        decade: int | None = None,
    ) -> list[tuple[Any, float]]:
        """Hybrid search: metadata filters + cosine-ranked vector similarity.

        Returns ``(record, similarity)`` pairs, best first, similarity in [0, 1]
        (1 − cosine distance). Movies without an embedding never match.
        """
        conditions = ["embedding IS NOT NULL"]
        params: list[Any] = [Vector(query_embedding)]

        for condition, value in (
            ("major_genre = ${}", genre),
            ("imdb_rating >= ${}", min_imdb_rating),
            ("mpaa_rating = ${}", mpaa_rating),
            ("decade = ${}", decade),
        ):
            if value is not None:
                params.append(value)
                conditions.append(condition.format(len(params)))

        params.append(top_k)
        sql = f"""
            SELECT {_MOVIE_COLUMNS}, 1 - (embedding <=> $1) AS similarity
            FROM movies
            WHERE {" AND ".join(conditions)}
            ORDER BY embedding <=> $1
            LIMIT ${len(params)}
        """

        pool = await self.pool()
        rows = await pool.fetch(sql, *params)
        return [(row, float(row["similarity"])) for row in rows]

    async def get_movie_by_id(self, movie_id: UUID) -> asyncpg.Record | None:
        pool = await self.pool()
        return await pool.fetchrow(
            f"SELECT {_MOVIE_COLUMNS} FROM movies WHERE id = $1", movie_id
        )

    async def get_movie_by_title(self, title: str) -> asyncpg.Record | None:
        """Exact (case-insensitive) title match first, then fuzzy substring match."""
        pool = await self.pool()
        needle = title.strip()

        exact = await pool.fetchrow(
            f"SELECT {_MOVIE_COLUMNS} FROM movies WHERE lower(title) = lower($1) "
            "ORDER BY release_year LIMIT 1",
            needle,
        )
        if exact is not None:
            return exact

        return await pool.fetchrow(
            f"SELECT {_MOVIE_COLUMNS} FROM movies WHERE title ILIKE '%' || $1 || '%' "
            "ORDER BY length(title), title LIMIT 1",
            needle,
        )

    async def get_similar_movies(
        self, movie_id: UUID, top_k: int = 5
    ) -> list[tuple[Any, float]] | None:
        """Nearest neighbours of a movie, excluding itself.

        ``None`` when the movie does not exist; ``[]`` when it has no embedding.
        """
        pool = await self.pool()
        source = await pool.fetchrow("SELECT embedding FROM movies WHERE id = $1", movie_id)
        if source is None:
            return None
        if source["embedding"] is None:
            return []

        rows = await pool.fetch(
            f"""
            SELECT {_MOVIE_COLUMNS}, 1 - (embedding <=> $1) AS similarity
            FROM movies
            WHERE id != $2 AND embedding IS NOT NULL
            ORDER BY embedding <=> $1
            LIMIT $3
            """,
            source["embedding"],
            movie_id,
            top_k,
        )
        return [(row, float(row["similarity"])) for row in rows]

    async def list_genres(self) -> list[str]:
        pool = await self.pool()
        rows = await pool.fetch(
            "SELECT DISTINCT major_genre FROM movies ORDER BY major_genre"
        )
        return [row["major_genre"] for row in rows]

    async def get_dataset_stats(self) -> asyncpg.Record:
        pool = await self.pool()
        return await pool.fetchrow(
            """
            SELECT count(*)                        AS total_movies,
                   count(embedding)                AS with_embeddings,
                   count(DISTINCT major_genre)     AS genres,
                   min(release_year)               AS min_year,
                   max(release_year)               AS max_year,
                   avg(imdb_rating)                AS avg_imdb_rating,
                   max(pipeline_version)           AS pipeline_version
            FROM movies
            """
        )
