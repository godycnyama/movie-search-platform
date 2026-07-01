# 🎬 Intelligent Movie Search Platform

An end-to-end semantic movie-search platform built on the **Vega movies dataset**: a Python **data
pipeline** that cleans, imputes, augments and embeds the catalogue; a **PostgreSQL 16 + pgvector**
store; a **Python FastMCP server** exposing semantic-search tools; and a secure, observable
**.NET 10 Web API** for end users — all orchestrated locally with **Docker Compose** and deployable
to **AWS via Terraform**.

> ⚠️ **Project status — scaffolding.** This repository currently contains the orchestration and
> documentation, plus `uv`-generated stubs in [mcp-server/](mcp-server/) and [pipeline/](pipeline/).
> [docker-compose.yml](docker-compose.yml) and [.env.example](.env.example) are committed; the
> per-service `Dockerfile`s and application code (pipeline stages, MCP tools, .NET API, Terraform,
> monitoring configs) are **not yet implemented**. Sections below marked ⚠️ **PLACEHOLDER** describe
> the intended design and must be updated as each component lands. This README follows the structure
> required by the technical assessment brief.

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
                        │      (web UI, curl, Swagger, LLM agents via MCP)         │
                        └───────────────┬───────────────────────┬─────────────────┘
                                        │ HTTPS/REST (JWT)       │ MCP (SSE)
                                        ▼                        │
                        ┌───────────────────────┐                │
                        │      .NET 10 API       │                │
                        │      (api/, :8080)     │                │
                        │  JWT auth · OpenAPI    │                │
                        │  cache · rate limit    │─── MCP client ─┤
                        └───────────┬───────────┘                ▼
                                    │              ┌───────────────────────────┐
                                    │              │       MCP Server          │
                                    │              │   (mcp-server/, :8000)    │
                                    │              │  FastMCP semantic tools   │
                                    │              └─────────────┬─────────────┘
                                    │                            │ asyncpg pool
                                    │      query embeddings      │
                                    ▼                            ▼
                        ┌───────────────────────┐    ┌───────────────────────────┐
                        │   Embedding Service    │◄───┤   query + doc embeddings  │
                        │  (embeddings, :8001)   │    └───────────────────────────┘
                        │  nomic-embed 768-dim   │
                        └───────────┬───────────┘
                                    │ vectors
                                    ▼
                        ┌────────────────────────────────────────────────────────┐
                        │            PostgreSQL 16 + pgvector (:5432)              │
                        │     movies: metadata + augmented_text + vector(768)      │
                        │            HNSW cosine index · audit columns             │
                        └───────────────────────────▲────────────────────────────┘
                                    ▲ reads embeddings │ bulk load + embed
              ┌─────────────────────┴──────┐          │
              │  Embedding Atlas (:7000)   │  ┌────────┴────────────────────────┐
              │  visualization (bonus)     │  │           Data Pipeline          │
              └────────────────────────────┘  │  (pipeline/) Vega dataset →      │
                                               │  clean → impute → augment →      │
                                               │  embed → load (idempotent)       │
                                               └──────────────────────────────────┘

  Observability: Serilog (JSON) + OpenTelemetry → Jaeger (:16686) · Prometheus (:9090) · Grafana (:3000)
  Deployment:    Terraform (terraform/) → AWS ECS Fargate + RDS + ALB + ECR + Secrets Manager
  Flow:          Pipeline → pgvector → MCP Server → .NET API → client   (traces span all services)
