"""Stage 3 — derive features and build the embedding text (README §6).

Adds ``budget_tier`` (bucketised budget; NULL when budget unknown), ``decade``
(from the release year) and ``blockbuster_flag`` (high budget AND high gross),
then serialises each movie into the rich ``augmented_text`` block that gets
embedded. The block only mentions facts we actually have — imputed "Unknown"
categories and NULL numerics are omitted so the vector isn't polluted with
placeholder tokens.
"""

import pandas as pd

from pipeline.constants import (
    BLOCKBUSTER_BUDGET,
    BLOCKBUSTER_GROSS,
    BUDGET_TIERS,
    NOT_RATED,
    TOP_BUDGET_TIER,
    UNKNOWN,
)


def augment(df: pd.DataFrame) -> pd.DataFrame:
    """Adds derived columns and ``augmented_text``; returns a new frame."""
    df = df.copy()

    df["budget_tier"] = df["production_budget"].map(_budget_tier, na_action="ignore")
    df["decade"] = (df["release_year"] // 10 * 10).astype("Int64")
    df["blockbuster_flag"] = (
        (df["production_budget"] >= BLOCKBUSTER_BUDGET)
        & (df["worldwide_gross"] >= BLOCKBUSTER_GROSS)
    ).fillna(False)

    df["augmented_text"] = df.apply(_augmented_text, axis=1)
    return df


def _budget_tier(budget: int) -> str:
    for ceiling, tier in BUDGET_TIERS:
        if budget < ceiling:
            return tier
    return TOP_BUDGET_TIER


def _augmented_text(row: pd.Series) -> str:
    """Serialises one movie into the text block that gets embedded."""
    parts = [f"Title: {row['title']}."]

    if row["major_genre"] != UNKNOWN:
        parts.append(f"Genre: {row['major_genre']}.")
    if row["creative_type"] != UNKNOWN:
        parts.append(f"Creative type: {row['creative_type']}.")
    if row["source"] != UNKNOWN:
        parts.append(f"Source: {row['source']}.")
    if row["director"] != UNKNOWN:
        parts.append(f"Directed by {row['director']}.")
    if row["distributor"] != UNKNOWN:
        parts.append(f"Distributed by {row['distributor']}.")

    if pd.notna(row["release_year"]):
        parts.append(f"Released in {row['release_year']} ({row['decade']}s).")
    if row["mpaa_rating"] != NOT_RATED:
        parts.append(f"MPAA rating: {row['mpaa_rating']}.")
    if pd.notna(row["running_time_min"]) and not row["runtime_imputed"]:
        parts.append(f"Running time: {row['running_time_min']} minutes.")

    if pd.notna(row["imdb_rating"]):
        votes = f" from {row['imdb_votes']} votes" if pd.notna(row["imdb_votes"]) else ""
        parts.append(f"IMDB rating: {row['imdb_rating']}/10{votes}.")
    if pd.notna(row["rotten_tomatoes_rating"]):
        parts.append(f"Rotten Tomatoes score: {row['rotten_tomatoes_rating']}/100.")

    if pd.notna(row["budget_tier"]):
        parts.append(f"Budget tier: {row['budget_tier']}.")
    if row["blockbuster_flag"]:
        parts.append("A blockbuster with high budget and high worldwide gross.")

    return " ".join(parts)
