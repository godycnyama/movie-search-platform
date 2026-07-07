# Movie Search Platform

An end-to-end semantic movie-search platform built on the **Vega movies dataset**: a Python **data
pipeline** that cleans, imputes, augments and embeds the catalogue; a **PostgreSQL 16 + pgvector**
store; a **Python FastMCP server** exposing semantic-search tools; and a secure, observable
**.NET 10 Web API** for end users — all orchestrated locally with **Docker Compose** and deployable
to **AWS via Terraform**.


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
                        │ (Scalar UI, web UI, curl, LLM agents via MCP Inspector) │
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
        │     PostgreSQL 16 + pgvector (:5432)       │    │  TEI embeddings (:8001)    │
        │  movies: metadata + augmented_text +       │    │  bge-base-en-v1.5 768-dim  │
        │  vector(768) · HNSW cosine index · users   │    │  (loaded on first start)   │
        └────────────────────▲──────────────────────┘    └────────────▲──────────────┘
                             │ alembic upgrade head + upsert           │ /embed
                    ┌────────┴─────────────────────────────────────────┴────────┐
                    │                      Data Pipeline                          │
                    │   (pipeline/, one-shot) Vega dataset → clean → impute →     │
                    │           augment → embed → load (idempotent)               │
                    └──────────────────────────────────────────────────────────────┘

  Cache:         Redis 7 (:6379) — API query-result cache (LRU, password-protected)
  Observability: OpenTelemetry → Jaeger (:16686) · Prometheus (:9090) · Grafana (:3000)
  Deployment:    Terraform (terraform/) → AWS ECS Fargate   see §12
  Bonus:         Embedding Atlas (:7000, compose profile "bonus")
```

Note: the API serves **all movie reads through the MCP server** (official `ModelContextProtocol`
C# SDK over SSE, one tool call per endpoint — see
[McpMovieCatalogService](api/MovieSearch/src/Infrastructure/Services/McpMovieCatalogService.cs));
query embedding and pgvector search happen inside the MCP server. The API touches Postgres
directly only for its own `users` table (auth). The same MCP tools remain consumable by any other
MCP client (LLM agents, MCP Inspector).

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


---

## 3. Quick Start

From a fresh clone to a running platform:

```bash
# 1. Clone
git clone <REPO_URL> movie-search-platform && cd movie-search-platform

# 2. Create your local environment file (git-ignored — never commit it)
cp .env.example .env            # then edit the REQUIRED secrets (see comments in the file)

# 3. Build & start the whole platform (db, redis, embeddings, mcp-server, api, observability)
docker compose up --build -d

# 4. The pipeline runs automatically as a one-shot service (migrations + load).
#    To re-run it manually:
docker compose run --rm pipeline

# 5. Verify the API is healthy
curl http://localhost:8080/health
```

On first start the `embeddings` (TEI) service downloads and loads the `BAAI/bge-base-en-v1.5`
model before the pipeline and MCP server come up (cold start ~1–2 min depending on bandwidth);
compose gates both on its `/health` check.

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
| Embedding Service (TEI) | http://localhost:8001 | HuggingFace TEI serving BAAI/bge-base-en-v1.5, 768-dim — local embedding backend for the pipeline & MCP server |
| PostgreSQL + pgvector | postgresql://localhost:5432 | Primary datastore (movies + users) |
| Redis | redis://localhost:6379 | API query-result cache (password from `.env`) |
| Prometheus | http://localhost:9090 | Metrics collection |
| Grafana | http://localhost:3000 | Dashboards (admin login from `.env`) |
| Jaeger UI | http://localhost:16686 | Distributed traces (OTLP in on 4317/4318) |

---

## 5. Data Pipeline

**Location:** [pipeline/](pipeline/) — entrypoint [pipeline/src/main.py](pipeline/src/main.py).
Runs as a one-shot Compose service: the container first applies schema migrations
(`alembic upgrade head`, versions mounted from [database/migrations/](database/migrations/)), then
executes the pipeline and exits. **Implemented.**

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
4. **Embed** ([embedding.py](pipeline/src/pipeline/embedding.py)) — call the **TEI embeddings
   service** (`BAAI/bge-base-en-v1.5`, 768-dim) over the network in configurable batches.
5. **Load** ([loader.py](pipeline/src/pipeline/loader.py)) — **idempotent upsert** into pgvector:
   movie IDs are deterministic (derived from title + release date), so re-running updates in place
   and never creates duplicates.

The combined cleaning + imputation report is written to **stdout and
`pipeline/logs/cleaning_report.json`** (the `logs/` directory is volume-mounted to the host).

### How to re-run

```bash
# Full run (idempotent — re-running does not create duplicates)
docker compose run --rm pipeline