```

> ⚠️ **PLACEHOLDER:** Replace with a linked `docs/architecture.png` once component boundaries are final.

### Repository layout

```
movie-search-platform/
├── README.md                 # This file
├── docker-compose.yml        # Local orchestration (Part 6.1)              ✅ committed
├── .env.example              # Environment template                        ✅ committed
├── openapi.json              # Exported OpenAPI 3.1 spec                    ⚠️ to add
├── pipeline/                 # Part 1 — data pipeline (Python)
├── mcp-server/               # Part 3 — FastMCP server (Python)
├── api/                      # Part 4 — .NET 10 Web API
├── database/migrations/      # Part 2 — SQL migrations (Flyway naming)
├── scripts/                  # export_embeddings_atlas.py, load_test.js
├── monitoring/               # prometheus.yml, grafana/ dashboards
├── terraform/                # Part 6 — AWS IaC
└── .github/workflows/        # ci.yml, cd.yml
```

---

## 2. Prerequisites

Exact versions the platform targets:

| Tool | Required version | Notes |
|------|------------------|-------|
| Docker Engine | **≥ 24.0** | Everything runs via Compose; this is the only hard requirement to run the platform |
| Docker Compose | **v2 (≥ 2.20)** | `docker compose`, not legacy `docker-compose` |
| Python | **3.12+** (repo pins **3.13** via `.python-version`) | Only needed to run [pipeline/](pipeline/) or [mcp-server/](mcp-server/) outside Docker |
| [uv](https://github.com/astral-sh/uv) | **≥ 0.5** | Python package & venv manager for both Python services |
| .NET SDK | **10.0** | Only needed to build/run [api/](api/) outside Docker |
| PostgreSQL | **16.x** + `pgvector` **≥ 0.7** | Provided by the `pgvector/pgvector:pg16` image |
| Terraform | **≥ 1.7** | For AWS deployment |
| AWS CLI | **v2** | Authenticated to the target account (SSO or named profile) |

> All tools should be installed from official sources, and new project dependencies should go through
> the organisation's approved dependency-vetting process before use.

---

## 3. Quick Start

From a fresh clone to a running platform in ≤ 5 commands:

```bash
# 1. Clone
git clone <REPO_URL> movie-search-platform && cd movie-search-platform

# 2. Create your local environment file (git-ignored — never commit it)
cp .env.example .env            # then edit the REQUIRED secrets (see comments in the file)

# 3. Build & start the whole platform (db, embeddings, mcp-server, api, observability)
docker compose up --build -d

# 4. Run the one-shot data pipeline to clean, embed and load the Vega catalogue
docker compose run --rm pipeline

# 5. Verify the API is healthy
curl http://localhost:8080/health
```

Add the bonus Embedding Atlas with a profile: `docker compose --profile bonus up -d atlas`.

> **Credentials:** all secrets (DB password, JWT signing key, Grafana admin password) are read from
> the git-ignored `.env` file — never hardcoded in code or committed config. The compose file uses
> `${VAR:?…}` so it fails fast if a required secret is missing. If this platform is exposed to a wider
> internal audience, contact the AI Engineering team before deploying.

> ⚠️ Until each service's `Dockerfile` and code are committed, `up --build` will fail at the build
> step for the not-yet-implemented services.

---

## 4. Service Endpoints

Local URLs when running via Docker Compose (ports per [docker-compose.yml](docker-compose.yml)):

| Service | URL | Purpose |
|---------|-----|---------|
| .NET 10 API | http://localhost:8080 | Public-facing search API |
| API — Swagger UI | http://localhost:8080/swagger | Interactive OpenAPI docs |
| API — OpenAPI spec | http://localhost:8080/openapi/v1.json | OpenAPI 3.1 spec |
| API — health | http://localhost:8080/health | Liveness/readiness probe |
| API — metrics | http://localhost:8080/metrics | Prometheus metrics |
| MCP Server | http://localhost:8000/sse | FastMCP endpoint (SSE) for MCP clients |
| MCP — health | http://localhost:8000/health | MCP health check |
| Embedding Service | http://localhost:8001 | Text → vector embedding (768-dim) |
| PostgreSQL + pgvector | postgresql://localhost:5432 | Primary datastore |
| Prometheus | http://localhost:9090 | Metrics collection |
| Grafana | http://localhost:3000 | Dashboards (admin login from `.env`) |
| Jaeger UI | http://localhost:16686 | Distributed traces |
| Embedding Atlas (bonus) | http://localhost:7000 | Embedding visualization |

---

## 5. Data Pipeline

**Location:** [pipeline/](pipeline/) — entrypoint [pipeline/main.py](pipeline/main.py). Runs as a
one-shot Compose service that executes and exits.

### How it works

The pipeline ingests the **Vega movies dataset** (`from vega_datasets import data; data.movies()`) and
prepares it for vector search through modular stages:

1. **Clean** (`cleaning.py`) — de-duplicate; strip/normalise string fields; parse `Release Date` into a
   consistent datetime; validate numeric ranges (reject negative budgets/gross, impossible ratings).
   Emits a **structured cleaning report** (counts of issues found and actions taken).
2. **Impute** (`imputation.py`) — fill or `NULL` missing values per the policy in [Data Decisions](#6-data-decisions).
3. **Augment** (`augmentation.py`) — build the rich embedding text (see [Embedding Strategy](#7-embedding-strategy))
   and engineer derived features: **`budget_tier`**, **`decade`** (plus `blockbuster_flag`).
4. **Embed** (`embedding.py`) — call the embedding service over the network in configurable batches,
   logging progress and failures.
5. **Load** (`loader.py`) — **idempotent upsert** of metadata + `vector(768)` into pgvector.

A final summary report is emitted to **stdout and a log file** (`pipeline/logs/`).

### How to re-run

```bash
# Full run (idempotent — re-running does not create duplicates)
docker compose run --rm pipeline

