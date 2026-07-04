"""MovieResult mapping from database records (shape shared with the .NET client)."""

from conftest import KNOWN_ID, movie_record
from server.models import MovieResult


def test_from_record_maps_every_exposed_column():
    result = MovieResult.from_record(movie_record())

    assert result.id == KNOWN_ID
    assert result.title == "Heat"
    assert result.release_year == 1995
    assert result.major_genre == "Drama"
    assert result.director == "Michael Mann"
    assert result.distributor == "Warner Bros."
    assert result.mpaa_rating == "R"
    assert result.imdb_rating == 8.3
    assert result.rotten_tomatoes_rating == 88
    assert result.production_budget == 60_000_000
    assert result.running_time_min == 170
    assert result.budget_tier == "high"
    assert result.decade == 1990


def test_from_record_rounds_similarity_to_four_decimals():
    assert MovieResult.from_record(movie_record(), similarity=0.913449).similarity_score == 0.9134


def test_from_record_leaves_similarity_null_for_direct_lookups():
    assert MovieResult.from_record(movie_record()).similarity_score is None
