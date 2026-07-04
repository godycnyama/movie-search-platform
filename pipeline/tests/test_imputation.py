"""Imputation stage (README §6): real categories for categoricals, genre-median runtimes."""

import pandas as pd

from pipeline.constants import NOT_RATED, UNKNOWN
from pipeline.imputation import impute


def cleaned_frame(rows: list[dict]) -> pd.DataFrame:
    defaults = {
        "title": "A Movie",
        "major_genre": "Drama",
        "director": "Someone",
        "distributor": "Studio",
        "mpaa_rating": "PG",
        "creative_type": "Fiction",
        "source": "Original",
        "running_time_min": None,
    }
    df = pd.DataFrame([{**defaults, **row} for row in rows])
    df["running_time_min"] = df["running_time_min"].astype("Int64")
    return df


def test_impute_fills_categoricals_with_real_categories():
    df, report = impute(
        cleaned_frame([{"major_genre": None, "director": None, "mpaa_rating": None}])
    )

    assert df.loc[0, "major_genre"] == UNKNOWN
    assert df.loc[0, "director"] == UNKNOWN
    assert df.loc[0, "mpaa_rating"] == NOT_RATED
    assert report["imputed_major_genre"] == 1
    assert report["imputed_mpaa_rating"] == 1


def test_impute_uses_the_genre_median_for_missing_runtimes():
    df, report = impute(cleaned_frame([
        {"major_genre": "Drama", "running_time_min": 100},
        {"major_genre": "Drama", "running_time_min": 120},
        {"major_genre": "Drama", "running_time_min": None},
        {"major_genre": "Comedy", "running_time_min": 90},
    ]))

    imputed = df[df["runtime_imputed"]]
    assert len(imputed) == 1
    assert imputed.iloc[0]["running_time_min"] == 110  # Drama median, not the global one
    assert report["imputed_running_time_min"] == 1


def test_impute_falls_back_to_the_global_median_when_the_genre_has_no_runtimes():
    df, _ = impute(cleaned_frame([
        {"major_genre": "Drama", "running_time_min": 100},
        {"major_genre": "Drama", "running_time_min": 120},
        {"major_genre": "Western", "running_time_min": None},
    ]))

    western = df[df["major_genre"] == "Western"].iloc[0]
    assert western["runtime_imputed"]
    assert western["running_time_min"] == 110


def test_impute_flags_only_imputed_runtimes():
    df, _ = impute(cleaned_frame([
        {"running_time_min": 95},
        {"running_time_min": None},
    ]))

    assert list(df["runtime_imputed"]) == [False, True]
    assert df.loc[0, "running_time_min"] == 95  # real values untouched