# Locally with uv (requires postgres + the embeddings service reachable via env vars)
cd pipeline && uv sync && uv run alembic upgrade head && uv run python src/main.py
```

Configuration is typed settings bound from environment variables — see
[pipeline/src/pipeline/settings.py](pipeline/src/pipeline/settings.py) and
[.env.example](.env.example): `DATABASE_URL`, `EMBEDDINGS_URL`, `EMBEDDING_MODEL`, `EMBEDDING_DIM`,
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
over invented data. Implemented as described in
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

- **Model:** **`BAAI/bge-base-en-v1.5`**, **768 dimensions**, served by **HuggingFace Text
  Embeddings Inference (TEI)** in its own container — no in-process model download and no
  paid/hosted API. TEI downloads and loads the model once on first start, then exposes it over HTTP.
- **Why:** strong retrieval quality for its size (a top MTEB performer in the base tier), fully open
  and locally runnable, and a context window that comfortably fits the augmented movie text. It keeps
  all data in-house.
- **Dimensionality:** **768**. The pgvector column is declared `vector(768)` and `EMBEDDING_DIM=768`
  in [.env.example](.env.example) — the column and model **must** agree or loads fail.
- The API itself never embeds — its search queries are embedded by the MCP server.

### How the embedding container is wired into Docker Compose

```yaml
embeddings:                # local backend — pipeline & MCP server embed via http://embeddings:8001
  image: ghcr.io/huggingface/text-embeddings-inference:cpu-1.9
  command: ["--model-id", "BAAI/bge-base-en-v1.5", "--port", "8001"]
  healthcheck: ...         # /health goes healthy once the model is loaded
```

Both the **pipeline** (document embedding) and the **MCP server** (query embedding) point at
`http://embeddings:8001` via `EMBEDDINGS_URL` with the same `EMBEDDING_MODEL`, and each gates on the
service's `/health` check, so document and query vectors always come from the identical model.

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

**Location:** [mcp-server/](mcp-server/) — a **FastMCP** server exposing the movie catalogue as
semantic-search **tools** consumable by any MCP-compatible client (the .NET API, LLM agents, the
MCP Inspector). **Implemented.**

### Technology stack

| Concern | Choice | Notes |
|---------|--------|-------|
| Language / runtime | **Python 3.13** | Pinned in [pyproject.toml](mcp-server/pyproject.toml) (`requires-python >=3.13`) |
| MCP framework | **FastMCP** (`fastmcp >= 3.4.2`) | Derives each tool's input schema from its type-annotated signature |
| ASGI server | **uvicorn** | Serves the app via the ASGI factory ([asgi.py](mcp-server/src/server/asgi.py)) |
| Database driver | **asyncpg** connection pool + **pgvector** | Vector similarity search against Postgres ([db.py](mcp-server/src/server/db.py)) |
| Query embeddings | **httpx** → TEI (local) · **boto3** → Amazon Bedrock (dev/prod) | Same model as the pipeline; selected by `ENV` ([embeddings.py](mcp-server/src/server/embeddings.py)) |
| Models / validation | **Pydantic v2** + **pydantic-settings** + **SQLModel** | Typed tool I/O and env-bound settings |
| Metrics | **prometheus-client** | Per-tool call counts + durations on `GET /metrics` |
| Package / env manager | **uv** (`uv.lock` committed) | Reproducible installs |
| Lint · types · tests | **ruff** · **mypy** · **pytest** + **pytest-asyncio** | Dev dependency group; all run in CI |

