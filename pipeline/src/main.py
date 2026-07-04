"""Movie Search Platform data pipeline (README §5-6).

Clean -> impute -> augment -> embed (Ollama) -> load (pgvector), end to end.
Idempotent: movie ids are deterministic, so re-running upserts in place.

Configuration: see ``pipeline.settings.PipelineSettings`` — typed settings bound
from environment variables / ``.env`` (docker-compose supplies them).
"""

import json
import logging
from datetime import UTC, datetime
from pathlib import Path

import pandas as pd
from vega_datasets import data

from models import Movie
from pipeline.augmentation import augment
from pipeline.cleaning import clean
from pipeline.embedding import create_embeddings_provider
from pipeline.imputation import impute
from pipeline.loader import create_db_engine, movie_id, upsert_movies
from pipeline.settings import PipelineSettings

logger = logging.getLogger("pipeline")

LOGS_DIR = Path(__file__).resolve().parent.parent / "logs"


def main() -> None:
    settings = PipelineSettings()

    logging.basicConfig(
        level=settings.log_level.upper(),
        format="%(asctime)s %(levelname)s %(name)s: %(message)s",
    )

    # Transform: clean -> impute -> augment.
    raw = data.movies()
    logger.info("Loaded %d raw rows from the Vega movies dataset", len(raw))

    df, cleaning_report = clean(raw)
    df, imputation_report = impute(df)
    df = augment(df)
    _write_report({
        "generated_at": datetime.now(UTC).isoformat(),
        "pipeline_version": settings.pipeline_version,
        "cleaning": cleaning_report,
        "imputation": imputation_report,
    })
    logger.info("Transformed %d rows (%s)", len(df), cleaning_report)

    # Embed + load in batches. (Schema is applied beforehand by
    # `alembic upgrade head` — see the pipeline Dockerfile CMD.)
    engine = create_db_engine(settings.database_url)
    client = create_embeddings_provider(settings)

    written = 0
    try:
        for start in range(0, len(df), settings.batch_size):
            batch = df.iloc[start:start + settings.batch_size]
            embeddings = client.embed(batch["augmented_text"].tolist())
            movies = [
                _to_movie(row, embedding, settings.pipeline_version)
                for (_, row), embedding in zip(batch.iterrows(), embeddings, strict=True)
            ]
            written += upsert_movies(engine, movies)
            logger.info("Upserted %d/%d movies", written, len(df))
    finally:
        client.close()

    logger.info("Pipeline complete: %d movies embedded and loaded", written)


def _to_movie(row: pd.Series, embedding: list[float], pipeline_version: str) -> Movie:
    """Maps one transformed row onto the shared SQLModel entity."""
    def value(column: str):
        v = row[column]
        if pd.isna(v):
            return None
        # Unwrap numpy scalars (Int64/Float64 columns) — psycopg can't adapt them.
        return v.item() if hasattr(v, "item") else v

    now = datetime.now(UTC)
    return Movie(
        id=movie_id(row["title"], value("release_date")),
        title=row["title"],
        release_date=value("release_date"),
        release_year=value("release_year"),
        major_genre=row["major_genre"],
        director=row["director"],
        distributor=row["distributor"],
        mpaa_rating=row["mpaa_rating"],
        creative_type=row["creative_type"],
        source=row["source"],
        imdb_rating=value("imdb_rating"),
        imdb_votes=value("imdb_votes"),
        rotten_tomatoes_rating=value("rotten_tomatoes_rating"),
        production_budget=value("production_budget"),
        worldwide_gross=value("worldwide_gross"),
        running_time_min=value("running_time_min"),
        runtime_imputed=bool(row["runtime_imputed"]),
        budget_tier=value("budget_tier"),
        decade=value("decade"),
        blockbuster_flag=bool(row["blockbuster_flag"]),
        augmented_text=row["augmented_text"],
        embedding=embedding,
        created_at=now,
        updated_at=now,
        pipeline_version=pipeline_version,
    )


def _write_report(report: dict) -> None:
    """Persists the cleaning report (mounted to the host in docker-compose)."""
    LOGS_DIR.mkdir(parents=True, exist_ok=True)
    path = LOGS_DIR / "cleaning_report.json"
    path.write_text(json.dumps(report, indent=2))
    logger.info("Cleaning report written to %s", path)


if __name__ == "__main__":
    main()
