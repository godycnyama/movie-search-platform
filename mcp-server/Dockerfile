# Python FastMCP server — semantic movie-search tools over SSE (spec Part 3).
FROM ghcr.io/astral-sh/uv:python3.13-bookworm-slim

# curl is used by the docker-compose healthcheck (GET /health).
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

ENV UV_COMPILE_BYTECODE=1 \
    UV_LINK_MODE=copy \
    PYTHONPATH=/app/src

WORKDIR /app

# Install dependencies first so they cache independently of source changes.
COPY pyproject.toml uv.lock ./
RUN uv sync --frozen --no-dev --no-install-project

COPY src ./src

EXPOSE 8000

# uvicorn serves the FastMCP ASGI app (server/asgi.py factory). Shell form so
# the MCP_HOST/MCP_PORT env vars are honored. The app's logging setup re-routes
# uvicorn's loggers into the structured JSON stream.
CMD uv run --no-sync uvicorn --factory server.asgi:create_app \
    --host "${MCP_HOST:-0.0.0.0}" --port "${MCP_PORT:-8000}"