### Transport & runtime

Transport is **SSE** by default (`MCP_TRANSPORT=sse`); `streamable-http` is supported for
production and `stdio` via `python -m server.main`. Structured JSON logs carry per-request IDs and
tool timings, and the server exposes `GET /health` and `GET /metrics`. In Docker it runs under
uvicorn.

### Available tools

All five tools from the spec — plus `get_movie_by_id`, which backs the .NET API's by-id endpoint —
are registered in [tools.py](mcp-server/src/server/tools.py). `top_k` is clamped to **[1, 50]**. The
.NET API is itself an MCP client of this server: every `/api/v1/movies/*` and `/api/v1/stats` read
maps to one of these tools.

| Tool | Description | Arguments | Returns |
|------|-------------|-----------|---------|
| `search_movies_by_description` | Semantic vector search with optional metadata filters; ranked results with similarity scores | `query: str`, `top_k: int = 10`, `genre_filter: str \| None`, `min_imdb_rating: float \| None`, `mpaa_rating: str \| None`, `decade: int \| None` | `list[MovieResult]` |
| `get_movie_by_id` | Retrieve a movie by its stable UUID | `movie_id: str` | `MovieResult \| null` |
| `get_movie_by_title` | Retrieve a movie by exact or fuzzy title match | `title: str` | `MovieResult \| null` |
| `get_similar_movies` | Most semantically similar movies to a given movie | `movie_id: str`, `top_k: int = 5` | `list[MovieResult]` |
| `list_genres` | All distinct genres in the dataset | — | `list[str]` |
| `get_dataset_stats` | Summary statistics (totals, embedding coverage, year range, avg IMDB rating, pipeline version) | — | `DatasetStats` |

> **Errors.** `get_movie_by_id` / `get_similar_movies` validate the UUID and raise a clear error on
> a malformed id; `get_similar_movies` also errors when the id is unknown. `get_movie_by_id` and
> `get_movie_by_title` return `null` when nothing matches.

### Tool reference — example inputs & outputs

**`search_movies_by_description`**

```jsonc
// input
{ "query": "sci-fi films directed by James Cameron", "top_k": 2, "min_imdb_rating": 7.5 }
```
```jsonc
// output — list[MovieResult] (ranked by cosine similarity, highest first)
[
  { "id": "9f1c2d3e-…", "title": "Terminator 2: Judgment Day", "release_year": 1991,
    "major_genre": "Action", "director": "James Cameron", "distributor": "TriStar Pictures",
    "mpaa_rating": "R", "imdb_rating": 8.5, "rotten_tomatoes_rating": 93,
    "production_budget": 102000000, "running_time_min": 137, "budget_tier": "blockbuster",
    "decade": 1990, "similarity_score": 0.87 },
  { "id": "c7b4…", "title": "Aliens", "release_year": 1986, "major_genre": "Action",
    "director": "James Cameron", "imdb_rating": 8.4, "similarity_score": 0.83 }
]
```

**`get_movie_by_id`**

```jsonc
// input
{ "movie_id": "9ddc4d0d-acde-45b1-8f32-a22fd9134d71" }
```
```jsonc
// output — MovieResult (similarity_score is null for direct lookups); null if the id is unknown
{ "id": "9ddc4d0d-acde-45b1-8f32-a22fd9134d71", "title": "Interstellar",
  "release_year": 2014, "major_genre": "Adventure", "director": "Christopher Nolan",
  "distributor": "Paramount Pictures", "mpaa_rating": "PG-13", "imdb_rating": 8.7,
  "rotten_tomatoes_rating": 91, "running_time_min": 169, "budget_tier": "high",
  "decade": 2010, "similarity_score": null }
```

**`get_movie_by_title`**

```jsonc
// input — exact (case-insensitive) match first, then fuzzy substring
{ "title": "interstellar" }
```
```jsonc
// output — MovieResult, or null when no title matches
{ "id": "9ddc4d0d-…", "title": "Interstellar", "release_year": 2014,
  "major_genre": "Adventure", "director": "Christopher Nolan", "imdb_rating": 8.7,
  "similarity_score": null }
```

