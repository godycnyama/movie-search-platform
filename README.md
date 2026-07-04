# 🎬 Intelligent Movie Search Platform

An end-to-end semantic movie-search platform built on the **Vega movies dataset**: a Python **data
pipeline** that cleans, imputes, augments and embeds the catalogue; a **PostgreSQL 16 + pgvector**
store; a **Python FastMCP server** exposing semantic-search tools; and a secure, observable
**.NET 10 Web API** for end users — all orchestrated locally with **Docker Compose** and deployable
to **AWS via Terraform**.

> **Project status — core implemented, deployment pending.**
> ✅ **Done:** [docker-compose.yml](docker-compose.yml) (11 services), the full data pipeline
> ([pipeline/](pipeline/)), Alembic migrations ([database/migrations/](database/migrations/)), the
> FastMCP server ([mcp-server/](mcp-server/)), the .NET 10 API
> ([api/](api/) — endpoints served through the MCP server, JWT auth, Redis caching, OpenTelemetry),
> and the monitoring configs ([monitoring/](monitoring/)).
> ✅ Also done: **Terraform** (AWS ECS Fargate — [terraform/](terraform/)), **CI/CD**
> ([.github/workflows/](.github/workflows/)), **pytest suites** for the pipeline and MCP server,
> the **k6 load test** ([scripts/load_test.js](scripts/load_test.js)), and Prometheus metrics on
> both services. This README follows the structure required by the technical assessment brief.

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
                        │      (web UI, curl, LLM agents via MCP Inspector)        │
                        └───────────────┬───────────────────────┬─────────────────┘
                                        │ HTTPS/REST (JWT)       │ MCP (SSE)
                                        ▼                        ▼
                        ┌───────────────────────┐    ┌───────────────────────────┐
                        │      .NET 10 API       │    │        MCP Server          │
                        │      (api/, :8080)     │───▶│    (mcp-server/, :8000)    │
                        │  JWT auth · OpenAPI    │MCP │   FastMCP semantic tools   │
                        │  Redis cache · OTel    │SSE │   asyncpg pool · OTel      │
                        └─────┬─────────────────┘    └─────┬──────────────┬──────┘
                              │ EF Core                    │ asyncpg      │ query
                              │ (users/auth only)          │ + pgvector   │ embeddings
                              ▼                            ▼              ▼
        ┌───────────────────────────────────────────┐    ┌───────────────────────────┐
        │     PostgreSQL 16 + pgvector (:5432)       │    │     Ollama (:11434)        │
        │  movies: metadata + augmented_text +       │    │  nomic-embed-text 768-dim  │
        │  vector(768) · HNSW cosine index · users   │    │  (pulled on first start)   │
        └────────────────────▲──────────────────────┘    └────────────▲──────────────┘
                             │ alembic upgrade head + upsert           │ /api/embed
                    ┌────────┴─────────────────────────────────────────┴────────┐
                    │                      Data Pipeline                          │
                    │   (pipeline/, one-shot) Vega dataset → clean → impute →     │
                    │           augment → embed → load (idempotent)               │
                    └──────────────────────────────────────────────────────────────┘

  Cache:         Redis 7 (:6379) — API query-result cache (LRU, password-protected)
  Observability: OpenTelemetry → Jaeger (:16686) · Prometheus (:9090) · Grafana (:3000)
  Deployment:    Terraform (terraform/) → AWS ECS Fargate   ✅ see §12
  Bonus:         Embedding Atlas (:7000, compose profile "bonus")
