"""Stage 1 — clean the raw Vega movies dataset (README §5-6).

Renames the raw columns onto the shared schema (see ``models.Movie``), then:
drops rows without a title, trims strings, parses release dates (fixing the
dataset's two-digit-year quirk where e.g. '46 parses as 2046), coerces numeric
columns to nullable types, nulls out impossible values (negative money, ratings
outside their scales) rather than fabricating data, and de-duplicates on
(title, release_date). Returns the cleaned frame plus a report dict that feeds
the cleaning report written by ``main``.
"""

import pandas as pd

from pipeline.constants import DATASET_MAX_YEAR, RAW_TO_SCHEMA, STRING_COLUMNS


def clean(raw: pd.DataFrame) -> tuple[pd.DataFrame, dict]:
    """Cleans the raw dataset; returns ``(cleaned_frame, report)``."""
    report: dict = {"rows_in": int(len(raw))}

    df = raw.rename(columns=RAW_TO_SCHEMA)[list(RAW_TO_SCHEMA.values())].copy()

    # Strings: trim; empty -> NA (imputation decides the category later).
    for column in STRING_COLUMNS:
        df[column] = df[column].astype("string").str.strip().replace("", pd.NA)

    # Rows with no title are unusable (title anchors identity and search).
    missing_title = df["title"].isna()
    report["dropped_missing_title"] = int(missing_title.sum())
    df = df[~missing_title]

    # Release dates: parse leniently; the source encodes some two-digit years,
    # so e.g. '46 parses as 2046. The Vega movies dataset was compiled around
    # 2010, so any year beyond that is a wrapped 20th-century date and gets
    # 100 years subtracted.
    parsed = pd.to_datetime(df["release_date"], errors="coerce", format="mixed")
    report["unparseable_dates"] = int((parsed.isna() & df["release_date"].notna()).sum())
    wrapped = (parsed.dt.year > DATASET_MAX_YEAR).fillna(False)
    report["two_digit_year_fixes"] = int(wrapped.sum())
    parsed = parsed.mask(wrapped, parsed - pd.DateOffset(years=100))
    df["release_date"] = parsed.dt.date
    df["release_year"] = parsed.dt.year.astype("Int64")

    # Numerics: coerce to nullable dtypes; unknown stays NULL (never fabricated).
    for column in ("imdb_votes", "rotten_tomatoes_rating", "production_budget",
                   "worldwide_gross", "running_time_min"):
        df[column] = pd.to_numeric(df[column], errors="coerce").round().astype("Int64")
    df["imdb_rating"] = pd.to_numeric(df["imdb_rating"], errors="coerce").astype("Float64")

    # Impossible values -> NULL (README: negatives coerced to NULL; ratings stay truthful).
    for column, low, high in (
        ("production_budget", 0, None),
        ("worldwide_gross", 0, None),
        ("imdb_votes", 0, None),
        ("running_time_min", 1, None),
        ("imdb_rating", 0, 10),
        ("rotten_tomatoes_rating", 0, 100),
    ):
        invalid = (df[column] < low) if high is None else ~df[column].between(low, high)
        invalid = invalid.fillna(False)
        report[f"invalid_{column}_nulled"] = int(invalid.sum())
        df[column] = df[column].mask(invalid)

    # De-duplicate on the identity key the loader also uses for stable ids.
    duplicated = df.duplicated(subset=["title", "release_date"], keep="first")
    report["duplicates_removed"] = int(duplicated.sum())
    df = df[~duplicated].reset_index(drop=True)

    report["rows_out"] = int(len(df))
    return df, report