**`get_similar_movies`**

```jsonc
// input
{ "movie_id": "9ddc4d0d-acde-45b1-8f32-a22fd9134d71", "top_k": 2 }
```
```jsonc
// output — list[MovieResult] most similar to the source movie
[
  { "id": "d8c918…", "title": "The Martian", "release_year": 2015, "major_genre": "Adventure",
    "imdb_rating": 8.0, "similarity_score": 0.95 },
  { "id": "e4ab62…", "title": "Gravity", "release_year": 2013, "major_genre": "Drama",
    "imdb_rating": 7.7, "similarity_score": 0.92 }
]
```

**`list_genres`**

```jsonc
// input — none
{}
```
```jsonc
// output — list[str]
["Action", "Adventure", "Comedy", "Drama", "Horror", "Musical", "Thriller", "Western"]
```

**`get_dataset_stats`**

```jsonc
// input — none
{}
```
```jsonc
// output — DatasetStats
{ "total_movies": 3201, "with_embeddings": 3201, "genres": 12,
  "year_range": [1915, 2010], "avg_imdb_rating": 6.28, "pipeline_version": "0.1.0" }
```

### Exercise the tools directly

```bash
# Interactive: point the MCP Inspector at the SSE endpoint (no code required)
npx @modelcontextprotocol/inspector
#   → Transport: SSE, URL: http://localhost:8000/sse   then call any tool above

# Health check
curl http://localhost:8000/health
```

Example natural-language queries the system handles: *"action movies from the 90s with high IMDB
ratings"*, *"critically acclaimed drama films with small budgets"*, *"animated family movies
distributed by Disney"*, *"sci-fi films directed by James Cameron"*, *"dark psychological thrillers
with low Rotten Tomatoes scores"*.

### Test & lint the MCP server locally

All commands run from [mcp-server/](mcp-server/) and use **uv** (installs the `dev` dependency
group). These mirror the CI job in [.github/workflows/ci.yml](.github/workflows/ci.yml):

```bash
cd mcp-server
uv sync                              # create the venv and install deps (incl. dev tools)

uv run ruff check .                  # lint  (rules: E, F, I, UP, B — line length 100)
uv run ruff format .                 # optional: auto-format
uvx mypy --ignore-missing-imports src  # static type check
uv run pytest -q                     # tests — all six tools via FastMCP's in-memory client,
                                     #         plus the /health and /metrics HTTP routes
```

Tests run against fake db/embeddings backends, so no Postgres or model server is required.

---

## 9. API Documentation

**Location:** [api/MovieSearch/](api/MovieSearch/) — a **.NET 10** REST API providing secure user
authentication and AI-powered movie search. All movie/stats reads are MCP tool calls against the
[MCP server](#8-mcp-server) (the official **ModelContextProtocol** C# SDK over SSE — the API never
queries the movies tables directly); EF Core is used only for the API-owned `users` table.

### Architecture & design patterns

The solution combines **Clean Architecture** with **Vertical Slice Architecture**, keeping the
codebase modular, testable and easy to navigate. It is a layered solution —
`Domain` / `Application` / `Infrastructure` / `Api` — with these patterns:

| Pattern | How it's applied | Why it matters |
|---------|------------------|----------------|
| **Vertical Slice Architecture** | Each feature is a self-contained slice ([Application/Features/](api/MovieSearch/src/Application/Features/)) bundling its **endpoint, request model, validation, command/query, handler and response model** in one place (via **Carter** endpoint modules) | Related code lives together; minimal coupling between features |
| **CQRS** (via **Wolverine**) | Commands and queries are dispatched through Wolverine as the in-process mediator | Loosely coupled handlers, simple DI, high testability, an extensible messaging pipeline with built-in middleware support |
| **Result Pattern** | Handlers return a shared `Result<T>` instead of throwing for *expected* application errors (not found, conflict, validation) | Better performance, explicit success/failure handling, cleaner business logic, easier unit testing |
| **Repository Pattern** | EF Core repositories abstract persistence for the `users` store behind interfaces in the Application layer | Business logic stays independent of the database implementation |

