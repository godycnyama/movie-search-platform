"""SQLModel table models shared across the platform.

These mirror the pipeline's ``models.py`` and the .NET API's Domain entities
(``api/.../Domain/Entities/Movie.cs`` and ``User.cs``): EF Core maps its
PascalCase properties through a snake_case naming convention, so the field
names here match the physical columns one-to-one (``movies`` / ``users``
tables). If a field is added in any of the three codebases, update the others.

The MCP server only reads these tables — the pipeline owns the movies schema,
the API owns users.
"""

from datetime import UTC, date, datetime
from typing import Any
from uuid import UUID, uuid4

from pgvector.sqlalchemy import Vector
from pydantic import BaseModel
from pydantic import Field as PydanticField
from sqlalchemy import BigInteger, Column, DateTime
from sqlmodel import Field, SQLModel

from server.constants import EMBEDDING_DIM, UserRoles


def _utc_now() -> datetime:
    return datetime.now(UTC)


class Movie(SQLModel, table=True):
    """A movie record from the Vega dataset, cleaned, augmented and embedded.

    Imputation policy (mirrors the pipeline / .NET entity docs): fields that
    feed ranking/filtering (ratings, budget, gross) stay NULL rather than
    fabricated when unknown; categorical fields are imputed with a real
    "Unknown" / "Not Rated" category.
    """

    __tablename__ = "movies"

    id: UUID = Field(default_factory=uuid4, primary_key=True)
    title: str
    release_date: date | None = None
    release_year: int | None = None
    major_genre: str = "Unknown"
    director: str = "Unknown"
    distributor: str = "Unknown"
    mpaa_rating: str = "Not Rated"
    creative_type: str = "Unknown"
    source: str = "Unknown"
    imdb_rating: float | None = None
    imdb_votes: int | None = None
    rotten_tomatoes_rating: int | None = None
    production_budget: int | None = Field(default=None, sa_column=Column(BigInteger))
    worldwide_gross: int | None = Field(default=None, sa_column=Column(BigInteger))
    running_time_min: int | None = None
    runtime_imputed: bool = False

    # Derived features (README §6 Data Decisions).
    budget_tier: str | None = None
    decade: int | None = None
    blockbuster_flag: bool = False

    # Embedding.
    augmented_text: str = ""
    embedding: list[float] | None = Field(
        default=None, sa_column=Column(Vector(EMBEDDING_DIM))
    )

    # Audit columns.
    created_at: datetime = Field(
        default_factory=_utc_now,
        sa_column=Column(DateTime(timezone=True), nullable=False),
    )
    updated_at: datetime = Field(
        default_factory=_utc_now,
        sa_column=Column(DateTime(timezone=True), nullable=False),
    )
    pipeline_version: str | None = None


class User(SQLModel, table=True):
    """An API user account (owned by the .NET API; see 002_create_users.sql)."""

    __tablename__ = "users"

    id: UUID = Field(default_factory=uuid4, primary_key=True)
    email: str = Field(max_length=320, unique=True)
    password_hash: str = Field(max_length=512)
    role: str = Field(default=UserRoles.READER, max_length=32)
    created_at: datetime = Field(
        default_factory=_utc_now,
        sa_column=Column(DateTime(timezone=True), nullable=False),
    )
    updated_at: datetime = Field(
        default_factory=_utc_now,
        sa_column=Column(DateTime(timezone=True), nullable=False),
    )


# --- MCP tool response models (Pydantic v2, spec §3.1/§3.2) -------------------


class MovieResult(BaseModel):
    """A movie as returned by the MCP tools; ranked results carry a similarity score."""

    id: UUID = PydanticField(description="Stable movie identifier.")
    title: str = PydanticField(description="Movie title.")
    release_year: int | None = PydanticField(default=None, description="Release year.")
    major_genre: str | None = PydanticField(default=None, description="Primary genre.")
    director: str | None = PydanticField(default=None, description="Director.")
    distributor: str | None = PydanticField(default=None, description="Distributor.")
    mpaa_rating: str | None = PydanticField(default=None, description="MPAA rating.")
    imdb_rating: float | None = PydanticField(default=None, description="IMDB rating in [0, 10].")
    rotten_tomatoes_rating: int | None = PydanticField(
        default=None, description="Rotten Tomatoes score in [0, 100]."
    )
    production_budget: int | None = PydanticField(
        default=None, description="Production budget in USD; null when unknown."
    )
    running_time_min: int | None = PydanticField(default=None, description="Runtime in minutes.")
    budget_tier: str | None = PydanticField(
        default=None, description="Derived budget bucket (low/mid/high/blockbuster)."
    )
    decade: int | None = PydanticField(default=None, description="Release decade, e.g. 1990.")
    similarity_score: float | None = PydanticField(
        default=None,
        description="Cosine similarity in [0, 1] for ranked results; null for direct lookups.",
    )

    @classmethod
    def from_record(
        cls, record: dict[str, Any] | Any, similarity: float | None = None
    ) -> "MovieResult":
        """Builds a result from an asyncpg record (mapping access by column name)."""
        return cls(
            id=record["id"],
            title=record["title"],
            release_year=record["release_year"],
            major_genre=record["major_genre"],
            director=record["director"],
            distributor=record["distributor"],
            mpaa_rating=record["mpaa_rating"],
            imdb_rating=record["imdb_rating"],
            rotten_tomatoes_rating=record["rotten_tomatoes_rating"],
            production_budget=record["production_budget"],
            running_time_min=record["running_time_min"],
            budget_tier=record["budget_tier"],
            decade=record["decade"],
            similarity_score=round(similarity, 4) if similarity is not None else None,
        )


class DatasetStats(BaseModel):
    """Summary statistics about the movie dataset (matches the API's /stats shape)."""

    total_movies: int = PydanticField(description="Total rows in the catalogue.")
    with_embeddings: int = PydanticField(description="Rows that have an embedding.")
    genres: int = PydanticField(description="Distinct major genres.")
    year_range: list[int] = PydanticField(
        default_factory=list, description="[min, max] release year; empty when unknown."
    )
    avg_imdb_rating: float | None = PydanticField(
        default=None, description="Mean IMDB rating over rated movies."
    )
    pipeline_version: str | None = PydanticField(
        default=None, description="Newest pipeline version stamped on the data."
    )
