"""Loader identity: deterministic UUIDv5 ids make the upsert idempotent."""

from datetime import date

from pipeline.loader import movie_id


def test_movie_id_is_stable_across_runs():
    assert movie_id("Heat", date(1995, 12, 15)) == movie_id("Heat", date(1995, 12, 15))


def test_movie_id_distinguishes_titles_and_release_dates():
    heat_1995 = movie_id("Heat", date(1995, 12, 15))

    assert movie_id("Heat", date(1972, 1, 1)) != heat_1995
    assert movie_id("Ronin", date(1995, 12, 15)) != heat_1995


def test_movie_id_handles_a_missing_release_date():
    assert movie_id("Heat", None) == movie_id("Heat", None)
    assert movie_id("Heat", None) != movie_id("Heat", date(1995, 12, 15))
