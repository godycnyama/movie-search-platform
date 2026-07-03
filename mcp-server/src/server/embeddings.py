"""Async Ollama embeddings client for search queries.

Queries must be embedded with the SAME model the pipeline used for the
catalogue (nomic-embed-text, 768-dim) or cosine similarity is meaningless.
Requests and responses go through Pydantic contracts so a malformed reply
fails with a precise validation error at the boundary.
"""

import logging

import httpx
from pydantic import BaseModel, ConfigDict

logger = logging.getLogger(__name__)


class OllamaEmbedRequest(BaseModel):
    """Body for Ollama's batch ``POST /api/embed``."""

    model: str
    input: list[str]


class OllamaEmbedResponse(BaseModel):
    """Response from ``POST /api/embed`` — one vector per input, in order."""

    # Ollama adds timing/telemetry fields freely; only validate what we use.
    model_config = ConfigDict(extra="ignore")

    embeddings: list[list[float]]


class OllamaEmbeddingsClient:
    """Thin async client for Ollama's ``/api/embed`` endpoint."""

    def __init__(self, base_url: str, model: str, expected_dim: int, timeout: float = 60.0):
        self._client = httpx.AsyncClient(base_url=base_url, timeout=timeout)
        self._model = model
        self._expected_dim = expected_dim

    async def embed_query(self, text: str) -> list[float]:
        """Embeds one search query; raises on transport errors or dimension mismatch."""
        request = OllamaEmbedRequest(model=self._model, input=[text])
        response = await self._client.post("/api/embed", json=request.model_dump())
        response.raise_for_status()

        parsed = OllamaEmbedResponse.model_validate_json(response.content)
        [vector] = parsed.embeddings
        if len(vector) != self._expected_dim:
            raise ValueError(
                f"Embedding dimension {len(vector)} != expected {self._expected_dim}; "
                f"model '{self._model}' does not match the vector({self._expected_dim}) column"
            )
        return vector

    async def aclose(self) -> None:
        await self._client.aclose()
