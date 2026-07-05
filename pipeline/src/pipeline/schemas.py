"""Pydantic request/response contracts for the external services the pipeline calls.

Validating responses at the boundary means a contract drift (renamed field,
wrong shape) fails immediately with a precise pydantic error instead of a
``KeyError`` deep inside a batch loop.
"""

from pydantic import BaseModel


class TeiEmbedRequest(BaseModel):
    """Body for TEI's batch ``POST /embed`` (HuggingFace Text Embeddings Inference).

    TEI serves a single model, so — unlike Ollama — the model name is not part of
    the request. ``normalize`` returns unit vectors (cosine-ready, matching the
    pgvector cosine index); ``truncate`` guards against inputs longer than the
    model's max sequence length. The response is a bare ``list[list[float]]`` —
    one vector per input, in order — so it needs no wrapper model.
    """

    inputs: list[str]
    normalize: bool = True
    truncate: bool = True
