# Data pipeline — clean, impute, augment, embed (TEI), load (pgvector).
# Runs once and exits (compose `pipeline` service / ECS one-off task).
#
# Build context is the REPOSITORY ROOT (not pipeline/) so the Alembic versions
# in database/migrations can be baked into the image — required on ECS Fargate
# where there is no host volume to mount them from:
#   docker build -f pipeline/Dockerfile .
FROM ghcr.io/astral-sh/uv:python3.13-bookworm-slim

ENV UV_COMPILE_BYTECODE=1 \
    UV_LINK_MODE=copy

WORKDIR /app

# Install dependencies first so they cache independently of source changes.
COPY pipeline/pyproject.toml pipeline/uv.lock ./
RUN uv sync --frozen --no-dev --no-install-project

COPY pipeline/alembic.ini ./
COPY pipeline/src ./src

# Make models.py + the pipeline package importable. Alembic's env.py derives
# pipeline/src from the repo layout (…/database/migrations sibling of pipeline/),
# but the image flattens that to /app/src and /database/migrations, so that
# derivation misses. `python src/main.py` gets /app/src on sys.path[0] for free;
# alembic (run from /database/migrations) does not — PYTHONPATH covers both.
ENV PYTHONPATH=/app/src

# alembic.ini resolves the versions as %(here)s/../database/migrations, i.e.
# /database/migrations. docker-compose still bind-mounts the same path read-only
# for local live-editing; on ECS the baked copy is used.
COPY database/migrations /database/migrations

# Apply schema migrations, then run the pipeline.
CMD uv run --no-sync alembic upgrade head && uv run --no-sync python src/main.py
