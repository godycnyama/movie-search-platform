"""Query-embedding backends behind a swappable strategy (spec §1.4 / §6.2).

Queries must be embedded with the SAME model the pipeline used for the
catalogue, or cosine similarity is meaningless. Which backend that is depends on
the environment:

    ENV=local        -> TEI (the docker-compose ``embeddings`` container)
    ENV=dev|prod     -> Amazon Bedrock

``create_embeddings_provider`` selects the backend from ``Settings``; both
implement the ``EmbeddingsProvider`` protocol so the tools never know which one
they are talking to. Requests/responses go through Pydantic contracts (TEI) or
the Bedrock JSON schema, and a dimension mismatch fails fast — loading or
querying wrong-sized vectors would silently corrupt search.
"""

import asyncio
import json
import logging
from typing import Protocol, runtime_checkable

import httpx
from pydantic import BaseModel, TypeAdapter

from config import Settings

logger = logging.getLogger(__name__)

# TEI's ``/embed`` returns a bare array of vectors; validate it at the boundary.
_TeiEmbedResponse = TypeAdapter(list[list[float]])


@runtime_checkable
class EmbeddingsProvider(Protocol):
    """A query-embedding backend. Implementations must return a vector of the
    expected dimensionality or raise."""

    async def embed_query(self, text: str) -> list[float]:
        """Embeds one search query; raises on transport errors or dimension mismatch."""
        ...

    async def aclose(self) -> None:
        """Releases any held resources (HTTP connections, SDK clients)."""
        ...


class TeiEmbedRequest(BaseModel):
    """Body for TEI's batch ``POST /embed`` (HuggingFace Text Embeddings Inference).

    TEI serves a single model, so the model name is not part of the request.
    ``normalize`` returns unit vectors (cosine-ready); ``truncate`` guards against
    inputs longer than the model's max sequence length. The response is a bare
    ``list[list[float]]`` — one vector per input, in order.
    """

    inputs: list[str]
    normalize: bool = True
    truncate: bool = True


class TeiEmbeddingsClient:
    """Local backend: thin async client for TEI's ``/embed`` endpoint (the
    docker-compose ``embeddings`` container).

    ``model`` is not sent to TEI (it serves a single model) — it is retained only
    to label dimension-mismatch errors.
    """

    def __init__(self, base_url: str, model: str, expected_dim: int, timeout: float = 60.0):
        self._client = httpx.AsyncClient(base_url=base_url, timeout=timeout)
        self._model = model
        self._expected_dim = expected_dim

    async def embed_query(self, text: str) -> list[float]:
        request = TeiEmbedRequest(inputs=[text])
        response = await self._client.post("/embed", json=request.model_dump())
        response.raise_for_status()

        [vector] = _TeiEmbedResponse.validate_json(response.content)
        _check_dim(vector, self._expected_dim, self._model)
        return vector

    async def aclose(self) -> None:
        await self._client.aclose()


class BedrockEmbeddingsClient:
    """dev/prod backend: Amazon Bedrock embeddings (Titan Text Embeddings V2 by
    default). Credentials come from the task role on ECS — never from config.

    Titan V2 supports output dimensions of 256/512/1024 via the ``dimensions``
    request field; ``expected_dim`` is passed through so the column and the model
    agree. boto3's client is synchronous, so calls run in a worker thread to keep
    the event loop free.
    """

    def __init__(self, region: str, model_id: str, expected_dim: int):
        # Lazy import so the local (TEI) path needs neither boto3 nor AWS creds.
        import boto3

        self._client = boto3.client("bedrock-runtime", region_name=region)
        self._model_id = model_id
        self._expected_dim = expected_dim

    async def embed_query(self, text: str) -> list[float]:
        vector = await asyncio.to_thread(self._invoke, text)
        _check_dim(vector, self._expected_dim, self._model_id)
        return vector

    def _invoke(self, text: str) -> list[float]:
        body = {"inputText": text, "dimensions": self._expected_dim, "normalize": True}
        response = self._client.invoke_model(
            modelId=self._model_id,
            accept="application/json",
            contentType="application/json",
            body=json.dumps(body),
        )
        payload = json.loads(response["body"].read())
        return payload["embedding"]

    async def aclose(self) -> None:
        # botocore clients hold no long-lived connections that require closing.
        return None


def resolve_provider_name(settings: Settings) -> str:
    """Maps the environment (or explicit override) to a backend name."""
    if settings.embedding_provider != "auto":
        return settings.embedding_provider
    return "tei" if settings.env == "local" else "bedrock"


def create_embeddings_provider(settings: Settings) -> EmbeddingsProvider:
    """Builds the query-embedding backend for the current environment."""
    provider = resolve_provider_name(settings)
    logger.info(
        "embeddings backend selected",
        extra={"provider": provider, "environment": settings.env},
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
