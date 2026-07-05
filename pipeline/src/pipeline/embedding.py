"""Document-embedding backends behind a swappable strategy (spec §1.4 / §6.2).

The pipeline embeds the augmented movie text; the MCP server later embeds search
queries. Both MUST use the same model per environment or cosine similarity is
meaningless, so both select their backend from ``ENV``:

    ENV=local        -> TEI (the docker-compose ``embeddings`` container)
    ENV=dev|prod     -> Amazon Bedrock

``create_embeddings_provider`` returns an ``EmbeddingsProvider``; the pipeline
loop calls ``embed`` / ``close`` without knowing which backend it got. A
dimension mismatch fails fast because loading wrong-sized vectors would corrupt
search; transient failures are retried with backoff.
"""

import json
import logging
import time
from abc import ABC, abstractmethod

import httpx
from pydantic import TypeAdapter

from pipeline.constants import EMBED_BACKOFF_SECONDS, EMBED_RETRIES
from pipeline.schemas import TeiEmbedRequest
from pipeline.settings import PipelineSettings

logger = logging.getLogger(__name__)

# TEI's ``/embed`` returns a bare array of vectors; validate it at the boundary.
_TeiEmbedResponse = TypeAdapter(list[list[float]])


class EmbeddingsProvider(ABC):
    """A document-embedding backend. ``embed`` returns one vector per input, in order."""

    @abstractmethod
    def embed(self, texts: list[str]) -> list[list[float]]:
        """Embeds a batch of texts; raises on failure or dimension mismatch."""

    def close(self) -> None:  # noqa: B027 — optional hook; backends override only if needed.
        """Releases any held resources; no-op by default."""


class TeiEmbeddingsClient(EmbeddingsProvider):
    """Local backend: HuggingFace Text Embeddings Inference (TEI) ``/embed`` batch
    endpoint (the docker-compose ``embeddings`` container).

    ``model`` is not sent to TEI (it serves a single model) — it is retained only
    to label dimension-mismatch errors.
    """

    def __init__(self, base_url: str, model: str, expected_dim: int, timeout: float = 300.0):
        self._client = httpx.Client(base_url=base_url, timeout=timeout)
        self._model = model
        self._expected_dim = expected_dim

    def embed(self, texts: list[str]) -> list[list[float]]:
        request = TeiEmbedRequest(inputs=texts)
        vectors = _TeiEmbedResponse.validate_json(self._post(request).content)

        if len(vectors) != len(texts):
            raise ValueError(
                f"TEI returned {len(vectors)} embeddings for {len(texts)} inputs"
            )
        for vector in vectors:
            _check_dim(vector, self._expected_dim, self._model)
        return vectors

    def _post(self, request: TeiEmbedRequest) -> httpx.Response:
        payload = request.model_dump()
        for attempt in range(1, EMBED_RETRIES + 1):
            try:
                response = self._client.post("/embed", json=payload)
                response.raise_for_status()
                return response
            except (httpx.HTTPStatusError, httpx.TransportError) as error:
                if attempt == EMBED_RETRIES:
                    raise
                delay = EMBED_BACKOFF_SECONDS * attempt
                logger.warning(
                    "TEI embed attempt %d/%d failed (%s); retrying in %.0fs",
                    attempt, EMBED_RETRIES, error, delay,
                )
                time.sleep(delay)
        raise RuntimeError("unreachable")

    def close(self) -> None:
        self._client.close()


class BedrockEmbeddingsClient(EmbeddingsProvider):
    """dev/prod backend: Amazon Bedrock embeddings (Titan Text Embeddings V2 by
    default). Titan embeds one input per call, so a batch is a loop; credentials
    come from the task role on ECS, never from config.
    """

    def __init__(self, region: str, model_id: str, expected_dim: int):
        # Lazy import so the local (TEI) path needs neither boto3 nor AWS creds.
        import boto3
        from botocore.config import Config

        self._client = boto3.client(
            "bedrock-runtime",
            region_name=region,
            config=Config(retries={"max_attempts": EMBED_RETRIES, "mode": "standard"}),
        )
        self._model_id = model_id
        self._expected_dim = expected_dim

    def embed(self, texts: list[str]) -> list[list[float]]:
        return [self._embed_one(text) for text in texts]

    def _embed_one(self, text: str) -> list[float]:
        body = {"inputText": text, "dimensions": self._expected_dim, "normalize": True}
        response = self._client.invoke_model(
            modelId=self._model_id,
            accept="application/json",
            contentType="application/json",
            body=json.dumps(body),
        )
        vector = json.loads(response["body"].read())["embedding"]
        _check_dim(vector, self._expected_dim, self._model_id)
        return vector


def resolve_provider_name(settings: PipelineSettings) -> str:
    """Maps the environment (or explicit override) to a backend name."""
    if settings.embedding_provider != "auto":
        return settings.embedding_provider
    return "tei" if settings.env == "local" else "bedrock"


def create_embeddings_provider(settings: PipelineSettings) -> EmbeddingsProvider:
    """Builds the document-embedding backend for the current environment."""
    provider = resolve_provider_name(settings)
    logger.info(
        "Embeddings backend: %s (environment=%s)", provider, settings.env
    )

    if provider == "tei":
        return TeiEmbeddingsClient(
            settings.embeddings_url, settings.embedding_model, settings.embedding_dim
        )
    if provider == "bedrock":
        return BedrockEmbeddingsClient(
            settings.bedrock_region, settings.bedrock_embedding_model_id, settings.embedding_dim
        )
    raise ValueError(f"Unknown embedding provider '{provider}'")


def _check_dim(vector: list[float], expected: int, model: str) -> None:
    if len(vector) != expected:
        raise ValueError(
            f"Embedding dimension {len(vector)} != expected {expected}; "
            f"model '{model}' does not match the vector({expected}) column"
        )
