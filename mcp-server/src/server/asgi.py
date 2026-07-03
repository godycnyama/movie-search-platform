"""ASGI entry point for running the MCP server under the uvicorn CLI.

    uvicorn --factory server.asgi:create_app --host 0.0.0.0 --port 8000

Used by the Dockerfile CMD. Configuration still comes entirely from
``config.Settings`` (environment variables); host/port are uvicorn CLI
concerns. For stdio transport use ``python -m server.main`` instead —
there is no HTTP server to run in that mode.
"""

from config import Settings
from server.db import Database
from server.embeddings import OllamaEmbeddingsClient
from server.logging_config import configure_logging
from server.main import create_server


def create_app():
    """Builds the FastMCP ASGI app (tools endpoint + /health) from Settings."""
    settings = Settings()
    configure_logging(settings.log_level)

    db = Database(settings.database_url, settings.db_pool_min_size, settings.db_pool_max_size)
    embeddings = OllamaEmbeddingsClient(
        settings.ollama_url, settings.embedding_model, settings.embedding_dim
    )

    mcp = create_server(settings, db, embeddings)

    transport = "sse" if settings.mcp_transport == "stdio" else settings.mcp_transport
    return mcp.http_app(transport=transport)