```

Note: the API serves **all movie reads through the MCP server** (official `ModelContextProtocol`
C# SDK over SSE, one tool call per endpoint — see
[McpMovieCatalogService](api/MovieSearch/src/Infrastructure/Services/McpMovieCatalogService.cs));
query embedding and pgvector search happen inside the MCP server. The API touches Postgres
directly only for its own `users` table (auth). The same MCP tools remain consumable by any other
MCP client (LLM agents, MCP Inspector).

### Repository layout

```
movie-search-platform/
├── README.md                 # This file
├── docker-compose.yml        # Local orchestration (11 services)             ✅ committed
├── .env.example              # Environment template                          ✅ committed
├── pipeline/                 # Part 1 — data pipeline (Python + uv)          ✅ implemented
│   ├── src/pipeline/         #   cleaning · imputation · augmentation · embedding · loader
│   ├── src/models.py         #   shared SQLModel entity (Movie)
│   ├── alembic.ini           #   migration config (versions live in database/migrations)
│   └── Dockerfile            #   runs `alembic upgrade head` then the pipeline
├── database/migrations/      # Part 2 — Alembic migrations                   ✅ implemented
│   └── versions/…initial_schema.py   # movies + users tables, pgvector, HNSW index
├── mcp-server/               # Part 3 — FastMCP server (Python + uv)         ✅ implemented
│   ├── src/server/           #   tools · db · embeddings · asgi · logging
│   └── Dockerfile
├── api/MovieSearch/          # Part 4 — .NET 10 Web API (MCP client)         ✅ implemented
│   ├── src/{Domain,Application,Infrastructure,Api}/   # layered solution (.slnx)
│   └── src/Api/Dockerfile
├── monitoring/               # prometheus.yml · Grafana provisioning + dashboard  ✅ committed
├── scripts/                  # Atlas export + k6 load test                   ✅ implemented
├── terraform/                # Part 6 — AWS ECS Fargate IaC (see §12)        ✅ implemented
└── .github/workflows/        # ci.yml, cd.yml (see §12-13)                   ✅ implemented
```

---

## 2. Prerequisites

Exact versions the platform targets:

| Tool | Required version | Notes |
|------|------------------|-------|
| Docker Engine | **≥ 24.0** | Everything runs via Compose; this is the only hard requirement to run the platform |
| Docker Compose | **v2 (≥ 2.20)** | `docker compose`, not legacy `docker-compose` |
| Python | **3.13** (pinned in each service's `pyproject.toml`) | Only needed to run [pipeline/](pipeline/) or [mcp-server/](mcp-server/) outside Docker |
| [uv](https://github.com/astral-sh/uv) | **≥ 0.5** | Python package & venv manager for both Python services (`uv.lock` committed) |
| .NET SDK | **10.0** | Only needed to build/run [api/](api/) outside Docker |
| PostgreSQL | **16.x** + `pgvector` **≥ 0.7** | Provided by the `pgvector/pgvector:pg16` image |
| Terraform | **≥ 1.9** | For AWS deployment ([terraform/](terraform/), §12) |
| AWS CLI | **v2** | Authenticated to the target account (SSO or named profile) |
| k6 | latest | Only for the load test ([scripts/load_test.js](scripts/load_test.js), §13) |

> All tools should be installed from official sources, and new project dependencies should go through
> the organisation's approved dependency-vetting process before use.

---

## 3. Quick Start

From a fresh clone to a running platform:

```bash
# 1. Clone
git clone <REPO_URL> movie-search-platform && cd movie-search-platform

# 2. Create your local environment file (git-ignored — never commit it)
cp .env.example .env            # then edit the REQUIRED secrets (see comments in the file)

# 3. Build & start the whole platform (db, redis, ollama, mcp-server, api, observability)
docker compose up --build -d

# 4. The pipeline runs automatically as a one-shot service (migrations + load).
#    To re-run it manually:
docker compose run --rm pipeline

