"""TEI embeddings client: contracts, dimension guard, retry with backoff."""

import httpx
import pytest

from pipeline.constants import EMBED_RETRIES
from pipeline.embedding import TeiEmbeddingsClient

DIM = 4


def make_client(handler) -> TeiEmbeddingsClient:
    """Client whose HTTP layer is replaced with an in-memory mock transport."""
    client = TeiEmbeddingsClient("http://embeddings.test", "nomic-embed-text-v1.5", DIM)
    client._client = httpx.Client(
        base_url="http://embeddings.test", transport=httpx.MockTransport(handler)
    )
    return client


@pytest.fixture(autouse=True)
def no_sleep(monkeypatch):
    """Retry backoff must not slow the suite down."""
    monkeypatch.setattr("pipeline.embedding.time.sleep", lambda _: None)


def test_embed_returns_one_vector_per_text_in_order():
    def handler(request: httpx.Request) -> httpx.Response:
        assert request.url.path == "/embed"
        return httpx.Response(200, json=[[0.1] * DIM, [0.2] * DIM])

    vectors = make_client(handler).embed(["first", "second"])

    assert vectors == [[0.1] * DIM, [0.2] * DIM]


def test_embed_rejects_a_count_mismatch():
    client = make_client(lambda _: httpx.Response(200, json=[[0.1] * DIM]))

    with pytest.raises(ValueError, match="1 embeddings for 2 inputs"):
        client.embed(["first", "second"])


def test_embed_rejects_a_dimension_mismatch():
    client = make_client(lambda _: httpx.Response(200, json=[[0.1, 0.2]]))

    with pytest.raises(ValueError, match="dimension"):
        client.embed(["only"])


def test_embed_retries_transient_failures_then_succeeds():
    calls = {"count": 0}

    def flaky(request: httpx.Request) -> httpx.Response:
        calls["count"] += 1
        if calls["count"] < EMBED_RETRIES:
            return httpx.Response(503, text="busy")
        return httpx.Response(200, json=[[0.5] * DIM])

    vectors = make_client(flaky).embed(["text"])

    assert vectors == [[0.5] * DIM]
    assert calls["count"] == EMBED_RETRIES


def test_embed_gives_up_after_the_configured_retries():
    calls = {"count": 0}

    def always_down(request: httpx.Request) -> httpx.Response:
        calls["count"] += 1
        return httpx.Response(500, text="down")

    with pytest.raises(httpx.HTTPStatusError):
        make_client(always_down).embed(["text"])

    assert calls["count"] == EMBED_RETRIES
