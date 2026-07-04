"""Shared fixtures: tiny raw frames shaped like the Vega movies dataset."""

import pandas as pd
import pytest

from pipeline.constants import RAW_TO_SCHEMA

RAW_COLUMNS = list(RAW_TO_SCHEMA.keys())


def raw_frame(rows: list[dict]) -> pd.DataFrame:
    """Builds a raw-shaped frame; unspecified columns default to None."""
    return pd.DataFrame([{column: row.get(column) for column in RAW_COLUMNS} for row in rows])


@pytest.fixture
def raw_movie() -> dict:
    """One fully-populated raw row (Vega column names)."""
    return {
        "Title": "Heat",
        "Release_Date": "Dec 15 1995",
        "Major_Genre": "Drama",
        "Director": "Michael Mann",
        "Distributor": "Warner Bros.",
        "MPAA_Rating": "R",
        "Creative_Type": "Contemporary Fiction",
        "Source": "Original Screenplay",
        "IMDB_Rating": 8.3,
        "IMDB_Votes": 400_000,
        "Rotten_Tomatoes_Rating": 88,
        "Production_Budget": 60_000_000,
        "Worldwide_Gross": 187_000_000,
        "Running_Time_min": 170,
    }