Inputs are validated with data annotations (→ RFC 7807 `ValidationProblem`), and domain errors map
to `application/problem+json` responses through the `Result<T>` type and a global exception handler.

### Technology stack

| Concern | Choice |
|---------|--------|
| Runtime / framework | **.NET 10** · **ASP.NET Core Minimal APIs** (Carter endpoint slices) |
| Messaging / CQRS | **Wolverine** (in-process mediator) |
| Persistence (users) | **PostgreSQL 16** via **Entity Framework Core** |
| Movie data source | **ModelContextProtocol** C# SDK → MCP server (SSE) |
| Cache | **Redis** (query-result caching in the query handlers) |
| AuthN / AuthZ | **JWT Bearer** tokens · role-based authorization |
| API docs | **OpenAPI** spec + **Scalar** interactive UI |
| Observability | **OpenTelemetry** (traces + Prometheus metrics) — see §11 |

**Why Minimal APIs over controllers?** Minimal APIs give lower request overhead and faster startup, and — paired with **Carter** —
let each vertical slice register its own route inline alongside its handler, request and response
models. That keeps a feature's endpoint in the same file as its logic (reinforcing the Vertical
Slice design) instead of scattering it across a separate `Controllers/` folder, while still
producing the same OpenAPI document and supporting the same auth, validation and versioning.

### Using the Scalar UI

Interactive API docs are served by **Scalar** at **http://localhost:8080/scalar** (the raw OpenAPI spec is at `/openapi/v1.json`, and a copy is committed at
[openapi.json](openapi.json)). To exercise the endpoints from the browser:

1. Open **http://localhost:8080/scalar**.
2. First get a token: expand **`POST /api/v1/auth/signup`** (or `login`), fill the request body,
   and **Send** — copy the `access_token` from the response.
3. Click **Authentication** (top of the page) and paste the token into the **Bearer** field. Scalar
   now sends `Authorization: Bearer <token>` on every request.
4. Pick any operation, fill in the **path/query parameters** or **request body**, and press **Send**
   to see the live response.

The examples below show, for each endpoint, what you enter in Scalar and the response you get back.

### Endpoint summary

| Method | Path | Auth | Purpose |
|--------|------|------|---------|
| `GET`  | `/health` | none | Liveness + readiness (per-dependency status) |
| `POST` | `/api/v1/auth/signup` | anonymous | Create an account, returns a bearer token |
| `POST` | `/api/v1/auth/login` | anonymous | Log in, returns a bearer token |
| `POST` | `/api/v1/auth/change-password` | any user | Change the caller's password |
| `POST` | `/api/v1/auth/assignadminrole` | **admin** | Promote another account to `admin` |
| `GET`  | `/api/v1/movies/search` | reader+ | Natural-language semantic search |
| `GET`  | `/api/v1/movies/{id}` | reader+ | Get one movie by UUID |
| `GET`  | `/api/v1/movies/by-title` | reader+ | Get one movie by title (exact → fuzzy) |
| `GET`  | `/api/v1/movies/{id}/similar` | reader+ | Movies similar to a given movie |
| `GET`  | `/api/v1/movies/genres` | reader+ | Distinct genres |
| `GET`  | `/api/v1/stats` | **admin** | Dataset statistics |

---

### Auth endpoints

#### `POST /api/v1/auth/signup` — create account *(anonymous)*

The **first account ever created becomes `admin`**; every subsequent sign-up is a `reader`.
Passwords must be ≥ 8 chars with upper, lower, digit and symbol. Returns **201** with a token, or
**409** if the email is already registered.

**In Scalar:** select the operation → paste this into the request body → **Send**.

```jsonc
// Request body
{ "email": "john@example.com", "password": "MyPassword123!" }
```
```jsonc
// Response — 201 Created
{ "access_token": "<jwt-token>", "token_type": "Bearer", "expires_in": 3600, "role": "reader" }
```

#### `POST /api/v1/auth/login` — obtain a token *(anonymous)*

Returns **200** with a token, or **401** for unknown email *or* wrong password (identical error, so
account existence is never leaked).

