"""Alembic environment for the platform schema (spec Part 2).

The schema's source of truth is the pipeline's SQLModel metadata
(``pipeline/src/models.py`` — mirrored by the MCP server and the .NET API).
Alembic lives in the pipeline project (``pipeline/alembic.ini``) and writes
version files here so all services share one migrations folder.

The database URL comes from ``DATABASE_URL`` via the pipeline's
pydantic-settings (never hardcoded); autogenerate diffs against the live DB:

    cd pipeline
    uv run alembic revision --autogenerate -m "describe change"
    uv run alembic upgrade head
"""

import sys
from logging.config import fileConfig
from pathlib import Path

from alembic import context
from sqlalchemy import engine_from_config, pool

# Make the pipeline's src/ importable (models.py + pipeline package).
sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "pipeline" / "src"))

import models  # noqa: E402,F401 — importing registers Movie/User on the metadata.
from sqlmodel import SQLModel  # noqa: E402
from pipeline.settings import PipelineSettings  # noqa: E402

config = context.config

if config.config_file_name is not None:
    fileConfig(config.config_file_name)

target_metadata = SQLModel.metadata

# psycopg3 driver, same rewrite the pipeline loader uses.
config.set_main_option(
    "sqlalchemy.url",
    PipelineSettings().database_url.replace("postgresql://", "postgresql+psycopg://", 1),
)


def run_migrations_offline() -> None:
    """Emit migration SQL without a live database (``alembic upgrade --sql``)."""
    context.configure(
        url=config.get_main_option("sqlalchemy.url"),
        target_metadata=target_metadata,
        literal_binds=True,
        dialect_opts={"paramstyle": "named"},
    )

    with context.begin_transaction():
        context.run_migrations()


def run_migrations_online() -> None:
    """Run migrations against the configured database."""
    connectable = engine_from_config(
        config.get_section(config.config_ini_section, {}),
        prefix="sqlalchemy.",
        poolclass=pool.NullPool,
    )

    with connectable.connect() as connection:
        context.configure(connection=connection, target_metadata=target_metadata)

        with context.begin_transaction():
            context.run_migrations()


if context.is_offline_mode():
    run_migrations_offline()
else:
    run_migrations_online()
