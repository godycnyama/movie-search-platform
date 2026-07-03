"""SQLModel table models shared with the .NET API.

These mirror the API's Domain entities (``api/.../Domain/Entities/Movie.cs`` and
``User.cs``) and map onto the same PostgreSQL schema: EF Core uses a snake_case
naming convention over the PascalCase properties, so the field names here match
the physical columns one-to-one (``movies`` / ``users`` tables). If a field is
added on either side, add it to the other as well.
"""

from datetime import UTC, date, datetime
from uuid import UUID, uuid4

from pgvector.sqlalchemy import Vector
from sqlalchemy import BigInteger, Column, DateTime
from sqlmodel import Field, SQLModel

from pipeline.constants import EMBEDDING_DIM, UserRoles


def _utc_now() -> datetime:
    return datetime.now(UTC)


class Movie(SQLModel, table=True):
    """A movie record from the Vega dataset, cleaned, augmented and embedded.

    Imputation policy (mirrors the .NET entity docs): fields that feed
    ranking/filtering (ratings, budget, gross) stay NULL rather than fabricated
    when unknown; categorical fields are imputed with a real "Unknown" /
    "Not Rated" category.
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
    """An API user account (owned by the .NET API; see 002_create_users.sql).

    The pipeline never writes users — the model exists so both codebases agree
    on the schema and the pipeline can join/inspect if ever needed.
    """

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