```jsonc
// Request body
{ "email": "john@example.com", "password": "MyPassword123!" }
```
```jsonc
// Response — 200 OK
{ "access_token": "<jwt-token>", "token_type": "Bearer", "expires_in": 3600, "role": "reader" }
```

#### `POST /api/v1/auth/change-password` — rotate password *(any authenticated user)*

Requires a Bearer token (set it under **Authentication** in Scalar). Verifies the current password
(**400** if wrong), then sets the new one.

```jsonc
// Request body
{ "current_password": "MyPassword123!", "new_password": "NewPassword123!" }
```
```jsonc
// Response — 200 OK
{ "message": "Password changed successfully." }
```

#### `POST /api/v1/auth/assignadminrole` — promote a user *(admin only)*

Requires an **admin** Bearer token. **403** for non-admin callers, **404** if the target email does
not exist.

```jsonc
// Request body
{ "email": "john@example.com" }
```
```jsonc
// Response — 200 OK
{ "message": "Admin role assigned successfully." }
```

---

### Health endpoint

#### `GET /health` — liveness + readiness *(no auth)*

No parameters — just **Send**.

```jsonc
// Response — 200 OK
{ "status": "Healthy", "dependencies": { "postgres": "Healthy", "mcp-server": "Healthy" } }
```

---

### Movie endpoints *(require a valid JWT — set it under Authentication in Scalar)*

#### `GET /api/v1/movies/search` — natural-language search

Results are cached in Redis per query + filters.

**In Scalar:** fill the query parameters →

| Parameter | Required | Notes |
|-----------|----------|-------|
| `query` | yes | Natural-language search text |
| `top_k` | no | Default 10, clamped to 50 |
| `genre` | no | Exact genre filter |
| `min_imdb_rating` | no | Minimum IMDB rating |
| `mpaa_rating` | no | e.g. `PG-13` |
| `decade` | no | e.g. `1990` |

```
// Example request
GET /api/v1/movies/search?query=space adventure&top_k=2
```
```jsonc
// Response — 200 OK
{
  "query": "space adventure",
  "count": 2,
  "results": [
    { "id": "9ddc4d0d-…", "title": "Interstellar", "release_year": 2014,
      "major_genre": "Adventure", "director": "Christopher Nolan", "imdb_rating": 8.7,
      "rt_rating": 91, "similarity_score": 0.96 },
    { "id": "d8c918…", "title": "The Martian", "release_year": 2015,
      "major_genre": "Adventure", "director": "Ridley Scott", "imdb_rating": 8.0,
      "rt_rating": 91, "similarity_score": 0.94 }
  ]
}
```

#### `GET /api/v1/movies/{id}` — get movie by ID

Enter the movie UUID as the `id` path parameter. Returns the full movie record, or **404** if
unknown.

```
// Example request
GET /api/v1/movies/9ddc4d0d-acde-45b1-8f32-a22fd9134d71
```
```jsonc
// Response — 200 OK
{ "id": "9ddc4d0d-acde-45b1-8f32-a22fd9134d71", "title": "Interstellar",
  "release_year": 2014, "major_genre": "Adventure", "director": "Christopher Nolan",
  "distributor": "Paramount Pictures", "mpaa_rating": "PG-13", "imdb_rating": 8.7,
  "rt_rating": 91, "running_time_min": 169, "budget_tier": "high", "decade": 2010 }
```

#### `GET /api/v1/movies/by-title` — get movie by title

Enter the `title` query parameter. Exact (case-insensitive) match first, then fuzzy substring —
same semantics as the MCP `get_movie_by_title` tool it calls. Returns the movie record or **404**.

```
// Example request
GET /api/v1/movies/by-title?title=Interstellar
```
```jsonc
// Response — 200 OK
{ "id": "9ddc4d0d-acde-45b1-8f32-a22fd9134d71", "title": "Interstellar", "release_year": 2014,
  "major_genre": "Adventure", "director": "Christopher Nolan", "imdb_rating": 8.7 }
```

#### `GET /api/v1/movies/{id}/similar` — similar movies

Enter the source movie's `id` path parameter and optional `top_k` (default 5, clamped to 50).
Returns **404** if the source id is unknown.

