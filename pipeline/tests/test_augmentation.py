"""Augmentation stage (README §6): derived features and the embedding text block."""

import pandas as pd

from pipeline.augmentation import augment


def frame(rows: list[dict]) -> pd.DataFrame:
    defaults = {
        "title": "A Movie",
        "major_genre": "Drama",
        "director": "Someone",
        "distributor": "Studio",
        "mpaa_rating": "PG",
        "creative_type": "Fiction",
        "source": "Original",
        "release_year": 1995,
        "imdb_rating": 7.5,
        "imdb_votes": 1000,
        "rotten_tomatoes_rating": 80,
        "production_budget": 20_000_000,
        "worldwide_gross": 50_000_000,
        "running_time_min": 110,
        "runtime_imputed": False,
    }
    df = pd.DataFrame([{**defaults, **row} for row in rows])
    for column in ("release_year", "imdb_votes", "rotten_tomatoes_rating",
                   "production_budget", "worldwide_gross", "running_time_min"):
        df[column] = df[column].astype("Int64")
    return df


def test_augment_buckets_budgets_into_tiers():
    df = augment(frame([
        {"production_budget": 9_999_999},
        {"production_budget": 10_000_000},
        {"production_budget": 50_000_000},
        {"production_budget": 100_000_000},
        {"production_budget": None},
    ]))

    assert list(df["budget_tier"][:4]) == ["low", "mid", "high", "blockbuster"]
    assert pd.isna(df.loc[4, "budget_tier"])  # unknown budget is never guessed


def test_augment_derives_the_decade_from_the_release_year():
    df = augment(frame([{"release_year": 1997}, {"release_year": None}]))

    assert df.loc[0, "decade"] == 1990
    assert pd.isna(df.loc[1, "decade"])


def test_augment_flags_blockbusters_only_when_budget_and_gross_are_both_high():
    df = augment(frame([
        {"production_budget": 150_000_000, "worldwide_gross": 500_000_000},
        {"production_budget": 150_000_000, "worldwide_gross": 50_000_000},
        {"production_budget": None, "worldwide_gross": 500_000_000},
    ]))

    assert list(df["blockbuster_flag"]) == [True, False, False]


def test_augmented_text_mentions_the_facts_we_have():
    df = augment(frame([{"title": "Heat", "director": "Michael Mann"}]))
    text = df.loc[0, "augmented_text"]

    assert text.startswith("Title: Heat.")
    assert "Genre: Drama." in text
    assert "Directed by Michael Mann." in text
    assert "Released in 1995 (1990s)." in text
    assert "IMDB rating: 7.5/10 from 1000 votes." in text


def test_augmented_text_omits_placeholders_and_imputed_values():
    df = augment(frame([{
        "major_genre": "Unknown",
        "director": "Unknown",
        "mpaa_rating": "Not Rated",
        "runtime_imputed": True,
        "imdb_rating": None,
        "rotten_tomatoes_rating": None,
        "production_budget": None,
    }]))
    text = df.loc[0, "augmented_text"]

    assert "Unknown" not in text
    assert "Not Rated" not in text
    assert "Running time" not in text  # imputed runtime must not pollute the vector
    assert "IMDB" not in text
    assert "Budget tier" not in text