# Locally with uv (requires postgres + embeddings reachable via env vars)
cd pipeline && uv sync && uv run python -m pipeline.main
```

Batch size, DB URL, embedding URL and pipeline version come from environment variables
(`PIPELINE_BATCH_SIZE`, `DATABASE_URL`, `EMBEDDINGS_URL`, `PIPELINE_VERSION`) — see [.env.example](.env.example).

### How to verify

```bash
# Row + embedding coverage
docker compose exec postgres psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" \
  -c "SELECT count(*) AS movies, count(embedding) AS embedded FROM movies;"

# End-to-end semantic search (needs a bearer token — see Authentication)
curl "http://localhost:8080/api/v1/movies/search?q=action%20movies%20from%20the%2090s%20with%20high%20IMDB%20ratings" \
  -H "Authorization: Bearer $TOKEN"
```

Expected: `embedded == movies` (every row has a vector), and search returns ranked results with
similarity scores.

> ⚠️ **PLACEHOLDER:** Document exact record counts, runtime, and the cleaning-report summary once the
> pipeline is implemented.

---

## 6. Data Decisions

Cleaning and imputation strategy across the Vega fields, chosen to **preserve data quality for
semantic search** — the guiding principle is to never fabricate values that feed the embedding text or
that users filter/sort on. Prefer an honest `NULL` (or a real "Unknown"/"Not Rated" category) over
invented data. ⚠️ **PLACEHOLDER — confirm/adjust once the pipeline is implemented.**

| Field | Strategy | Why |
|-------|----------|-----|
| Duplicates | De-duplicate on normalised `(Title, Release Date)`; keep the record with the most non-null fields | Prevents double-counting in ranked search results |
| `Title` | Trim/normalise; drop rows with no title | A movie with no title is neither identifiable nor searchable |
| `Release Date` | Parse to `datetime`; leave `NULL` if unparseable | Enables `decade` derivation and time filters without guessing |
| `IMDB Rating` | Leave **`NULL`** (no imputation) | The `min_imdb_rating` filter and ranking depend on truthful values; median imputation would inflate low-information films |
| `Rotten Tomatoes Rating` | Leave **`NULL`** | Same as IMDB — critical-score queries must not see fabricated scores |
| `Production Budget` | Leave **`NULL`**; derive `budget_tier` only when known | Budgets span orders of magnitude; imputing distorts `budget_tier` and budget-based queries |
| `Running Time min` | Median imputation within `Major Genre`, with a `runtime_imputed` flag | Runtime is low-signal and roughly regular within a genre; median is robust to outliers |
| `MPAA Rating` | Impute the real category **`"Not Rated"`** | "Not Rated" is semantically accurate for missing/unrated titles — better than mode |
| `Director` | Impute **`"Unknown"`** | Preserves the row; avoids wrongly attributing a film to a real director |
| `Distributor` | Impute **`"Unknown"`** | Same rationale as Director |
| `Major Genre`, `Creative Type`, `Source` | Impute **`"Unknown"`** | Keeps faceting/filtering stable without inventing categories |
| Numeric ranges | Coerce out-of-range values to `NULL` (negative budgets/gross; IMDB ∉ [0,10]; RT ∉ [0,100]) | Removes impossible values before they poison filters and derived features |

**Derived features (documented rationale):**
- **`budget_tier`** — bucketises `Production Budget` (e.g. low/mid/high/blockbuster) so budget-relative
  queries ("small budget", "blockbuster") work without exact figures.
- **`decade`** — integer decade from `Release Date` (e.g. 1990) powering the `decade` filter and
  "from the 90s"-style queries.
- **`blockbuster_flag`** — boolean from high budget + high gross, useful for popularity intent.

---

## 7. Embedding Strategy

### Model choice & rationale

- **Model:** **`nomic-ai/nomic-embed-text-v1.5`**, run as its own container (per the brief). No
  in-process model download and no paid/hosted API — the pipeline and MCP server call it over the
  network.
- **Why:** strong retrieval quality for its size, fully open and locally runnable, and long context
  (2048 tokens) that comfortably fits the augmented movie text. It keeps all data in-house.
- **Dimensionality:** **768**. The pgvector column is declared `vector(768)` and `EMBEDDING_DIM=768`
  in [.env.example](.env.example) — the column and model **must** agree or loads fail.

### How the embedding container is wired into Docker Compose

The `embeddings` service in [docker-compose.yml](docker-compose.yml) serves the model over HTTP on
port **8001**. The brief suggests the Docker Model Runner image `ai/nomic-embed-text-v1.5`; this repo
uses **HuggingFace Text Embeddings Inference (TEI)** to serve the same model with a ready `/health`
endpoint (swap the image/command for Docker Model Runner or Ollama if preferred — keep 768-dim):

```yaml
embeddings:
  image: ghcr.io/huggingface/text-embeddings-inference:cpu-1.5
  command: ["--model-id", "nomic-ai/nomic-embed-text-v1.5", "--port", "8001"]
  ports: ["8001:8001"]
  volumes: [model-cache:/data]        # persist weights across restarts
  healthcheck:
    test: ["CMD-SHELL", "curl -fsS http://localhost:8001/health || exit 1"]
    start_period: 120s