```
// Example request
GET /api/v1/movies/9ddc4d0d-acde-45b1-8f32-a22fd9134d71/similar?top_k=2
```
```jsonc
// Response — 200 OK
{
  "source_id": "9ddc4d0d-acde-45b1-8f32-a22fd9134d71",
  "results": [
    { "id": "d8c918…", "title": "The Martian", "similarity_score": 0.95 },
    { "id": "e4ab62…", "title": "Gravity",     "similarity_score": 0.92 }
  ]
}
```

#### `GET /api/v1/movies/genres` — list genres

No parameters — just **Send**.

```
// Example request
GET /api/v1/movies/genres
```
```jsonc
// Response — 200 OK
{ "genres": ["Action", "Adventure", "Comedy", "Drama", "Horror", "Musical", "Thriller", "Western"] }
```

---

### Statistics endpoint

#### `GET /api/v1/stats` — dataset statistics *(admin only)*

Requires an **admin** Bearer token; **403** for non-admin callers. No parameters.

```
// Example request
GET /api/v1/stats
```
```jsonc
// Response — 200 OK
{ "total_movies": 3201, "with_embeddings": 3201, "genres": 12,
  "year_range": [1915, 2010], "avg_imdb_rating": 6.28, "pipeline_version": "0.1.0" }
```

### Linting the .NET API

The API is formatted and lint-checked with `dotnet format` (enforced by CI —
[.github/workflows/ci.yml](.github/workflows/ci.yml)):

```bash
# Verify formatting/analyzers with no changes (this is exactly what CI runs)
dotnet format api/MovieSearch/MovieSearch.slnx --verify-no-changes --verbosity minimal

# Auto-apply fixes locally
dotnet format api/MovieSearch/MovieSearch.slnx
```

---

## 10. Authentication

The API uses **JWT Bearer token** authentication with email/password accounts stored in Postgres
(`users` table, **PBKDF2** password hashing). Two roles:

- **`reader`** — all movie endpoints (`/api/v1/movies/*`).
- **`admin`** — everything a reader can do, plus `/api/v1/stats` and `/api/v1/auth/assignadminrole`.

