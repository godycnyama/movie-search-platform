# 🎬 Intelligent Movie Search Platform

An end-to-end semantic movie-search platform built on the **Vega movies dataset**: a Python **data
pipeline** that cleans, imputes, augments and embeds the catalogue; a **PostgreSQL 16 + pgvector**
store; a **Python FastMCP server** exposing semantic-search tools; and a secure, observable
**.NET 10 Web API** for end users — all orchestrated locally with **Docker Compose** and deployable
to **AWS via Terraform**.

> **Project status — core implemented, deployment pending.**
> ✅ **Done:** [docker-compose.yml](docker-compose.yml) (11 services), the full data pipeline
> ([pipeline/](pipeline/)), Alembic migrations ([database/migrations/](database/migrations/)), the
> FastMCP server with all five tools ([mcp-server/](mcp-server/)), most of the .NET 10 API
> ([api/](api/) — endpoints, JWT auth, Redis caching, pgvector search, OpenTelemetry), and the
> monitoring configs ([monitoring/](monitoring/)).
> 🚧 **Outstanding:** the API's embedding client is a stub (semantic search via the API returns an
> error until it is wired to the embedding service — the MCP server path works end to end), test
> suites are template stubs, and [terraform/](terraform/), [scripts/](scripts/) and
> [.github/workflows/](.github/workflows/) are empty. Sections below marked ⚠️ describe intended
> design that has not landed yet. This README follows the structure required by the technical
> assessment brief.

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
                        │      (api/, :8080)     │    │    (mcp-server/, :8000)    │
                        │  JWT auth · OpenAPI    │    │   FastMCP semantic tools   │
                        │  Redis cache · OTel    │    │   asyncpg pool · OTel      │
                        └─────┬─────────┬───────┘    └─────┬──────────────┬──────┘
                              │ EF Core │ query embeddings │ asyncpg      │ query
                              │ +pgvector│ (stub — see §14) │              │ embeddings
                              ▼         ▼                  ▼              ▼
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
  Deployment:    Terraform (terraform/) → AWS   ⚠️ not started
  Bonus:         Embedding Atlas (:7000, compose profile "bonus")   ⚠️ not started
