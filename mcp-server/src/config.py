"""MCP server configuration via pydantic-settings (spec §3.2: environment-based,
no hardcoded values).

Every knob comes through :class:`Settings` — typed, validated at startup, and
sourced from environment variables (see the docker-compose ``mcp-server``
service) or a local ``.env`` file. Field names map case-insensitively onto the
env vars (``database_url`` <- ``DATABASE_URL``). Secrets (the password inside
``DATABASE_URL``) come from the environment only; the committed default is
credential-free.
"""

from typing import Literal

from pydantic import Field
from pydantic_settings import BaseSettings, SettingsConfigDict

from server.constants import EMBEDDING_DIM


class Settings(BaseSettings):
    model_config = SettingsConfigDict(
        env_file=".env",
        env_file_encoding="utf-8",
        extra="ignore",
    )

    database_url: str = Field(
        default="postgresql://localhost:5432/movies",
        description="PostgreSQL DSN; credentials come from the environment, never committed.",
    )

    db_pool_min_size: int = Field(default=1, ge=0, description="asyncpg pool floor.")
    db_pool_max_size: int = Field(default=10, ge=1, description="asyncpg pool ceiling.")

    # --- Embeddings: swappable backend (see server/embeddings.py) ------------
    # ENV selects the query-embedding backend: `local` uses the TEI `embeddings`
    # container, `dev`/`prod` use Amazon Bedrock. MUST match the backend (and
    # model) the pipeline embedded the catalogue with, or cosine similarity is
    # meaningless. `embedding_provider` can override the environment-derived
    # choice explicitly (mainly for tests).
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
        description="Base URL of the TEI server that embeds search queries (local backend).",
    )

    embedding_model: str = Field(
        default="nomic-embed-text-v1.5",
        description="Embedding model label; MUST be the same model the pipeline embedded with.",
    )

    bedrock_region: str = Field(
        default="us-east-1",
        description="AWS region for the Bedrock runtime (dev/prod backend).",
    )

    bedrock_embedding_model_id: str = Field(
        default="amazon.titan-embed-text-v2:0",
        description="Bedrock embedding model id; must match the pipeline's Bedrock model.",
    )

    embedding_dim: int = Field(
        default=EMBEDDING_DIM,
        ge=1,
        description="Expected embedding dimensionality; must match the pgvector vector(N) column.",
    )

    mcp_host: str = Field(default="127.0.0.1", description="Bind host (0.0.0.0 in containers).")
    mcp_port: int = Field(default=8000, ge=1, le=65535, description="Bind port.")

    mcp_transport: Literal["sse", "streamable-http", "stdio"] = Field(
        default="sse",
        description="SSE locally per the spec; configurable for production.",
    )

    log_level: str = Field(default="INFO", description="Python logging level name.")
