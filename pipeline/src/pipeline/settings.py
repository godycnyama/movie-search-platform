"""Pipeline configuration via pydantic-settings.

Every knob the pipeline reads comes through :class:`PipelineSettings` — typed,
validated at startup, and sourced from environment variables (see the
docker-compose ``pipeline`` service) or a local ``.env`` file. Field names map
case-insensitively onto the env vars (``database_url`` <- ``DATABASE_URL``).

Secrets (the database password inside ``DATABASE_URL``) must come from the
environment / ``.env`` — the committed default is credential-free and only
works against a local trust-auth database.
"""

from pydantic import Field
from pydantic_settings import BaseSettings, SettingsConfigDict

from pipeline.constants import EMBEDDING_DIM


class PipelineSettings(BaseSettings):
    model_config = SettingsConfigDict(
        env_file=".env",
        env_file_encoding="utf-8",
        extra="ignore",
    )

    database_url: str = Field(
        default="postgresql://localhost:5432/movies",
        description="PostgreSQL DSN; credentials come from the environment, never committed.",
    )

    ollama_url: str = Field(
        default="http://localhost:11434",
        description="Base URL of the Ollama server that serves the embedding model.",
    )

    embedding_model: str = Field(
        default="nomic-embed-text",
        description="Ollama model used for embeddings; must produce embedding_dim-sized vectors.",
    )

    embedding_dim: int = Field(
        default=EMBEDDING_DIM,
        ge=1,
        description="Expected embedding dimensionality; must match the pgvector vector(768) column.",
    )

    batch_size: int = Field(
        default=64,
        ge=1,
        le=1024,
        description="Rows per embed/upsert batch.",
    )

    pipeline_version: str = Field(
        default="0.1.0",
        description="Version stamped onto every loaded row.",
    )

    log_level: str = Field(
        default="INFO",
        description="Python logging level name.",
    )
