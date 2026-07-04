# Embedding Atlas (bonus) — exports the embedded catalogue from pgvector and
# serves Apple's Embedding Atlas UI on :7000 (see docker-compose `atlas`).
FROM ghcr.io/astral-sh/uv:python3.13-bookworm-slim

# curl is used by the docker-compose healthcheck.
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Dependencies live in a venv outside /app: docker-compose mounts ./scripts
# over /app read-only, so the image must not rely on anything installed there.
RUN uv venv /opt/venv \
    && uv pip install --python /opt/venv/bin/python \
        embedding-atlas "psycopg[binary]" pandas pyarrow
ENV PATH="/opt/venv/bin:$PATH"

WORKDIR /app

EXPOSE 7000

# Export the catalogue (writes outside the read-only mount), then serve the
# atlas. UMAP projection is computed from the exported 768-dim vectors.
CMD python export_embeddings_atlas.py /tmp/movies.parquet \
    && embedding-atlas /tmp/movies.parquet \
        --text augmented_text --vector embedding \
        --host 0.0.0.0 --port 7000
