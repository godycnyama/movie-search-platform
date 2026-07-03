# Data pipeline — clean, impute, augment, embed (Ollama), load (pgvector).
# Runs once and exits (see docker-compose `pipeline` service).
FROM ghcr.io/astral-sh/uv:python3.13-bookworm-slim

ENV UV_COMPILE_BYTECODE=1 \
    UV_LINK_MODE=copy

WORKDIR /app

# Install dependencies first so they cache independently of source changes.
COPY pyproject.toml uv.lock ./
RUN uv sync --frozen --no-dev --no-install-project

COPY alembic.ini ./
COPY src ./src

# Apply schema migrations (Alembic versions are mounted at /database/migrations
# by docker-compose), then run the pipeline.
CMD uv run --no-sync alembic upgrade head && uv run --no-sync python src/main.py
