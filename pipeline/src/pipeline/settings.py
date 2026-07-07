"""Pipeline configuration via pydantic-settings.

Every knob the pipeline reads comes through :class:`PipelineSettings` — typed,
validated at startup, and sourced from environment variables (see the
docker-compose ``pipeline`` service) or a local ``.env`` file. Field names map
case-insensitively onto the env vars (``database_url`` <- ``DATABASE_URL``).

Secrets (the database password inside ``DATABASE_URL``) must come from the
environment / ``.env`` — the committed default is credential-free and only
works against a local trust-auth database.
"""

from typing import Literal

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

    # --- Embeddings: swappable backend (see pipeline/embedding.py) -----------
    # ENV selects the embedding backend: `local` uses the TEI `embeddings`
    # container, `dev`/`prod` use Amazon Bedrock. The MCP server MUST use the same
    # backend/model so query and document vectors share one space.
    env: Literal["local", "dev", "prod"] = Field(
        default="local",
        description="Deployment environment; drives the default embedding backend.",
    )
    embedding_provider: Literal["auto", "tei", "bedrock"] = Field(
        default="auto",
        description="Explicit backend override; 'auto' derives it from env.",
    )

    embeddings_url: str = Field(
        default="http://localhost:8001",
        description="Base URL of the TEI server serving the embedding model (local backend).",
    )

    embedding_model: str = Field(
        default="BAAI/bge-base-en-v1.5",
        description="Embedding model label; must produce embedding_dim-sized vectors.",
    )

    bedrock_region: str = Field(
        default="us-east-1",
        description="AWS region for the Bedrock runtime (dev/prod backend).",
    )

    bedrock_embedding_model_id: str = Field(
        default="amazon.titan-embed-text-v2:0",
        description="Bedrock embedding model id; the MCP server must use the same id.",
    )

    embedding_dim: int = Field(
        default=EMBEDDING_DIM,
        ge=1,
        description="Expected embedding dimensionality; must match the pgvector vector(N) column.",
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