**Role assignment:** the **first account created becomes `admin`** (bootstrap); every subsequent
sign-up is a `reader`. An existing admin can promote others via
`POST /api/v1/auth/assignadminrole`. Full request/response examples for all four auth endpoints are
in [§9 → Auth endpoints](#auth-endpoints).

### Typical flow

```bash
# 1. Sign up (or log in) and capture the token
TOKEN=$(curl -s -X POST http://localhost:8080/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"you@example.com","password":"<strong password>"}' | jq -r .access_token)

# 2. Call a protected endpoint with the bearer token
curl "http://localhost:8080/api/v1/movies/genres" -H "Authorization: Bearer $TOKEN"
```

- The JWT signing key (`JWT_SIGNING_KEY`), issuer and audience are read from `.env` — never
  hardcoded or committed. In AWS these would come from Secrets Manager.
- Login returns the same error for unknown email and wrong password, so account existence is not
  leaked. Tokens expire (`expires_in`); log in again on `401 Unauthorized`.
- Calling an admin-only endpoint with a `reader` token returns `403 Forbidden`.

---

## 11. Observability

**Location:** [monitoring/](monitoring/) — Prometheus scrape config, Grafana datasource +
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

**Location:** [terraform/](terraform/) — **implemented** (see the detailed
[terraform/README.md](terraform/README.md)). Target: **AWS ECS Fargate** — the **.NET API** and
**MCP server** each autoscale between **1 and 2 tasks** (CPU target tracking), with query and
document embeddings served by **Amazon Bedrock** (no in-cluster inference) and the **pipeline** as
a one-off ECS task. Backed by **RDS PostgreSQL 16 (pgvector)** and **ElastiCache Redis** in private subnets,
images in **ECR** (scan-on-push), a public **ALB** (HTTP now; supply `acm_certificate_arn` to turn
on HTTPS + redirect), and CloudWatch alarms → SNS.

```
terraform/
├── main.tf · variables.tf · outputs.tf · versions.tf    # platform composition module
├── modules/{networking,ecr,secrets,rds,elasticache,alb,compute,iam,monitoring}/
└── environments/{dev,prod}/                             # dev: single-AZ; prod: Multi-AZ + protection
```

The S3 state bucket + DynamoDB lock table are provisioned and managed separately
(their own repo/process), then referenced via each environment's `backend.hcl`.

Infrastructure guarantees: all secrets **generated in-stack** and stored in Secrets Manager
(injected into tasks via `valueFrom` — no credentials in tfvars, source, or plain env vars);
compute uses IAM roles (GitHub deploys via **OIDC**, no access keys); RDS/Redis in private subnets
only; CPU- and memory-based auto-scaling; **VPC Flow Logs** enabled; **remote state in S3 with
DynamoDB locking** (managed separately); every resource tagged `Environment`, `Project`, `ManagedBy`
via provider `default_tags`.

---

## 13. Running Tests

Three suites, all run by CI ([.github/workflows/ci.yml](.github/workflows/ci.yml)) on every PR
and push to master:

- **pipeline** (pytest) — cleaning (date quirks, impossible-value nulling, dedup), imputation
  (genre-median runtimes, real categories), augmentation (budget tiers, embedding text),
  deterministic loader ids, and the TEI client (retries, dimension guard) via mock transports.
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

The load test is driven by [k6](https://k6.io), which must be installed on the machine first.
On Windows, install it with winget:

```powershell
winget install k6 --source winget
```

Confirm the install succeeded:

```powershell
k6 --version
```

Bring up the whole platform so the API (and its MCP/DB/Redis dependencies) is running. From the
root of the repo:

```bash
docker compose up
```

Confirm the API is up by opening <http://localhost:8080/scalar/> in a browser.

Then, from the root of the repo, run the load test:

```powershell
k6 run .\scripts\load_test.js
```

The results are printed when the run completes. The script signs up its own throwaway users, then
mixes the movie read endpoints (semantic search / by-title / genres). Thresholds fail the run if
p95 >= 500ms or the error rate exceeds 1%.

### Linting & static analysis

All of the below are enforced by CI ([.github/workflows/ci.yml](.github/workflows/ci.yml)):

```bash
# .NET API — format + analyzer check (CI uses --verify-no-changes; drop it to auto-fix)
dotnet format api/MovieSearch/MovieSearch.slnx --verify-no-changes --verbosity minimal

# Python services — ruff lint + mypy type check (run in each project dir)
cd pipeline   && uv run ruff check . && uvx mypy --ignore-missing-imports src
cd mcp-server && uv run ruff check . && uvx mypy --ignore-missing-imports src
```

Ruff is configured per project in `pyproject.toml` (rule set `E, F, I, UP, B`, line length 100).
`terraform fmt -check -recursive` and `terraform validate` cover the IaC.

### Continuous delivery

CD ([.github/workflows/cd.yml](.github/workflows/cd.yml)) builds and pushes the images to ECR via
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
- Locally, the pipeline and MCP server both embed via the single **TEI** `embeddings` service
  (`BAAI/bge-base-en-v1.5`, 768-dim); the AWS stack swaps this for **Amazon Bedrock** (`ENV`
  drives dev/prod → Bedrock), so there is no in-cluster inference in the cloud.
- TEI downloads model weights on first start (cold start ~1–2 min locally, cached in the
  `model-cache` volume thereafter); healthchecks gate dependents until the model is loaded.

**Future improvements**

- Integration tests that exercise the API → MCP server → pgvector path end to end
  (unit tests currently cover each side against fakes).
- Cache-TTL tuning.
- Hybrid search (vector similarity + full-text/metadata filters) with re-ranking.
- Tune the pgvector **HNSW** index (`m`, `ef_search`) and benchmark recall vs. latency at scale.
- HTTPS on the ALB (provide `acm_certificate_arn` once a domain/cert exists) and a Route53 alias.
- Ship the local Grafana dashboard to the AWS environments (Amazon Managed Grafana or
  container-based) — CloudWatch alarms cover the basics today.
