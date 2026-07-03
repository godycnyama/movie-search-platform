"""Movie Search Platform MCP server (assessment spec Part 3).

FastMCP server exposing the five semantic-search tools over SSE locally
(transport configurable for production via MCP_TRANSPORT). Includes a
GET /health endpoint reporting per-dependency status, structured JSON logging
with request tracing, an asyncpg pool to pgvector, and Ollama-backed query
embeddings — all configured through pydantic-settings (src/config.py).
"""

import logging

import uvicorn
from fastmcp import FastMCP
from starlette.requests import Request
from starlette.responses import JSONResponse

from config import Settings
from server.db import Database
from server.embeddings import OllamaEmbeddingsClient
from server.logging_config import configure_logging
from server.tools import register_tools

logger = logging.getLogger(__name__)


def create_server(settings: Settings, db: Database, embeddings: OllamaEmbeddingsClient) -> FastMCP:
    """Builds the FastMCP server with tools and the /health route registered."""
    mcp = FastMCP(name="movie-search")

    register_tools(mcp, db, embeddings, settings)

    @mcp.custom_route("/health", methods=["GET"])
    async def health(request: Request) -> JSONResponse:
        """Liveness/readiness: healthy only when pgvector is reachable."""
        try:
            await db.ping()
            postgres = "healthy"
        except Exception:  # noqa: BLE001 — any failure means unhealthy, never crash health.
            logger.warning("health check: postgres unreachable", exc_info=True)
            postgres = "unhealthy"

        status = "healthy" if postgres == "healthy" else "unhealthy"
        return JSONResponse(
            {"status": status, "dependencies": {"postgres": postgres}},
            status_code=200 if status == "healthy" else 503,
        )

    return mcp


def main() -> None:
    settings = Settings()
    configure_logging(settings.log_level)

    db = Database(settings.database_url, settings.db_pool_min_size, settings.db_pool_max_size)
    embeddings = OllamaEmbeddingsClient(
        settings.ollama_url, settings.embedding_model, settings.embedding_dim
    )

    mcp = create_server(settings, db, embeddings)

    logger.info(
        "starting MCP server",
        extra={"transport": settings.mcp_transport},
    )
    if settings.mcp_transport == "stdio":
        mcp.run(transport="stdio")
        return

    # HTTP transports (sse / streamable-http): FastMCP builds the ASGI app
    # (tools endpoint + /health custom route), uvicorn serves it.
    # log_config=None keeps uvicorn's loggers propagating to our JSON root handler.
    app = mcp.http_app(transport=settings.mcp_transport)
    uvicorn.run(app, host=settings.mcp_host, port=settings.mcp_port, log_config=None)


if __name__ == "__main__":
    main()
