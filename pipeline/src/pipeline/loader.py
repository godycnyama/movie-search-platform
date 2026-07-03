"""Stage 5 — idempotent upsert into PostgreSQL + pgvector (README §5-6).

Movie identity is a deterministic UUIDv5 of (title, release_date), so re-running
the pipeline updates rows in place instead of duplicating them. The upsert is
``INSERT ... ON CONFLICT (id) DO UPDATE`` with ``created_at`` preserved on
updates. The schema itself is owned by Alembic (database/migrations, generated
from the shared SQLModel metadata) and applied via ``alembic upgrade head``
before the pipeline runs.
"""

import logging
import uuid
from datetime import date

from sqlalchemy import Engine, create_engine
from sqlalchemy.dialects.postgresql import insert

from models import Movie
from pipeline.constants import MOVIE_ID_NAMESPACE

logger = logging.getLogger(__name__)


def movie_id(title: str, release_date: date | None) -> uuid.UUID:
    """Stable identity for a movie across pipeline runs."""
    return uuid.uuid5(MOVIE_ID_NAMESPACE, f"{title}|{release_date or ''}")


def create_db_engine(database_url: str) -> Engine:
    """Engine from a ``postgresql://`` URL (rewritten to the psycopg3 driver)."""
    url = database_url.replace("postgresql://", "postgresql+psycopg://", 1)
    return create_engine(url)


def upsert_movies(engine: Engine, movies: list[Movie]) -> int:
    """Upserts a batch; returns the number of rows written."""
    if not movies:
        return 0

    rows = [movie.model_dump() for movie in movies]
    statement = insert(Movie).values(rows)
    # Update everything the pipeline owns; keep the original created_at.
    updatable = {
        column.name: statement.excluded[column.name]
        for column in Movie.__table__.columns
        if column.name not in ("id", "created_at")
    }
    statement = statement.on_conflict_do_update(index_elements=["id"], set_=updatable)

    with engine.begin() as connection:
        connection.execute(statement)

    return len(rows)
