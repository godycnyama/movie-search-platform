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

    ollama_url: str = Field(
        default="http://localhost:11434",
        description="Base URL of the Ollama server that embeds search queries.",
    )

    embedding_model: str = Field(
        default="nomic-embed-text",
        description="Ollama model; MUST be the same model the pipeline embedded with.",
    )

    embedding_dim: int = Field(
        default=EMBEDDING_DIM,
        ge=1,
        description="Expected embedding dimensionality; must match vector(768).",
    )

    mcp_host: str = Field(default="127.0.0.1", description="Bind host (0.0.0.0 in containers).")
    mcp_port: int = Field(default=8000, ge=1, le=65535, description="Bind port.")

    mcp_transport: Literal["sse", "streamable-http", "stdio"] = Field(
        default="sse",
        description="SSE locally per the spec; configurable for production.",
    )

    log_level: str = Field(default="INFO", description="Python logging level name.")
