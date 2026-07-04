"""Ollama query-embedding client: contract validation and the dimension guard."""

import httpx
import pytest

from server.embeddings import OllamaEmbeddingsClient

DIM = 4


def make_client(handler) -> OllamaEmbeddingsClient:
    client = OllamaEmbeddingsClient("http://ollama.test", "nomic-embed-text", DIM)
    client._client = httpx.AsyncClient(
        base_url="http://ollama.test", transport=httpx.MockTransport(handler)
    )
    return client


async def test_embed_query_returns_the_vector():
    def handler(request: httpx.Request) -> httpx.Response:
        assert request.url.path == "/api/embed"
        return httpx.Response(200, json={"embeddings": [[0.1] * DIM]})

    assert await make_client(handler).embed_query("heist movies") == [0.1] * DIM


async def test_embed_query_rejects_a_dimension_mismatch():
    client = make_client(lambda _: httpx.Response(200, json={"embeddings": [[0.1, 0.2]]}))

    with pytest.raises(ValueError, match="dimension"):
        await client.embed_query("query")


async def test_embed_query_raises_on_http_errors():
    client = make_client(lambda _: httpx.Response(500, text="model not loaded"))

    with pytest.raises(httpx.HTTPStatusError):
        await client.embed_query("query")
