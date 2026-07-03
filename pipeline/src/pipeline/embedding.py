"""Stage 4 — embed augmented text via the Ollama server (README §5-6).

Calls Ollama's batch endpoint ``POST /api/embed`` with the configured model
(``nomic-embed-text``, 768-dim — must match the pgvector ``vector(768)``
column). Requests and responses go through the Pydantic contracts in
``pipeline.schemas``, so a malformed reply fails with a precise validation
error. Transient failures are retried with backoff; a dimension mismatch
fails fast because loading wrong-sized vectors would corrupt search.
"""

import logging
import time

import httpx

from pipeline.constants import EMBED_BACKOFF_SECONDS, EMBED_RETRIES
from pipeline.schemas import OllamaEmbedRequest, OllamaEmbedResponse

logger = logging.getLogger(__name__)


class OllamaEmbeddingsClient:
    """Thin client for Ollama's ``/api/embed`` batch endpoint."""

    def __init__(self, base_url: str, model: str, expected_dim: int, timeout: float = 300.0):
        self._client = httpx.Client(base_url=base_url, timeout=timeout)
        self._model = model
        self._expected_dim = expected_dim

    def embed(self, texts: list[str]) -> list[list[float]]:
        """Embeds a batch of texts; returns one vector per text, in order."""
        request = OllamaEmbedRequest(model=self._model, input=texts)
        response = OllamaEmbedResponse.model_validate_json(self._post(request).content)

        if len(response.embeddings) != len(texts):
            raise ValueError(
                f"Ollama returned {len(response.embeddings)} embeddings for {len(texts)} inputs"
            )
        for vector in response.embeddings:
            if len(vector) != self._expected_dim:
                raise ValueError(
                    f"Embedding dimension {len(vector)} != expected {self._expected_dim}; "
                    f"model '{self._model}' does not match the vector({self._expected_dim}) column"
                )
        return response.embeddings

    def _post(self, request: OllamaEmbedRequest) -> httpx.Response:
        payload = request.model_dump()
        for attempt in range(1, EMBED_RETRIES + 1):
            try:
                response = self._client.post("/api/embed", json=payload)
                response.raise_for_status()
                return response
            except (httpx.HTTPStatusError, httpx.TransportError) as error:
                if attempt == EMBED_RETRIES:
                    raise
                delay = EMBED_BACKOFF_SECONDS * attempt
                logger.warning(
                    "Ollama embed attempt %d/%d failed (%s); retrying in %.0fs",
                    attempt, EMBED_RETRIES, error, delay,
                )
                time.sleep(delay)
        raise RuntimeError("unreachable")

    def close(self) -> None:
        self._client.close()