# 5. Verify the API is healthy
curl http://localhost:8080/health
```

On first start the `ollama-pull-models` one-shot pulls the `nomic-embed-text` model before the
pipeline and MCP server come up (cold start ~1–2 min depending on bandwidth).

> **Credentials:** all secrets (DB password, Redis password, JWT signing key, Grafana admin
> password) are read from the git-ignored `.env` file — never hardcoded in code or committed config.
> The compose file uses `${VAR:?…}` so it fails fast if a required secret is missing. If this
> platform is exposed to a wider internal audience, contact the AI Engineering team before deploying.

---

## 4. Service Endpoints

Local URLs when running via Docker Compose (ports per [docker-compose.yml](docker-compose.yml)):

| Service | URL | Purpose |
|---------|-----|---------|
| .NET 10 API | http://localhost:8080 | Public-facing search API |
| API — OpenAPI spec | http://localhost:8080/openapi/v1.json | OpenAPI spec (Development environment only) |
| API — health | http://localhost:8080/health | Liveness/readiness probe (reports per-dependency status) |
| API — metrics | http://localhost:8080/metrics | Prometheus metrics (OTel exporter) |
| MCP Server | http://localhost:8000/sse | FastMCP endpoint (SSE) for MCP clients |
| MCP — health | http://localhost:8000/health | MCP health check |
| Embedding Service (TEI) | http://localhost:8001 | HuggingFace TEI serving nomic-embed-text-v1.5 (alternative backend) |
| Ollama | http://localhost:11434 | Primary embedding backend (`nomic-embed-text`, 768-dim) |
| PostgreSQL + pgvector | postgresql://localhost:5432 | Primary datastore (movies + users) |
| Redis | redis://localhost:6379 | API query-result cache (password from `.env`) |
| Prometheus | http://localhost:9090 | Metrics collection |
| Grafana | http://localhost:3000 | Dashboards (admin login from `.env`) |
| Jaeger UI | http://localhost:16686 | Distributed traces (OTLP in on 4317/4318) |
| Embedding Atlas (bonus) | http://localhost:7000 | Embedding visualization — ⚠️ not implemented |

---

## 5. Data Pipeline

**Location:** [pipeline/](pipeline/) — entrypoint [pipeline/src/main.py](pipeline/src/main.py).
Runs as a one-shot Compose service: the container first applies schema migrations
(`alembic upgrade head`, versions mounted from [database/migrations/](database/migrations/)), then
executes the pipeline and exits. ✅ **Implemented.**

### How it works

The pipeline ingests the **Vega movies dataset** (`from vega_datasets import data; data.movies()`)
and prepares it for vector search through modular stages:

1. **Clean** ([cleaning.py](pipeline/src/pipeline/cleaning.py)) — rename raw columns onto the shared
   schema; trim strings (empty → NA); drop rows with no title; parse release dates **including the
   dataset's two-digit-year quirk** (e.g. `'46` parsing as 2046 gets 100 years subtracted); coerce
   numerics to nullable types; null out impossible values (negative money/votes, IMDB ∉ [0,10],
   RT ∉ [0,100], runtime < 1); de-duplicate on `(title, release_date)`. Emits a structured report.
