"""Stage 2 — impute missing values (README §6 imputation policy).

Categorical fields get a *real* category ("Unknown" / "Not Rated") so filters
and grouping stay meaningful. Runtime is median-imputed within the movie's
major genre (flagged via ``runtime_imputed``). Fields that feed ranking and
filtering — ratings, votes, budget, gross — are deliberately left NULL rather
than fabricated; that policy lives in the cleaning stage and the .NET entity
docs, not here.
"""

import pandas as pd

from pipeline.constants import NOT_RATED, UNKNOWN, UNKNOWN_IMPUTED_COLUMNS


def impute(df: pd.DataFrame) -> tuple[pd.DataFrame, dict]:
    """Fills categorical gaps and genre-median runtimes; returns ``(frame, report)``."""
    df = df.copy()
    report: dict = {}

    for column in UNKNOWN_IMPUTED_COLUMNS:
        report[f"imputed_{column}"] = int(df[column].isna().sum())
        df[column] = df[column].fillna(UNKNOWN)

    report["imputed_mpaa_rating"] = int(df["mpaa_rating"].isna().sum())
    df["mpaa_rating"] = df["mpaa_rating"].fillna(NOT_RATED)

    # Runtime: median within major_genre (fallback: global median), flagged so
    # consumers can exclude imputed values from statistics.
    missing_runtime = df["running_time_min"].isna()
    df["runtime_imputed"] = missing_runtime
    genre_median = df.groupby("major_genre")["running_time_min"].transform("median")
    global_median = df["running_time_min"].median()
    fill = genre_median.fillna(global_median).round().astype("Int64")
    df["running_time_min"] = df["running_time_min"].fillna(fill)
    report["imputed_running_time_min"] = int(missing_runtime.sum())

    return df, report
