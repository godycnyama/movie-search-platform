"""Export the embedded movie catalogue for Embedding Atlas (README §4, bonus).

Reads every embedded movie from pgvector and writes a Parquet file whose
``embedding`` column holds the raw 768-dim vectors. The atlas container then
serves it with the ``embedding-atlas`` CLI, which computes the 2-D UMAP
projection from those vectors:

    python export_embeddings_atlas.py [output.parquet]
    embedding-atlas output.parquet --text augmented_text --vector embedding

Configuration: ``DATABASE_URL`` (postgresql://user:pass@host:5432/movies).
"""

import os
import sys

import pandas as pd
import psycopg

# ``embedding::real[]`` (pgvector >= 0.7 cast) arrives as a plain Python list,
# so no pgvector adapter is needed on this side.
QUERY = """
    SELECT
        id::text                AS id,
        title,
        major_genre,
        creative_type,
        source,
        director,
        distributor,
        mpaa_rating,
        release_year,
        decade,
        imdb_rating,
        rotten_tomatoes_rating,
        budget_tier,
        augmented_text,
        embedding::real[]       AS embedding
    FROM movies
    WHERE embedding IS NOT NULL
    ORDER BY title
"""


def main() -> None:
    database_url = os.environ.get("DATABASE_URL")
    if not database_url:
        sys.exit("DATABASE_URL is not set (postgresql://user:pass@host:5432/movies)")

    output = sys.argv[1] if len(sys.argv) > 1 else "movies.parquet"

    with psycopg.connect(database_url) as connection, connection.cursor() as cursor:
        cursor.execute(QUERY)
        columns = [description.name for description in cursor.description]
        df = pd.DataFrame(cursor.fetchall(), columns=columns)

    if df.empty:
        sys.exit("No embedded movies found — run the pipeline first (docker compose run --rm pipeline)")

    df.to_parquet(output, index=False)
    print(f"Exported {len(df)} embedded movies to {output}")


if __name__ == "__main__":
    main()