2. **Impute** ([imputation.py](pipeline/src/pipeline/imputation.py)) — fill or `NULL` missing values
   per the policy in [Data Decisions](#6-data-decisions), with per-field counts in the report.
3. **Augment** ([augmentation.py](pipeline/src/pipeline/augmentation.py)) — build the embedding text
   (see [Embedding Strategy](#7-embedding-strategy)) and derive **`budget_tier`**, **`decade`** and
   **`blockbuster_flag`**.
4. **Embed** ([embedding.py](pipeline/src/pipeline/embedding.py)) — call **Ollama**
   (`nomic-embed-text`, 768-dim) over the network in configurable batches.
5. **Load** ([loader.py](pipeline/src/pipeline/loader.py)) — **idempotent upsert** into pgvector:
   movie IDs are deterministic (derived from title + release date), so re-running updates in place
   and never creates duplicates.

The combined cleaning + imputation report is written to **stdout and
`pipeline/logs/cleaning_report.json`** (the `logs/` directory is volume-mounted to the host).

### How to re-run

```bash
# Full run (idempotent — re-running does not create duplicates)
docker compose run --rm pipeline

# Locally with uv (requires postgres + ollama reachable via env vars)
cd pipeline && uv sync && uv run alembic upgrade head && uv run python src/main.py
```

Configuration is typed settings bound from environment variables — see
[pipeline/src/pipeline/settings.py](pipeline/src/pipeline/settings.py) and
[.env.example](.env.example): `DATABASE_URL`, `OLLAMA_URL`, `EMBEDDING_MODEL`, `EMBEDDING_DIM`,
`BATCH_SIZE` (`PIPELINE_BATCH_SIZE` in compose), `PIPELINE_VERSION`, `LOG_LEVEL`.

### How to verify

```bash
# Row + embedding coverage
docker compose exec postgres psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" \
  -c "SELECT count(*) AS movies, count(embedding) AS embedded FROM movies;"

# Inspect the cleaning report
cat pipeline/logs/cleaning_report.json

# Semantic search via the MCP server (the API's movie endpoints ride this same path)
npx @modelcontextprotocol/inspector   # → SSE, http://localhost:8000/sse, call search_movies_by_description
```

Expected: `embedded == movies` (every row has a vector), and MCP search returns ranked results with
similarity scores.

---

## 6. Data Decisions

Cleaning and imputation strategy across the Vega fields, chosen to **preserve data quality for
semantic search** — the guiding principle is to never fabricate values that feed the embedding text
or that users filter/sort on. Prefer an honest `NULL` (or a real "Unknown"/"Not Rated" category)
over invented data. ✅ Implemented as described in
[cleaning.py](pipeline/src/pipeline/cleaning.py) / [imputation.py](pipeline/src/pipeline/imputation.py).

| Field | Strategy | Why |
|-------|----------|-----|
| Duplicates | De-duplicate on `(title, release_date)` | Prevents double-counting in ranked search results |
| `Title` | Trim/normalise; drop rows with no title | A movie with no title is neither identifiable nor searchable |
| `Release Date` | Parse leniently; fix wrapped two-digit years (> 2010 ⇒ −100y); leave `NULL` if unparseable | Enables `decade` derivation and time filters without guessing |
| `IMDB Rating` | Leave **`NULL`** (no imputation) | The `min_imdb_rating` filter and ranking depend on truthful values; median imputation would inflate low-information films |
| `Rotten Tomatoes Rating` | Leave **`NULL`** | Same as IMDB — critical-score queries must not see fabricated scores |
| `Production Budget` | Leave **`NULL`**; derive `budget_tier` only when known | Budgets span orders of magnitude; imputing distorts `budget_tier` and budget-based queries |
| `Running Time min` | Median imputation within `Major Genre` (global-median fallback), flagged via `runtime_imputed` | Runtime is low-signal and roughly regular within a genre; the flag lets consumers exclude imputed values |
| `MPAA Rating` | Impute the real category **`"Not Rated"`** | "Not Rated" is semantically accurate for missing/unrated titles — better than mode |
| `Director` | Impute **`"Unknown"`** | Preserves the row; avoids wrongly attributing a film to a real director |
| `Distributor` | Impute **`"Unknown"`** | Same rationale as Director |
| `Major Genre`, `Creative Type`, `Source` | Impute **`"Unknown"`** | Keeps faceting/filtering stable without inventing categories |
| Numeric ranges | Coerce out-of-range values to `NULL` (negative budgets/gross/votes; runtime < 1; IMDB ∉ [0,10]; RT ∉ [0,100]) | Removes impossible values before they poison filters and derived features |

**Derived features (documented rationale):**
- **`budget_tier`** — bucketises `Production Budget` (low/mid/high/blockbuster) so budget-relative
  queries ("small budget", "blockbuster") work without exact figures; `NULL` when budget unknown.
- **`decade`** — integer decade from the release year (e.g. 1990) powering the `decade` filter and
  "from the 90s"-style queries.
- **`blockbuster_flag`** — boolean: high production budget **and** high worldwide gross.

**Schema** ([database/migrations/](database/migrations/), Alembic, autogenerated from the pipeline's
SQLModel metadata): `movies` (metadata + `augmented_text` + `vector(768)` + audit columns +
`pipeline_version`) with an **HNSW cosine index** (`m=16, ef_construction=64`), the `vector`
extension, and the API's `users` table.

---

## 7. Embedding Strategy

### Model choice & rationale

- **Model:** **`nomic-embed-text`** (nomic-embed-text-v1.5), **768 dimensions**, served by
  **Ollama** in its own container — no in-process model download and no paid/hosted API. A one-shot
  `ollama-pull-models` compose service pre-pulls the model before dependents start.
- **Why:** strong retrieval quality for its size, fully open and locally runnable, and long context
  that comfortably fits the augmented movie text. It keeps all data in-house.
- **Dimensionality:** **768**. The pgvector column is declared `vector(768)` and `EMBEDDING_DIM=768`
  in [.env.example](.env.example) — the column and model **must** agree or loads fail.
- The compose file also carries a **HuggingFace Text Embeddings Inference (TEI)** service on `:8001`
  serving the same model as an alternative HTTP backend. The API itself never embeds — its search
  queries are embedded by the MCP server.

### How the embedding containers are wired into Docker Compose

```yaml
ollama:                    # primary backend — pipeline & MCP server embed via http://ollama:11434
  image: ollama/ollama:latest
  ports: ["11434:11434"]
  volumes: [ollama_data:/root/.ollama]

ollama-pull-models:        # one-shot: `ollama pull nomic-embed-text` before dependents start
  image: ollama/ollama:latest
  depends_on: { ollama: { condition: service_healthy } }

embeddings:                # alternative backend (TEI) serving the same model on :8001
  image: ghcr.io/huggingface/text-embeddings-inference:cpu-1.5
  command: ["--model-id", "nomic-ai/nomic-embed-text-v1.5", "--port", "8001"]
```

Both the **pipeline** (document embedding) and the **MCP server** (query embedding) point at
`http://ollama:11434` via `OLLAMA_URL` with the same `EMBEDDING_MODEL`, so document and query
vectors always come from the identical model.

### How the embedding text is constructed

Each movie is serialised into a natural-language block by
[augmentation.py](pipeline/src/pipeline/augmentation.py) and stored in the `augmented_text` column
for transparency and re-embedding. **Only facts we actually have are mentioned** — imputed
"Unknown"/"Not Rated" categories, imputed runtimes and `NULL` numerics are omitted so the vector is
not polluted with placeholder tokens:

```
Title: {title}. Genre: {genre}. Creative type: {creative_type}. Source: {source}.
Directed by {director}. Distributed by {distributor}. Released in {year} ({decade}s).
MPAA rating: {mpaa_rating}. Running time: {runtime} minutes.
IMDB rating: {imdb_rating}/10 from {imdb_votes} votes. Rotten Tomatoes score: {rt_rating}/100.
Budget tier: {budget_tier}. A blockbuster with high budget and high worldwide gross.
```

---

## 8. MCP Server

**Location:** [mcp-server/](mcp-server/) — a **FastMCP** server exposing movie search as MCP tools
consumable by any MCP-compatible client. ✅ **Implemented:** transport is **SSE** by default
(`MCP_TRANSPORT=sse`; `streamable-http` supported for production, `stdio` via
`python -m server.main`), with an **asyncpg** connection pool to pgvector
([db.py](mcp-server/src/server/db.py)), Ollama query embeddings
([embeddings.py](mcp-server/src/server/embeddings.py)), Pydantic models, JSON structured logging
with per-request IDs and tool timings, and a `GET /health` endpoint. In Docker it runs under
uvicorn via the ASGI factory ([asgi.py](mcp-server/src/server/asgi.py)).

### Available tools

All five tools from the spec — plus `get_movie_by_id`, which backs the .NET API's by-id endpoint —
are registered in [tools.py](mcp-server/src/server/tools.py); FastMCP derives input schemas from
the type-annotated signatures. `top_k` is clamped to [1, 50]. The .NET API is itself an MCP client
of this server: every `/api/v1/movies/*` and `/api/v1/stats` read maps to one of these tools.

| Tool | Description | Arguments |
|------|-------------|-----------|
| `search_movies_by_description` | Semantic vector search with optional metadata filters; returns ranked results with similarity scores | `query: str`, `top_k: int = 10`, `genre_filter: str \| None`, `min_imdb_rating: float \| None`, `mpaa_rating: str \| None`, `decade: int \| None` |
| `get_movie_by_id` | Retrieve a movie by its stable UUID (null when unknown) | `movie_id: str` |
| `get_movie_by_title` | Retrieve a movie by exact or fuzzy title match | `title: str` |
| `get_similar_movies` | Most semantically similar movies to a given movie (UUID validated; unknown IDs raise a clear error) | `movie_id: str`, `top_k: int = 5` |
| `list_genres` | All distinct genres in the dataset | — |
| `get_dataset_stats` | Summary statistics (totals, embedding coverage, year range, avg IMDB rating, pipeline version) | — |

### How to test the tools directly

```bash
# Interactive: point the MCP Inspector at the SSE endpoint (no code required)
npx @modelcontextprotocol/inspector
#   → Transport: SSE, URL: http://localhost:8000/sse

# Health check
curl http://localhost:8000/health
```

Example natural-language queries the system handles: *"action movies from the 90s with high IMDB
ratings"*, *"critically acclaimed drama films with small budgets"*, *"animated family movies
distributed by Disney"*, *"sci-fi films directed by James Cameron"*, *"dark psychological thrillers
with low Rotten Tomatoes scores"*.

---

## 9. API Documentation

**Location:** [api/MovieSearch/](api/MovieSearch/) — a layered .NET 10 solution
(`Domain` / `Application` / `Infrastructure` / `Api`) using **Carter** endpoint slices, **Wolverine**
as the in-process CQRS mediator, URL-segment **API versioning**, the official
**ModelContextProtocol** C# SDK as the movie data client (all movie/stats reads are MCP tool calls
against the MCP server — the API never queries the movies tables), EF Core for the API-owned
`users` table, and **Redis**-backed response caching in the query handlers.

**Base URL:** `http://localhost:8080` · OpenAPI spec at `/openapi/v1.json` (Development only).
All `/api/v1/movies/*` and `/api/v1/stats` endpoints require a valid JWT (see
[Authentication](#10-authentication)).

### `GET /health` — liveness + readiness (no auth)

```bash
curl http://localhost:8080/health
```
```json
{ "status": "Healthy", "dependencies": { "postgres": "Healthy", "mcp-server": "Healthy" } }
```

### `GET /api/v1/movies/search` — natural-language search

Query params: `q` (required), `top_k` (default 10, max 50), `genre`, `min_imdb_rating`,
`mpaa_rating`, `decade`. Results are cached in Redis per query+filters.

```bash
curl "http://localhost:8080/api/v1/movies/search?q=sci-fi%20films%20directed%20by%20James%20Cameron&top_k=3" \
  -H "Authorization: Bearer $TOKEN"
```
```json
{
  "query": "sci-fi films directed by James Cameron",
  "count": 3,
  "results": [
    { "id": "9f1c…", "title": "Terminator 2: Judgment Day", "release_year": 1991,
      "major_genre": "Action", "director": "James Cameron", "imdb_rating": 8.5,
      "similarity_score": 0.86 }
  ]
}
```

### `GET /api/v1/movies/{id}` — get movie by ID

```bash
curl http://localhost:8080/api/v1/movies/<GUID> -H "Authorization: Bearer $TOKEN"
```

Returns the full movie record (metadata, ratings, `budget_tier`, `decade`, …) or `404`.

### `GET /api/v1/movies/by-title` — get movie by title

Exact (case-insensitive) title match first, then fuzzy substring match — same semantics as the
MCP `get_movie_by_title` tool it calls. Returns the full movie record or `404`.

```bash
curl "http://localhost:8080/api/v1/movies/by-title?title=terminator" -H "Authorization: Bearer $TOKEN"
```

### `GET /api/v1/movies/{id}/similar` — similar movies

```bash
curl "http://localhost:8080/api/v1/movies/<GUID>/similar?top_k=5" -H "Authorization: Bearer $TOKEN"
```

### `GET /api/v1/movies/genres` — list genres

```bash
curl http://localhost:8080/api/v1/movies/genres -H "Authorization: Bearer $TOKEN"
```
```json
{ "genres": ["Action", "Adventure", "Comedy", "Drama", "Horror", "Musical", "Thriller", "Western"] }
```

### `GET /api/v1/stats` — dataset statistics (**admin** role)

```bash
curl http://localhost:8080/api/v1/stats -H "Authorization: Bearer $ADMIN_TOKEN"
```
```json
{ "total_movies": 3201, "with_embeddings": 3201, "genres": 12,
  "year_range": [1915, 2010], "avg_imdb_rating": 6.28, "pipeline_version": "0.1.0" }
```

All endpoints validate inputs (data-annotation validation → RFC 7807 `ValidationProblem`) and map
domain errors to problem-details responses via a shared `Result<T>` type.

---

## 10. Authentication

The API uses **JWT Bearer token** authentication with email/password accounts stored in Postgres
(`users` table, **PBKDF2** password hashing). Two roles: **`reader`** (all movie endpoints) and
**`admin`** (additionally `/api/v1/stats`). Sign-up always creates a `reader`; promoting to `admin`
is a manual DB operation.

### Endpoints (anonymous)

```bash
# Create an account — returns a bearer token immediately
curl -X POST http://localhost:8080/api/v1/auth/signup \
  -H "Content-Type: application/json" \
  -d '{"email":"you@example.com","password":"<strong password>"}'

# Log in
curl -X POST http://localhost:8080/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"you@example.com","password":"<strong password>"}'
```
```json
{ "access_token": "eyJhbGciOi…", "token_type": "Bearer", "expires_in": 3600, "role": "reader" }
```

There is also `POST /api/v1/auth/change-password` (authenticated).

### Use the token

```bash
TOKEN="eyJhbGciOi…"
curl "http://localhost:8080/api/v1/movies/genres" -H "Authorization: Bearer $TOKEN"
```

- The JWT signing key (`JWT_SIGNING_KEY`), issuer and audience are read from `.env` — never
  hardcoded or committed. In AWS these would come from Secrets Manager.
- Login returns the same error for unknown email and wrong password, so account existence is not
  leaked. Tokens expire (`expires_in`); log in again on `401 Unauthorized`.
- Calling an admin-only endpoint with a `reader` token returns `403 Forbidden`.

---

## 11. Observability

**Location:** [monitoring/](monitoring/) — ✅ Prometheus scrape config, Grafana datasource +
dashboard provisioning, and the `movie-search.json` dashboard are committed.

| Signal | Tooling | Where to find it |
|--------|---------|------------------|
| **Traces** | OpenTelemetry (ASP.NET Core, HttpClient, Npgsql/pgvector, Wolverine handler spans; OTLP in the MCP server via env) → **Jaeger** | http://localhost:16686 |
| **Metrics** | OpenTelemetry Prometheus exporter on the API's `/metrics`, scraped by Prometheus | http://localhost:9090; dashboards in **Grafana** http://localhost:3000 |
| **Logs** | Structured console logging (.NET `Microsoft.Extensions.Logging`; Python JSON logs with request IDs + tool timings) | `docker compose logs -f <service>` |

Health checks: the API's `/health` reports overall + per-dependency status (Postgres via a
DbContext check, the MCP server via an MCP ping over the shared session) and backs the compose
healthcheck; the MCP server exposes its own `/health`.

Rate limiting (60 req/min per authenticated user, fixed window on the JWT `sub` claim) is enforced
by the API's rate limiter middleware; the p95 < 500ms SLO is validated by the k6 load test
([scripts/load_test.js](scripts/load_test.js), §13). Both Python services expose Prometheus
metrics: the API via the OTel exporter, the MCP server via `/metrics` (per-tool call counts and
durations).

---

## 12. Terraform Deployment

**Location:** [terraform/](terraform/) — ✅ **implemented** (see the detailed
[terraform/README.md](terraform/README.md)). Target: **AWS ECS Fargate** — the **.NET API** and
**MCP server** each autoscale between **1 and 2 tasks** (CPU target tracking), with an internal
**Ollama** service (EFS model cache) for query embeddings and the **pipeline** as a one-off ECS
task. Backed by **RDS PostgreSQL 16 (pgvector)** and **ElastiCache Redis** in private subnets,
images in **ECR** (scan-on-push), a public **ALB** (HTTP now; supply `acm_certificate_arn` to turn
on HTTPS + redirect), and CloudWatch alarms → SNS.

```
terraform/
├── main.tf · variables.tf · outputs.tf · versions.tf    # platform composition module
├── modules/{networking,ecr,secrets,rds,elasticache,alb,ecs,iam,monitoring}/
├── environments/{dev,prod}/                             # dev: single-AZ; prod: Multi-AZ + protection
└── bootstrap/                                           # one-time S3 state bucket + DynamoDB locks
```

Infrastructure guarantees: all secrets **generated in-stack** and stored in Secrets Manager
(injected into tasks via `valueFrom` — no credentials in tfvars, source, or plain env vars);
compute uses IAM roles (GitHub deploys via **OIDC**, no access keys); RDS/Redis in private subnets
only; CPU-based auto-scaling; **VPC Flow Logs** enabled; **remote state in S3 with DynamoDB
locking** (`terraform/bootstrap`); every resource tagged `Environment`, `Project`, `ManagedBy` via
provider `default_tags`.

- Never commit real `*.tfvars` or `backend.hcl` (both git-ignored; `.example` templates provided).
- Deployment targets the organisation's own AWS account only — do not use public/consumer hosting.
  Coordinate with the AI Engineering team before exposing the platform internally, and have
  Security review the deploy role's IAM policy before first use.

---

## 13. Running Tests

Three suites, all run by CI ([.github/workflows/ci.yml](.github/workflows/ci.yml)) on every PR
and push to master:

- **pipeline** (pytest) — cleaning (date quirks, impossible-value nulling, dedup), imputation
  (genre-median runtimes, real categories), augmentation (budget tiers, embedding text),
  deterministic loader ids, and the Ollama client (retries, dimension guard) via mock transports.
- **mcp-server** (pytest) — all six tools exercised end to end through FastMCP's **in-memory MCP
  client** (schemas, structured output, and error mapping included) against fake db/embeddings
  backends, plus the `/health` and `/metrics` HTTP routes.
- **api** (xUnit, `tests/Tests`) — MCP tool-result decoding and the password hasher.

### Unit tests

```bash
cd pipeline   && uv sync && uv run pytest     # Python — pipeline stages + embeddings client
cd mcp-server && uv sync && uv run pytest     # Python — MCP tools, models, HTTP endpoints
cd api/MovieSearch && dotnet test             # .NET — xUnit
```

### Load test (p95 < 500ms SLO)

```bash
# Signs up its own throwaway users, then mixes search / by-id / similar / genres.
# Thresholds fail the run if p95 >= 500ms or the error rate exceeds 1%.
k6 run scripts/load_test.js -e BASE_URL=http://localhost:8080
```

**Linting:** `ruff check .` in both Python projects (enforced by CI). CD
([.github/workflows/cd.yml](.github/workflows/cd.yml)) builds and pushes the images to ECR via
OIDC, auto-deploys **dev** with `terraform apply`, and promotes the same images to **prod** behind
a manual approval on the `production` GitHub environment.

---

## 14. Known Limitations & Future Improvements

**Current limitations**

- **The MCP server is a hard dependency of the API's movie endpoints** — all movie/stats reads are
  MCP tool calls, so if the MCP server is down those endpoints fail (auth and cached responses
  still work). The API `/health` surfaces this as the `mcp-server` dependency.
- The API↔MCP hop adds a network round-trip per uncached read (Redis response caching in the
  handlers absorbs repeat queries); run the k6 load test (§13) against a loaded stack to validate
  the p95 < 500ms SLO on your hardware.
- The AWS stacks have not been applied to a real account yet — `terraform validate` passes and CI
  enforces fmt/validate, but the first apply (see [terraform/README.md](terraform/README.md))
  should be shepherded, and the deploy role's IAM policy reviewed by Security first.
- No exported [openapi.json](openapi.json); the OpenAPI spec is served only
  in the Development environment.
- Two embedding backends (Ollama + TEI) are both in compose; only Ollama is used (pipeline + MCP
  server) — the TEI service could be dropped. The AWS stack deploys Ollama only.
- Ollama pulls model weights on first start (cold start ~1–2 min locally; on AWS the EFS cache
  makes it a one-time cost); healthchecks account for this.

**Future improvements**

- Integration tests that exercise the API → MCP server → pgvector path end to end
  (unit tests currently cover each side against fakes).
- Cache-TTL tuning.
- Hybrid search (vector similarity + full-text/metadata filters) with re-ranking.
- Tune the pgvector **HNSW** index (`m`, `ef_search`) and benchmark recall vs. latency at scale.
- HTTPS on the ALB (provide `acm_certificate_arn` once a domain/cert exists) and a Route53 alias.
- Ship the local Grafana dashboard to the AWS environments (Amazon Managed Grafana or
  container-based) — CloudWatch alarms cover the basics today.

---

> Maintainers: ⚠️ **PLACEHOLDER** · License: ⚠️ **PLACEHOLDER**