```

Both the **pipeline** (document embedding) and the **MCP server** (query embedding) point at
`http://embeddings:8001` via `EMBEDDINGS_URL`, so document and query vectors always come from the
identical model.

### How the embedding text was constructed

Each movie is serialised into the rich text block specified by the brief, then embedded:

```
Title: {title}
Genre: {genre}
Director: {director}
MPAA Rating: {mpaa_rating}
Release Year: {year}
Runtime: {runtime} minutes
IMDB Rating: {imdb_rating}/10 ({imdb_votes} votes)
Rotten Tomatoes: {rt_rating}%
Budget: ${budget}
Distributor: {distributor}
Creative Type: {creative_type}
Source: {source}
```

Fields imputed as `NULL` are rendered as `"Unknown"`/omitted rather than fabricated. The exact
serialisation lives in `pipeline/src/pipeline/augmentation.py` and the produced string is stored in the
`augmented_text` column for transparency and re-embedding.

---

## 8. MCP Server

**Location:** [mcp-server/](mcp-server/) — a **FastMCP** server exposing movie search as MCP tools
consumable by any MCP-compatible client (including the .NET API). Transport is **SSE** locally
(configurable for production), with an `asyncpg` connection pool to pgvector, Pydantic v2 models,
JSON structured logging, and a `GET /health` endpoint.

### Available tools

⚠️ **PLACEHOLDER — signatures per the brief; update once registered in `mcp-server/src/server/tools.py`.**

| Tool | Description | Arguments |
|------|-------------|-----------|
| `search_movies_by_description` | Semantic vector search with optional metadata filters; returns ranked results with similarity scores | `query: str`, `top_k: int = 10`, `genre_filter: str \| None`, `min_imdb_rating: float \| None`, `mpaa_rating: str \| None`, `decade: int \| None` |
| `get_movie_by_title` | Retrieve a movie by exact or fuzzy title match | `title: str` |
| `get_similar_movies` | Most semantically similar movies to a given movie | `movie_id: str`, `top_k: int = 5` |
| `list_genres` | All distinct genres in the dataset | — |
| `get_dataset_stats` | Summary statistics about the dataset | — |

### How to test the tools directly

```bash
# Interactive: point the MCP Inspector at the SSE endpoint (no code required)
npx @modelcontextprotocol/inspector
#   → Transport: SSE, URL: http://localhost:8000/sse

# Health check
curl http://localhost:8000/health
```

Example natural-language queries the system must handle: *"action movies from the 90s with high IMDB
ratings"*, *"critically acclaimed drama films with small budgets"*, *"animated family movies
distributed by Disney"*, *"sci-fi films directed by James Cameron"*, *"dark psychological thrillers
with low Rotten Tomatoes scores"*.

---

## 9. API Documentation

