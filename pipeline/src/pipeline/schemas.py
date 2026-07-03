"""Pydantic request/response contracts for the external services the pipeline calls.

Validating responses at the boundary means a contract drift (renamed field,
wrong shape) fails immediately with a precise pydantic error instead of a
``KeyError`` deep inside a batch loop.
"""

from pydantic import BaseModel, ConfigDict


class OllamaEmbedRequest(BaseModel):
    """Body for Ollama's batch ``POST /api/embed``."""

    model: str
    input: list[str]


class OllamaEmbedResponse(BaseModel):
    """Response from ``POST /api/embed`` — one vector per input, in order."""

    # Ollama adds timing/telemetry fields freely; only validate what we use.
    model_config = ConfigDict(extra="ignore")

    model: str | None = None
    embeddings: list[list[float]]
    total_duration: int | None = None
    prompt_eval_count: int | None = None
