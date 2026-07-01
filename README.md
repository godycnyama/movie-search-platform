# 🎬 Movie Search Platform

Semantic movie search platform: an ingestion **data pipeline** that cleans and embeds a movie
catalogue, a **PostgreSQL + pgvector** store, a **REST API** for search, and an **MCP server** that
exposes the same capabilities to LLM agents — all observable and deployable to AWS via Terraform.

> ⚠️ **PLACEHOLDER — Project status:** This repository currently contains scaffolding only.
> [mcp-server/](mcp-server/) and [pipeline/](pipeline/) hold `uv`-generated stubs; the
> [api/](api/), [database/](database/), [monitoring/](monitoring/), [scripts/](scripts/), and
> [terraform/](terraform/) directories are placeholders. Sections below marked ⚠️ **PLACEHOLDER**
> describe the intended design and must be updated once the corresponding components are implemented.

---

## Table of Contents

1. [Architecture](#1-architecture)
2. [Prerequisites](#2-prerequisites)
3. [Quick Start](#3-quick-start)
4. [Service Endpoints](#4-service-endpoints)
5. [Data Pipeline](#5-data-pipeline)
6. [Data Decisions](#6-data-decisions)
7. [Embedding Strategy](#7-embedding-strategy)
8. [MCP Server](#8-mcp-server)
9. [API Documentation](#9-api-documentation)
10. [Authentication](#10-authentication)
11. [Observability](#11-observability)
12. [Terraform Deployment](#12-terraform-deployment)
13. [Running Tests](#13-running-tests)
14. [Known Limitations & Future Improvements](#14-known-limitations--future-improvements)

---

## 1. Architecture

```
                        ┌────────────────────────────────────────────────────────┐
                        │                     Clients / Users                     │
                        │        (web UI, curl, LLM agents via MCP host)           │
                        └───────────────┬───────────────────────┬─────────────────┘
                                        │ HTTPS/REST             │ MCP (stdio / HTTP)
                                        ▼                        ▼
                        ┌───────────────────────┐    ┌───────────────────────────┐
                        │        REST API        │    │        MCP Server         │
                        │      (api/, :8000)     │    │   (mcp-server/, :8080)    │
                        │  auth · search · CRUD  │    │  search_movies, get_movie │
                        └───────────┬───────────┘    └─────────────┬─────────────┘
                                    │                              │
                                    │        query embeddings      │
                                    ▼                              ▼
                        ┌───────────────────────┐    ┌───────────────────────────┐
                        │   Embedding Service    │◄───┤   Shared query embedder   │
                        │  (embeddings/, :8001)  │    └───────────────────────────┘
                        └───────────┬───────────┘
                                    │ vectors
                                    ▼
                        ┌────────────────────────────────────────────────────────┐
                        │            PostgreSQL 16 + pgvector (:5432)              │
                        │        movies · embeddings (vector) · metadata           │
                        └───────────────────────────▲────────────────────────────┘
                                                     │ bulk load + embed
                        ┌────────────────────────────┴───────────────────────────┐
                        │                     Data Pipeline                        │
                        │   (pipeline/)  ingest → clean/impute → embed → load      │
                        │                 source: raw movie dataset                │
                        └──────────────────────────────────────────────────────────┘

  Observability: OpenTelemetry traces + Prometheus metrics + structured logs → monitoring/ stack
  Deployment:    Terraform (terraform/) → AWS (ECS/Fargate + RDS + ALB)   ⚠️ PLACEHOLDER
```

> ⚠️ **PLACEHOLDER:** Replace with an authoritative diagram (e.g. a linked
> `docs/architecture.png` exported from draw.io / Excalidraw) once component boundaries are final.

---

## 2. Prerequisites

Exact versions the platform is developed and tested against:

| Tool | Required version | Notes |
|------|------------------|-------|
| Python | **3.13** | Pinned via `.python-version` in [pipeline/](pipeline/.python-version) and [mcp-server/](mcp-server/.python-version) |
| [uv](https://github.com/astral-sh/uv) | **≥ 0.5** | Python package & venv manager used by both services |
| Docker Engine | **≥ 24.0** | Required for Compose stack |
| Docker Compose | **v2 (≥ 2.20)** | `docker compose`, not the legacy `docker-compose` |
| PostgreSQL | **16.x** with `pgvector` **≥ 0.7** | Provided via Docker Compose; only needed standalone for local DB work |
| Terraform | ⚠️ **PLACEHOLDER** (target: **≥ 1.7**) | For AWS deployment |
| AWS CLI | ⚠️ **PLACEHOLDER** (target: **v2**) | Authenticated to the target account |

> Install tools from official sources only. New project dependencies should go through the
> organisation's approved dependency-vetting process before use.

---

## 3. Quick Start

From a fresh clone to a running platform in ≤ 5 commands:

```bash
# 1. Clone
git clone <REPO_URL> movie-search-platform && cd movie-search-platform

# 2. Create your local environment file (never commit this — it is git-ignored)
cp .env.example .env          # ⚠️ PLACEHOLDER: .env.example to be added; edit values as needed

# 3. Build & start all services (DB, API, MCP server, embedding service, observability)
docker compose up -d --build

# 4. Run the data pipeline to ingest, clean, embed, and load the movie catalogue
docker compose run --rm pipeline

# 5. Verify the platform is up
curl http://localhost:8000/health
```

> **Credentials:** All secrets (DB passwords, API signing keys, model/API keys) are read from the
> local `.env` file, which is excluded from version control via `.gitignore`. Never hardcode
> credentials in code or committed config. If this platform is to be exposed to a wider internal
> audience, contact the AI Engineering team before deploying.

> ⚠️ **PLACEHOLDER:** `docker-compose.yml`, `.env.example`, and the `pipeline` service definition
> do not exist yet. Update this section once they are committed.

---

## 4. Service Endpoints

Local URLs when running via Docker Compose (default ports — ⚠️ **PLACEHOLDER**, confirm against
`docker-compose.yml` once it exists):

| Service | URL | Purpose |
|---------|-----|---------|
| REST API | http://localhost:8000 | Search & catalogue API |
| API — health | http://localhost:8000/health | Liveness/readiness probe |
| API — OpenAPI docs | http://localhost:8000/docs | Interactive Swagger UI |
| MCP Server | http://localhost:8080 | MCP endpoint for LLM agents |
| Embedding Service | http://localhost:8001 | Text → vector embedding |
| PostgreSQL + pgvector | postgresql://localhost:5432 | Primary datastore |
| Prometheus | http://localhost:9090 | Metrics | 
| Grafana | http://localhost:3000 | Dashboards |
| Jaeger / Tempo UI | http://localhost:16686 | Distributed traces |

---

## 5. Data Pipeline

**Location:** [pipeline/](pipeline/) — entrypoint [pipeline/main.py](pipeline/main.py).

### How it works

The pipeline transforms a raw movie dataset into a searchable, embedded catalogue in four stages:

1. **Ingest** — read the source dataset (⚠️ **PLACEHOLDER:** source & format, e.g. TMDB/IMDb CSV or JSON).
2. **Clean & impute** — normalise fields, handle missing values (see [Data Decisions](#6-data-decisions)).
3. **Embed** — build a text representation per movie and call the embedding service to produce vectors
   (see [Embedding Strategy](#7-embedding-strategy)).
4. **Load** — upsert rows and vectors into PostgreSQL/pgvector.

### How to re-run

```bash
# Full run (idempotent upsert)
docker compose run --rm pipeline

# Or locally with uv
cd pipeline
uv sync
uv run python main.py            # ⚠️ PLACEHOLDER: add flags e.g. --full-refresh / --stage embed
```

### How to verify

```bash
# Row count in the catalogue
docker compose exec db psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" \
  -c "SELECT count(*) AS movies, count(embedding) AS embedded FROM movies;"

# Smoke-test a semantic search end to end
curl "http://localhost:8000/search?q=space+opera+with+a+rogue+ai"
```

Expected: `embedded` equals `movies` (every movie has a vector), and search returns ranked results.

> ⚠️ **PLACEHOLDER:** Document exact record counts, expected runtime, and a data-quality report
> once the pipeline is implemented.

---

## 6. Data Decisions

Imputation and cleaning strategies, with rationale. ⚠️ **PLACEHOLDER — the table below is the
intended policy; confirm/adjust once the pipeline is implemented.**

| Field | Missing-value strategy | Why |
|-------|------------------------|-----|
| `title` | Drop record | A movie with no title is not searchable or identifiable |
| `overview` / `plot` | Impute empty string; flag `has_overview = false` | Preserve the row for metadata search; avoid biasing embeddings with fabricated text |
| `release_year` | Leave `NULL` (no imputation) | Guessing a year corrupts time-based filtering; `NULL` is honest |
| `genres` | Impute `["unknown"]` | Keeps faceting stable without inventing genres |
| `runtime` | Median imputation within genre | Numeric completeness for filters; median is robust to outliers |
| `rating` / `vote_average` | Leave `NULL` | Do not fabricate popularity signals |
| Duplicates | De-duplicate by external ID, else by normalised `(title, year)` | Prevent double-counting in search results |

**Principle:** never impute values that feed the embedding text or that users filter/sort on in a way
that would mislead — prefer `NULL` + a boolean presence flag over fabricated data.

---

## 7. Embedding Strategy

⚠️ **PLACEHOLDER — the model choice below is a recommended default; confirm the committed choice.**

### Model choice & rationale

- **Model:** `sentence-transformers/all-MiniLM-L6-v2` (recommended default).
- **Why:** small (~80 MB), fast on CPU, strong quality-per-cost for short-to-medium text, no external
  API dependency (keeps movie data in-house), and permissively licensed. A larger model
  (e.g. `bge-large-en-v1.5`) can be swapped in if retrieval quality needs to improve.
- **Dimensionality:** **384** (fixed by `all-MiniLM-L6-v2`). The pgvector column is declared
  `vector(384)` — this **must** match the model or loads will fail.

> If a hosted embedding API is chosen instead, its API key must live in `.env` (never committed),
> and the dimensionality/column type must be updated accordingly.

### How the embedding container is wired into Docker Compose

⚠️ **PLACEHOLDER — intended wiring; add once `docker-compose.yml` exists:**

```yaml
services:
  embeddings:
    build: ./embeddings
    ports:
      - "8001:8001"
    environment:
      EMBEDDING_MODEL: sentence-transformers/all-MiniLM-L6-v2
      MODEL_CACHE_DIR: /models
    volumes:
      - model-cache:/models        # persist downloaded weights across restarts

  pipeline:
    build: ./pipeline
    depends_on: [embeddings, db]
    environment:
      EMBEDDING_URL: http://embeddings:8001

  api:
    build: ./api
    depends_on: [embeddings, db]
    environment:
      EMBEDDING_URL: http://embeddings:8001   # query-time embedding reuses the same service

volumes:
  model-cache:
```

Both the pipeline (document embedding) and the API (query embedding) call the **same** embedding
service so document and query vectors are always produced by the identical model.

### How the embedding text was constructed

Each movie is serialised into a single text block before embedding, so that title, plot, and key
metadata all contribute to semantic similarity:

```
Title: {title} ({release_year})
Genres: {genres joined by ", "}
Overview: {overview}
```

⚠️ **PLACEHOLDER:** Confirm the exact template and which fields are included once the pipeline is
implemented. Fields imputed as empty (e.g. missing overview) are omitted rather than injected as
placeholder text.

---

## 8. MCP Server

**Location:** [mcp-server/](mcp-server/) — entrypoint [mcp-server/main.py](mcp-server/main.py).

The MCP (Model Context Protocol) server exposes the platform's search capabilities to LLM agents.

### Available tools

⚠️ **PLACEHOLDER — intended tool surface; update once tools are registered in
[mcp-server/main.py](mcp-server/main.py):**

| Tool | Description | Arguments |
|------|-------------|-----------|
| `search_movies` | Semantic search over the catalogue | `query: string`, `limit?: int` |
| `get_movie` | Fetch a single movie by ID | `movie_id: string` |
| `filter_movies` | Structured filter (genre, year range, rating) | `genre?`, `year_from?`, `year_to?`, `min_rating?` |

### How to test the tools directly

```bash
# List available tools using the MCP Inspector (no code required)
npx @modelcontextprotocol/inspector uv run --directory mcp-server python main.py

# Or, if the server runs over HTTP via Compose:
curl -X POST http://localhost:8080/mcp \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/list"}'
```

⚠️ **PLACEHOLDER:** Confirm transport (stdio vs HTTP) and the exact invocation once implemented.

---

## 9. API Documentation

Interactive docs are served at http://localhost:8000/docs (Swagger UI). Endpoints below are the
intended contract — ⚠️ **PLACEHOLDER, confirm once [api/](api/) is implemented.**

### `GET /health`

```bash
curl http://localhost:8000/health
```
```json
{ "status": "ok", "db": "up", "embeddings": "up" }
```

### `GET /search` — semantic search

```bash
curl "http://localhost:8000/search?q=space%20opera%20with%20a%20rogue%20ai&limit=3" \
  -H "Authorization: Bearer $TOKEN"
```
```json
{
  "query": "space opera with a rogue ai",
  "results": [
    { "id": "603", "title": "The Matrix", "year": 1999, "score": 0.82 },
    { "id": "62", "title": "2001: A Space Odyssey", "year": 1968, "score": 0.79 }
  ]
}
```

### `GET /movies/{id}` — fetch by ID

```bash
curl http://localhost:8000/movies/603 -H "Authorization: Bearer $TOKEN"
```
```json
{ "id": "603", "title": "The Matrix", "year": 1999, "genres": ["Action","Sci-Fi"], "overview": "..." }
```

### `POST /movies` — create (admin) ⚠️ PLACEHOLDER

```bash
curl -X POST http://localhost:8000/movies \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"title":"New Film","year":2026,"overview":"..."}'
```
```json
{ "id": "10001", "status": "created" }
```

---

## 10. Authentication

⚠️ **PLACEHOLDER — intended scheme; confirm once auth is implemented.**

The API uses **bearer tokens (JWT)**.

### Obtain a token

```bash
curl -X POST http://localhost:8000/auth/token \
  -H "Content-Type: application/json" \
  -d '{"username":"'"$API_USER"'","password":"'"$API_PASSWORD"'"}'
```
```json
{ "access_token": "eyJhbGciOi...", "token_type": "bearer", "expires_in": 3600 }
```

### Use the token

```bash
TOKEN="eyJhbGciOi..."
curl "http://localhost:8000/search?q=heist" -H "Authorization: Bearer $TOKEN"
```

- Credentials (`API_USER`, `API_PASSWORD`) and the JWT signing secret are read from `.env` — never
  hardcoded or committed.
- Tokens expire; request a new one via `/auth/token` when you receive `401 Unauthorized`.

---

## 11. Observability

**Location:** [monitoring/](monitoring/). ⚠️ **PLACEHOLDER — intended stack; confirm once configured.**

| Signal | Tooling | Where to find it |
|--------|---------|------------------|
| **Traces** | OpenTelemetry → Jaeger/Tempo | http://localhost:16686 |
| **Metrics** | Prometheus (scrapes `/metrics` on each service) | http://localhost:9090 → dashboards in Grafana http://localhost:3000 |
| **Logs** | Structured JSON to stdout | `docker compose logs -f <service>`; shipped to Loki/CloudWatch in deployed envs |

Each service is instrumented with OpenTelemetry; trace context propagates across API →
embedding service → database so a single search can be followed end to end.

---

## 12. Terraform Deployment

**Location:** [terraform/](terraform/). ⚠️ **PLACEHOLDER — no Terraform is committed yet; the steps
below describe the intended AWS deployment (ECS/Fargate + RDS + ALB).**

Target architecture: containers on **ECS/Fargate** behind an **Application Load Balancer**, backed by
**RDS for PostgreSQL** (with the `pgvector` extension enabled), images in **ECR**, secrets in **AWS
Secrets Manager**, and logs/metrics in **CloudWatch**.

```bash
# 0. Authenticate to the target AWS account (SSO / named profile)
aws sso login --profile <PROFILE>          # ⚠️ PLACEHOLDER

# 1. Initialise Terraform (uses a remote S3 + DynamoDB backend)
cd terraform
terraform init

# 2. Select / create a workspace per environment
terraform workspace select staging || terraform workspace new staging

# 3. Review the plan
terraform plan -var-file=environments/staging.tfvars   # ⚠️ PLACEHOLDER

# 4. Apply
terraform apply -var-file=environments/staging.tfvars

# 5. Build & push images, then run DB migrations / initial pipeline
#    ⚠️ PLACEHOLDER: document ECR push + one-off Fargate task for migrations & first ingest
```

- **Never** put credentials in `.tfvars` committed to git — use AWS Secrets Manager / SSM Parameter
  Store and pass ARNs. State should live in an encrypted remote backend, not locally.
- Deployment targets the organisation's own AWS account only. Do not deploy to public/consumer
  hosting. Coordinate with the AI Engineering team before exposing the platform internally.

---

## 13. Running Tests

⚠️ **PLACEHOLDER — no test suites are committed yet; commands below are the intended layout.**

### Unit tests

```bash
# Per service (pytest via uv)
cd api        && uv run pytest
cd pipeline   && uv run pytest
cd mcp-server && uv run pytest
```

### Integration tests

```bash
# Spins up the Compose stack (DB + embeddings) and exercises real endpoints
docker compose -f docker-compose.yml -f docker-compose.test.yml up --abort-on-container-exit
```

### Load tests

```bash
# e.g. with k6 or Locust against a running API   ⚠️ PLACEHOLDER: pick and commit a tool
k6 run tests/load/search.js
```

> ⚠️ **PLACEHOLDER:** Add coverage thresholds, CI wiring (`.github/workflows/`), and expected
> pass criteria once tests exist.

---

## 14. Known Limitations & Future Improvements

**Current limitations**

- 🚧 The platform is **scaffolding only** — the API, database schema, embedding service, monitoring,
  and Terraform are not yet implemented. Most sections above are intended designs, not shipped behaviour.
- No dataset, migrations, or seed data are committed.
- No CI/CD pipeline is defined ([.github/workflows/](.github/workflows/) is empty).

**Future improvements**

- Implement and commit `docker-compose.yml`, `.env.example`, and each service.
- Add a hybrid search mode (BM25/keyword + vector) with re-ranking for better relevance.
- Evaluate a larger embedding model (e.g. `bge-large-en-v1.5`) and measure retrieval quality.
- Add pagination, filtering, and caching to the search API.
- Add ANN indexing (pgvector HNSW) and benchmark recall vs. latency at scale.
- Wire up CI (lint, type-check, tests) and CD (build → ECR → Terraform apply).
- Add authn/authz roles (read vs. admin) and rate limiting.

---

> Maintainers: ⚠️ **PLACEHOLDER** · License: ⚠️ **PLACEHOLDER**