**Base URL:** `http://localhost:8080` · Swagger UI at `/swagger` · spec at `/openapi/v1.json` (also
exported to [openapi.json](openapi.json)). All `/api/v1/*` endpoints require a valid JWT (see
[Authentication](#10-authentication)). ⚠️ **PLACEHOLDER — confirm once [api/](api/) is implemented.**

### `GET /health` — liveness + readiness (no auth)

```bash
curl http://localhost:8080/health
```
```json
{ "status": "Healthy", "dependencies": { "mcp-server": "Healthy", "postgres": "Healthy" } }
```

### `GET /api/v1/movies/search` — natural-language search

Query params: `q` (required), `top_k` (default 10, max 50), `genre`, `min_imdb_rating`,
`mpaa_rating`, `decade`.

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
      "rt_rating": 93, "similarity_score": 0.86 },
    { "id": "3ab7…", "title": "Aliens", "release_year": 1986,
      "major_genre": "Action", "director": "James Cameron", "imdb_rating": 8.4,
      "similarity_score": 0.81 }
  ]
}
```

### `GET /api/v1/movies/{id}` — get movie by ID

```bash
curl http://localhost:8080/api/v1/movies/9f1c… -H "Authorization: Bearer $TOKEN"
```
```json
{ "id": "9f1c…", "title": "Terminator 2: Judgment Day", "release_year": 1991,
  "major_genre": "Action", "director": "James Cameron", "distributor": "TriStar",
  "mpaa_rating": "R", "imdb_rating": 8.5, "rt_rating": 93,
  "production_budget": 102000000, "running_time_min": 137, "budget_tier": "blockbuster",
  "decade": 1990 }
```

### `GET /api/v1/movies/{id}/similar` — similar movies

```bash
curl "http://localhost:8080/api/v1/movies/9f1c…/similar?top_k=5" -H "Authorization: Bearer $TOKEN"
```
```json
{ "source_id": "9f1c…", "results": [ { "id": "…", "title": "The Terminator", "similarity_score": 0.88 } ] }
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

---

## 10. Authentication

The API uses **JWT Bearer token** authentication. All `/api/v1/*` endpoints require a valid token.
Two roles: **`reader`** (search endpoints only) and **`admin`** (all endpoints, including
`/api/v1/stats`). ⚠️ **PLACEHOLDER — confirm once auth is implemented.**

### Obtain a token (client-credentials flow)

```bash
curl -X POST http://localhost:8080/auth/token \
  -H "Content-Type: application/json" \
  -d '{"client_id":"'"$CLIENT_ID"'","client_secret":"'"$CLIENT_SECRET"'"}'
```
```json
{ "access_token": "eyJhbGciOi…", "token_type": "Bearer", "expires_in": 3600, "role": "reader" }
```

### Use the token

```bash
TOKEN="eyJhbGciOi…"
curl "http://localhost:8080/api/v1/movies/search?q=heist" -H "Authorization: Bearer $TOKEN"
```

- The JWT signing key (`JWT_SIGNING_KEY`), issuer and audience are read from `.env` — never hardcoded
  or committed. In AWS these come from Secrets Manager.
- Tokens expire (`expires_in`); request a new one from `/auth/token` on `401 Unauthorized`.
- Calling an admin-only endpoint with a `reader` token returns `403 Forbidden`.

---

## 11. Observability

**Location:** [monitoring/](monitoring/). ⚠️ **PLACEHOLDER — configs to be committed.**

| Signal | Tooling | Where to find it |
|--------|---------|------------------|
| **Traces** | OpenTelemetry → **Jaeger** (local) / AWS X-Ray (prod). Context propagates **.NET API ↔ Python MCP server** | http://localhost:16686 |
| **Metrics** | OpenTelemetry → Prometheus, scraped from `/metrics` | http://localhost:9090; dashboards in **Grafana** http://localhost:3000 |
| **Logs** | **Serilog** (.NET) & JSON logging (Python) → console + file sink | `docker compose logs -f <service>`; CloudWatch in AWS |

A **Grafana dashboard** (`monitoring/grafana/dashboards/movie-search.json`) provides at minimum:
request rate & latency (p50/p95/p99), error rate, MCP tool-call latency, and active connections.
The API targets **p95 < 500ms** under normal load, with response caching for repeated queries and
rate limiting of **60 req/min per authenticated user**.

---

## 12. Terraform Deployment