```

Note: the API currently performs vector search **directly against pgvector** (EF Core +
`Pgvector`), with the MCP server serving MCP clients such as LLM agents. Compose passes
`MCP__ServerUrl` to the API, but an API→MCP client integration is not implemented.

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
├── api/MovieSearch/          # Part 4 — .NET 10 Web API                      ✅ implemented*
│   ├── src/{Domain,Application,Infrastructure,Api}/   # layered solution (.slnx)
│   └── src/Api/Dockerfile    #   *embedding client is a stub — see §14
├── monitoring/               # prometheus.yml · Grafana provisioning + dashboard  ✅ committed
├── scripts/                  # Atlas export + load tests                     ⚠️ empty
├── terraform/                # Part 6 — AWS IaC                              ⚠️ empty
└── .github/workflows/        # ci.yml, cd.yml                                ⚠️ empty
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
| Terraform | **≥ 1.7** | For AWS deployment (⚠️ IaC not yet written) |
| AWS CLI | **v2** | Authenticated to the target account (SSO or named profile) |

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

> ⚠️ **Known build caveats:**
> - The `api` service builds with `context: ./api`, but the Dockerfile lives at
>   [api/MovieSearch/src/Api/Dockerfile](api/MovieSearch/src/Api/Dockerfile) — the compose build
>   context/dockerfile paths need aligning before `docker compose build api` succeeds.
> - The bonus `atlas` service expects a `scripts/Dockerfile` that does not exist yet
>   ([scripts/](scripts/) is empty). It is behind the `bonus` profile, so the default
>   `docker compose up` is unaffected.

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

# Semantic search via the MCP server (works end to end today; the API path is pending — see §14)
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
  serving the same model as an alternative HTTP backend (currently what the API's
  `OllamaSettings__OllamaBaseUrl` points at).

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

All five tools from the spec are registered in [tools.py](mcp-server/src/server/tools.py); FastMCP
derives input schemas from the type-annotated signatures. `top_k` is clamped to [1, 50].

| Tool | Description | Arguments |
|------|-------------|-----------|
| `search_movies_by_description` | Semantic vector search with optional metadata filters; returns ranked results with similarity scores | `query: str`, `top_k: int = 10`, `genre_filter: str \| None`, `min_imdb_rating: float \| None`, `mpaa_rating: str \| None`, `decade: int \| None` |
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
as the in-process CQRS mediator, URL-segment **API versioning**, EF Core + **Pgvector** for
similarity search, and **Redis**-backed response caching in the query handlers.

**Base URL:** `http://localhost:8080` · OpenAPI spec at `/openapi/v1.json` (Development only).
All `/api/v1/movies/*` and `/api/v1/stats` endpoints require a valid JWT (see
[Authentication](#10-authentication)).

> ⚠️ `search` and `{id}/similar` depend on query embeddings; the API's `EmbeddingsService` is
> currently a stub that throws, so those two endpoints fail until it is wired to the embedding
> container (§14). The remaining endpoints work against the pipeline-loaded data.

### `GET /health` — liveness + readiness (no auth)

```bash
curl http://localhost:8080/health
```
```json
{ "status": "Healthy", "dependencies": { "postgres": "Healthy" } }
```

### `GET /api/v1/movies/search` — natural-language search *(⚠️ blocked on embedding client)*

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

### `GET /api/v1/movies/{id}/similar` — similar movies *(⚠️ blocked on embedding client)*

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
DbContext check) and backs the compose healthcheck; the MCP server exposes its own `/health`.

> ⚠️ Rate limiting (60 req/min per user) and the p95 < 500ms load-test validation described in the
> brief are not implemented yet — Redis response caching in the search/stats handlers is.

---

## 12. Terraform Deployment

**Location:** [terraform/](terraform/). ⚠️ **PLACEHOLDER — the directory is empty; no IaC has been
written yet.** Intended target: **AWS ECS Fargate** behind an **ALB (HTTPS/ACM)**, backed by
**RDS PostgreSQL (pgvector)** in private subnets, images in **ECR**, secrets in **Secrets Manager**,
logs/traces in **CloudWatch/X-Ray**.

```
terraform/                                   # intended layout
├── modules/{networking,compute,rds,ecr,alb,iam,monitoring,secrets}/
├── environments/{dev,prod}/
├── main.tf · variables.tf · outputs.tf · README.md
```

Infrastructure guarantees (intended): all secrets via Secrets Manager (no hardcoded credentials);
compute tasks use IAM roles (no access keys); RDS in private subnets only; ALB with HTTPS;
CPU/memory auto-scaling; VPC Flow Logs enabled; **remote state in S3 with DynamoDB locking**; every
resource tagged `Environment`, `Project`, `ManagedBy`.

- Never commit real `*.tfvars` with secrets — pass Secrets Manager ARNs instead.
- Deployment targets the organisation's own AWS account only — do not use public/consumer hosting.
  Coordinate with the AI Engineering team before exposing the platform internally.

---

## 13. Running Tests

⚠️ **PLACEHOLDER — no real tests yet.** The .NET solution contains two template test projects
(`tests/MovieSearch.Tests`, `tests/Tests`) with only generated stubs; the Python services have no
test suites. Intended commands once suites land:

### Unit tests

```bash
cd pipeline   && uv run pytest        # Python — cleaning/imputation/augmentation logic
cd mcp-server && uv run pytest        # Python — tool logic, query building
cd api/MovieSearch && dotnet test     # .NET — xUnit unit + integration tests
```

### Integration tests

```bash
# Bring up the stack and exercise real endpoints against seeded data
docker compose up -d --build
docker compose run --rm pipeline
cd api/MovieSearch && dotnet test --filter Category=Integration
```

### Load tests

```bash
# k6 targeting the search endpoint (scripts/load_test.js — ⚠️ not written yet)
k6 run scripts/load_test.js -e BASE_URL=http://localhost:8080 -e TOKEN=$TOKEN
```

**Linting/type-checking (intended for CI):** `ruff` + `mypy` for Python,
`dotnet format --verify-no-changes` for .NET. No CI workflows exist yet
([.github/workflows/](.github/workflows/) is empty).

---

## 14. Known Limitations & Future Improvements

**Current limitations**

- 🚧 **API embedding client is a stub** — `Infrastructure/Services/EmbeddingsService` throws
  `NotImplementedException`, so `GET /api/v1/movies/search` and `…/similar` fail through the API.
  Everything else in the API (auth, by-id, genres, stats, caching, observability) works, and the
  same search works end to end through the **MCP server**.
- The API queries pgvector **directly** rather than through the MCP server; compose passes
  `MCP__ServerUrl` but no MCP client exists in the API yet.
- **Compose build paths:** the `api` service's build context doesn't yet point at
  [api/MovieSearch/src/Api/Dockerfile](api/MovieSearch/src/Api/Dockerfile), and the bonus `atlas`
  service references a `scripts/Dockerfile` that doesn't exist.
- **No tests** — the committed test projects are template stubs; no Python tests.
- **No CI/CD** ([.github/workflows/](.github/workflows/) empty) and **no Terraform**
  ([terraform/](terraform/) empty).
- **No rate limiting** and no exported [openapi.json](openapi.json); the OpenAPI spec is served only
  in the Development environment.
- Two embedding backends (Ollama + TEI) are both in compose; the API's settings point at TEI while
  the pipeline/MCP use Ollama — consolidate on one before wiring the API client.
- Ollama pulls model weights on first start (cold start ~1–2 min); healthchecks account for this.

**Future improvements**

- Wire the API's `EmbeddingsService` to the embedding container (same model/dim as the pipeline) to
  unblock search/similar, then delete the unused backend.
- Fix the compose build contexts so `docker compose up --build` runs end to end on a clean machine.
- Real test suites: pytest for pipeline/MCP, xUnit unit + integration for the API, k6 load test
  validating p95 < 500ms.
- Rate limiting (60 req/min per authenticated user) and cache-TTL tuning.
- Hybrid search (vector similarity + full-text/metadata filters) with re-ranking.
- Tune the pgvector **HNSW** index (`m`, `ef_search`) and benchmark recall vs. latency at scale.
- CI/CD pipelines (lint, test, build, Compose integration, `terraform apply` to dev→prod) and the
  Terraform stack itself.
- Embedding Atlas bonus: `scripts/export_embeddings_atlas.py` + the `atlas` service Dockerfile.

---

> Maintainers: ⚠️ **PLACEHOLDER** · License: ⚠️ **PLACEHOLDER**
