"""Cleaning stage (README §5-6): schema rename, date quirks, impossible values, dedup."""

import pandas as pd

from conftest import raw_frame
from pipeline.cleaning import clean


def test_clean_maps_raw_columns_onto_the_shared_schema(raw_movie):
    df, report = clean(raw_frame([raw_movie]))

    assert list(df["title"]) == ["Heat"]
    assert list(df["major_genre"]) == ["Drama"]
    assert list(df["release_year"]) == [1995]
    assert report["rows_in"] == 1
    assert report["rows_out"] == 1


def test_clean_drops_rows_without_a_title(raw_movie):
    untitled = dict(raw_movie, Title=None)
    blank = dict(raw_movie, Title="   ")

    df, report = clean(raw_frame([raw_movie, untitled, blank]))

    assert list(df["title"]) == ["Heat"]
    assert report["dropped_missing_title"] == 2


def test_clean_trims_strings_and_treats_empty_as_missing(raw_movie):
    row = dict(raw_movie, Director="  Michael Mann  ", Distributor="")

    df, _ = clean(raw_frame([row]))

    assert df.loc[0, "director"] == "Michael Mann"
    assert pd.isna(df.loc[0, "distributor"])


def test_clean_fixes_the_two_digit_year_wrap(raw_movie):
    # The Vega dataset encodes e.g. 1946 as '46, which pandas parses as 2046;
    # anything after the dataset's last year (2011) must lose 100 years.
    wrapped = dict(raw_movie, Title="Notorious", Release_Date="Aug 15 2046")

    df, report = clean(raw_frame([wrapped]))

    assert df.loc[0, "release_year"] == 1946
    assert report["two_digit_year_fixes"] == 1


def test_clean_nulls_impossible_values_instead_of_fabricating(raw_movie):
    row = dict(
        raw_movie,
        Production_Budget=-5,
        IMDB_Rating=11.0,
        Rotten_Tomatoes_Rating=150,
        Running_Time_min=0,
    )

    df, report = clean(raw_frame([row]))

    assert pd.isna(df.loc[0, "production_budget"])
    assert pd.isna(df.loc[0, "imdb_rating"])
    assert pd.isna(df.loc[0, "rotten_tomatoes_rating"])
    assert pd.isna(df.loc[0, "running_time_min"])
    assert report["invalid_production_budget_nulled"] == 1
    assert report["invalid_imdb_rating_nulled"] == 1


def test_clean_keeps_unknown_numerics_null(raw_movie):
    row = dict(raw_movie, IMDB_Rating=None, Production_Budget=None)

    df, _ = clean(raw_frame([row]))

    assert pd.isna(df.loc[0, "imdb_rating"])
    assert pd.isna(df.loc[0, "production_budget"])


def test_clean_deduplicates_on_title_and_release_date(raw_movie):
    same_movie = dict(raw_movie, IMDB_Rating=7.0)  # different metadata, same identity
    other_release = dict(raw_movie, Release_Date="Jan 01 2000")

    df, report = clean(raw_frame([raw_movie, same_movie, other_release]))

    assert len(df) == 2
    assert report["duplicates_removed"] == 1
    # First occurrence wins.
    assert df.loc[0, "imdb_rating"] == 8.3


def test_clean_reports_unparseable_dates(raw_movie):
    row = dict(raw_movie, Release_Date="not a date")

    df, report = clean(raw_frame([row]))

    assert report["unparseable_dates"] == 1
    assert pd.isna(df.loc[0, "release_year"])