**Location:** [terraform/](terraform/). ⚠️ **PLACEHOLDER — Terraform to be committed.** Target:
**AWS ECS Fargate** behind an **ALB (HTTPS/ACM)**, backed by **RDS PostgreSQL (pgvector)** in private
subnets, images in **ECR**, secrets in **Secrets Manager**, logs/traces in **CloudWatch/X-Ray**.

```
terraform/
├── modules/{networking,compute,rds,ecr,alb,iam,monitoring,secrets}/
├── environments/{dev,prod}/
├── main.tf · variables.tf · outputs.tf · README.md
```

Infrastructure guarantees: all secrets via Secrets Manager (no hardcoded credentials); compute tasks
use IAM roles (no access keys); RDS in private subnets only; ALB with HTTPS; CPU/memory auto-scaling;
VPC Flow Logs enabled; **remote state in S3 with DynamoDB locking**; every resource tagged
`Environment`, `Project`, `ManagedBy`.

### Step-by-step (dev)

```bash
# 0. Authenticate to the target AWS account
aws sso login --profile <PROFILE>

# 1. Build & push images to ECR (one-off bootstrap, or via CI)
#    ⚠️ PLACEHOLDER: aws ecr get-login-password | docker login … && docker compose build && docker push …

# 2. Initialise Terraform for the dev environment (S3 backend + DynamoDB lock)
cd terraform/environments/dev
terraform init

# 3. Review the plan
terraform plan

# 4. Apply
terraform apply

# 5. Run DB migrations + initial pipeline as a one-off Fargate task
#    ⚠️ PLACEHOLDER: document the task-run command and the ALB URL from `terraform output`
```

- Never commit real `*.tfvars` with secrets — they are git-ignored; pass Secrets Manager ARNs instead.
- Deployment targets the organisation's own AWS account only — do not use public/consumer hosting.
  Coordinate with the AI Engineering team before exposing the platform internally.

---

## 13. Running Tests

⚠️ **PLACEHOLDER — test suites to be committed; commands reflect the intended layout and CI.**

### Unit tests

```bash
cd pipeline   && uv run pytest        # Python — cleaning/imputation/augmentation logic
cd mcp-server && uv run pytest        # Python — tool logic, query building
cd api        && dotnet test          # .NET — xUnit unit + integration tests
```

### Integration tests

```bash
# Bring up the stack and exercise real endpoints against seeded data
docker compose up -d --build
docker compose run --rm pipeline
# then run the API/MCP integration suite (hits http://localhost:8080 / :8000)
cd api && dotnet test --filter Category=Integration
```

### Load tests

```bash
# k6 targeting the search endpoint (scripts/load_test.js) — validates p95 < 500ms
k6 run scripts/load_test.js -e BASE_URL=http://localhost:8080 -e TOKEN=$TOKEN
```

**Linting/type-checking (enforced in CI):** `ruff` + `mypy` for Python, `dotnet format --verify-no-changes`
for .NET. CI (`.github/workflows/ci.yml`) also builds all images, runs a Compose integration test, and
runs `terraform fmt/validate/plan`.

---

## 14. Known Limitations & Future Improvements

**Current limitations**

- 🚧 **Scaffolding stage** — orchestration ([docker-compose.yml](docker-compose.yml)) and this
  documentation exist, but the pipeline stages, MCP tools, .NET API, migrations, monitoring configs,
  and Terraform are **not yet implemented**. Sections marked ⚠️ are intended designs.
- No dataset load, migrations, or seed data are committed yet.
- No CI/CD workflows are defined ([.github/workflows/](.github/workflows/) is empty).
- The `embeddings` service pulls model weights on first start (cold start ~1–2 min); healthcheck
  `start_period` accounts for this.

**Future improvements**

- Implement each service + `Dockerfile` so `docker compose up --build` runs end to end on a clean machine.
- Hybrid search (vector similarity + full-text/metadata filters) with re-ranking; document a hybrid query.
- Tune the pgvector **HNSW** index (`m`, `ef_search`) and benchmark recall vs. latency at scale.
- Response caching TTL tuning and per-role rate limits.
- Evaluate larger/alternative embedding models and measure retrieval quality on a labelled query set.
- Complete the CI/CD pipelines (lint, test, build, Compose integration, `terraform apply` to dev→prod).
- Finish the Embedding Atlas bonus and document how to interpret the genre-coloured projection.

---

> Maintainers: ⚠️ **PLACEHOLDER** · License: ⚠️ **PLACEHOLDER**
