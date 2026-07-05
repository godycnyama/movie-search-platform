"""Shared fixtures: a FastMCP server wired to fake database/embeddings backends.

Tools are exercised end to end through FastMCP's in-memory client transport, so
schemas, validation, structured output and error mapping behave exactly as they
do over SSE/streamable-http — only postgres and the embeddings service are replaced.
"""

import uuid
from typing import Any

import pytest
from fastmcp import FastMCP

from config import Settings
from server.tools import register_tools

KNOWN_ID = uuid.UUID("9f8b6a4e-1c2d-4e3f-8a5b-6c7d8e9f0a1b")
UNEMBEDDED_ID = uuid.UUID("00000000-0000-0000-0000-00000000feed")
UNKNOWN_ID = uuid.UUID("00000000-0000-0000-0000-000000000001")

EMBEDDING_DIM = 768


def movie_record(**overrides: Any) -> dict[str, Any]:
    """A row shaped like the asyncpg record the real queries return."""
    record = {
        "id": KNOWN_ID,
        "title": "Heat",
        "release_year": 1995,
        "major_genre": "Drama",
        "director": "Michael Mann",
        "distributor": "Warner Bros.",
        "mpaa_rating": "R",
        "imdb_rating": 8.3,
        "rotten_tomatoes_rating": 88,
        "production_budget": 60_000_000,
        "running_time_min": 170,
        "budget_tier": "high",
        "decade": 1990,
    }
    record.update(overrides)
    return record


class FakeDatabase:
    """Canned catalogue with the same contract as ``server.db.Database``."""

    def __init__(self):
        self.search_kwargs: dict[str, Any] | None = None
        self.stats_row: dict[str, Any] = {
            "total_movies": 3201,
            "with_embeddings": 3200,
            "genres": 12,
            "min_year": 1920,
            "max_year": 2010,
            "avg_imdb_rating": 6.284,
            "pipeline_version": "0.1.0",
        }

    async def ping(self) -> None:
        pass

    async def search_movies_by_embedding(self, query_embedding, **kwargs):
        self.search_kwargs = {"query_embedding": query_embedding, **kwargs}
        return [(movie_record(), 0.91344), (movie_record(title="Ronin"), 0.8321)]

    async def get_movie_by_id(self, movie_id):
        return movie_record() if movie_id == KNOWN_ID else None

    async def get_movie_by_title(self, title):
        return movie_record() if title.strip().lower() == "heat" else None

    async def get_similar_movies(self, movie_id, top_k=5):
        if movie_id == KNOWN_ID:
            return [(movie_record(title="Ronin"), 0.8321)]
        if movie_id == UNEMBEDDED_ID:
            return []  # exists but has no embedding yet
        return None  # movie does not exist

    async def list_genres(self):
        return ["Action", "Drama"]

    async def get_dataset_stats(self):
        return self.stats_row


class FakeEmbeddings:
    """Deterministic stand-in for the TEI query-embedding client."""

    def __init__(self):
        self.embedded: list[str] = []

    async def embed_query(self, text: str) -> list[float]:
        self.embedded.append(text)
        return [0.5] * EMBEDDING_DIM


@pytest.fixture
def fake_db() -> FakeDatabase:
    return FakeDatabase()


@pytest.fixture
def fake_embeddings() -> FakeEmbeddings:
    return FakeEmbeddings()


@pytest.fixture
def mcp(fake_db, fake_embeddings) -> FastMCP:
    server = FastMCP(name="movie-search-test")
    register_tools(server, fake_db, fake_embeddings, Settings())
    return server


def payload(result) -> Any:
    """Unwraps FastMCP's ``{"result": ...}`` structured-output envelope."""
    structured = result.structured_content
    if isinstance(structured, dict) and set(structured) == {"result"}:
        return structured["result"]
    return structured
