"""Tool behaviour through the in-memory MCP client (schemas + errors included)."""

import pytest
from fastmcp import Client
from fastmcp.exceptions import ToolError

from conftest import KNOWN_ID, UNEMBEDDED_ID, UNKNOWN_ID, payload
from server.constants import MAX_TOP_K


async def call(mcp, tool: str, arguments: dict | None = None):
    async with Client(mcp) as client:
        return await client.call_tool(tool, arguments or {})


async def test_search_returns_ranked_results_with_similarity(mcp, fake_embeddings):
    result = await call(mcp, "search_movies_by_description", {"query": "heist thrillers"})

    hits = payload(result)
    assert [hit["title"] for hit in hits] == ["Heat", "Ronin"]
    assert hits[0]["similarity_score"] == 0.9134  # rounded to 4 dp
    assert fake_embeddings.embedded == ["heist thrillers"]  # server-side embedding


async def test_search_passes_filters_through_to_the_database(mcp, fake_db):
    await call(mcp, "search_movies_by_description", {
        "query": "anything",
        "genre_filter": "Drama",
        "min_imdb_rating": 7.0,
        "mpaa_rating": "R",
        "decade": 1990,
    })

    assert fake_db.search_kwargs["genre"] == "Drama"
    assert fake_db.search_kwargs["min_imdb_rating"] == 7.0
    assert fake_db.search_kwargs["mpaa_rating"] == "R"
    assert fake_db.search_kwargs["decade"] == 1990


async def test_search_clamps_top_k_to_the_contract(mcp, fake_db):
    await call(mcp, "search_movies_by_description", {"query": "q", "top_k": 9999})
    assert fake_db.search_kwargs["top_k"] == MAX_TOP_K

    await call(mcp, "search_movies_by_description", {"query": "q", "top_k": -3})
    assert fake_db.search_kwargs["top_k"] == 1


async def test_get_movie_by_id_returns_the_movie(mcp):
    result = await call(mcp, "get_movie_by_id", {"movie_id": str(KNOWN_ID)})

    movie = payload(result)
    assert movie["title"] == "Heat"
    assert movie["production_budget"] == 60_000_000
    assert movie["similarity_score"] is None  # direct lookups carry no score


async def test_get_movie_by_id_returns_null_for_an_unknown_id(mcp):
    result = await call(mcp, "get_movie_by_id", {"movie_id": str(UNKNOWN_ID)})
    assert payload(result) is None


async def test_get_movie_by_id_rejects_a_malformed_uuid(mcp):
    with pytest.raises(ToolError, match="not a valid movie id"):
        await call(mcp, "get_movie_by_id", {"movie_id": "not-a-uuid"})


async def test_get_movie_by_title_matches_case_insensitively(mcp):
    result = await call(mcp, "get_movie_by_title", {"title": "  HEAT "})
    assert payload(result)["title"] == "Heat"


async def test_get_movie_by_title_returns_null_when_nothing_matches(mcp):
    result = await call(mcp, "get_movie_by_title", {"title": "No Such Film"})
    assert payload(result) is None


async def test_get_similar_movies_returns_neighbours(mcp):
    result = await call(mcp, "get_similar_movies", {"movie_id": str(KNOWN_ID), "top_k": 3})

    hits = payload(result)
    assert [hit["title"] for hit in hits] == ["Ronin"]
    assert hits[0]["similarity_score"] == 0.8321


async def test_get_similar_movies_errors_for_an_unknown_movie(mcp):
    # The .NET API maps this exact error text to a 404 — it is part of the contract.
    with pytest.raises(ToolError, match="does not exist"):
        await call(mcp, "get_similar_movies", {"movie_id": str(UNKNOWN_ID)})


async def test_get_similar_movies_is_empty_for_an_unembedded_movie(mcp):
    result = await call(mcp, "get_similar_movies", {"movie_id": str(UNEMBEDDED_ID)})
    assert payload(result) == []


async def test_list_genres_returns_the_distinct_genres(mcp):
    result = await call(mcp, "list_genres")
    assert payload(result) == ["Action", "Drama"]


async def test_get_dataset_stats_maps_the_aggregate_row(mcp):
    result = await call(mcp, "get_dataset_stats")

    stats = payload(result)
    assert stats["total_movies"] == 3201
    assert stats["with_embeddings"] == 3200
    assert stats["genres"] == 12
    assert stats["year_range"] == [1920, 2010]
    assert stats["avg_imdb_rating"] == 6.28  # rounded to 2 dp
    assert stats["pipeline_version"] == "0.1.0"


async def test_get_dataset_stats_handles_an_empty_catalogue(mcp, fake_db):
    fake_db.stats_row = {
        "total_movies": 0,
        "with_embeddings": 0,
        "genres": 0,
        "min_year": None,
        "max_year": None,
        "avg_imdb_rating": None,
        "pipeline_version": None,
    }

    stats = payload(await call(mcp, "get_dataset_stats"))

    assert stats["year_range"] == []
    assert stats["avg_imdb_rating"] is None
