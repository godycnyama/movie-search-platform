"""Backend selection: ENV (and the explicit override) picks Ollama
vs Bedrock, and the factory builds the matching client."""

import boto3
import pytest

from config import Settings
from server.embeddings import (
    BedrockEmbeddingsClient,
    OllamaEmbeddingsClient,
    create_embeddings_provider,
    resolve_provider_name,
)


@pytest.mark.parametrize(
    ("environment", "expected"),
    [("local", "ollama"), ("dev", "bedrock"), ("prod", "bedrock")],
)
def test_env_selects_the_backend(environment, expected):
    settings = Settings(env=environment, embedding_provider="auto")
    assert resolve_provider_name(settings) == expected


@pytest.mark.parametrize("override", ["ollama", "bedrock"])
def test_explicit_provider_overrides_the_environment(override):
    # local would normally be Ollama; the explicit override wins either way.
    settings = Settings(env="local", embedding_provider=override)
    assert resolve_provider_name(settings) == override


def test_factory_builds_ollama_for_local():
    settings = Settings(
        env="local",
        ollama_url="http://ollama.test:11434",
        embedding_model="nomic-embed-text",
        embedding_dim=768,
    )

    provider = create_embeddings_provider(settings)

    assert isinstance(provider, OllamaEmbeddingsClient)
    assert provider._expected_dim == 768
    assert provider._model == "nomic-embed-text"


def test_factory_builds_bedrock_for_prod(monkeypatch):
    # Keep the test hermetic — no real boto3 client / AWS calls.
    monkeypatch.setattr(boto3, "client", lambda *args, **kwargs: object())
    settings = Settings(
        env="prod",
        bedrock_embedding_model_id="amazon.titan-embed-text-v2:0",
        embedding_dim=1024,
    )

    provider = create_embeddings_provider(settings)

    assert isinstance(provider, BedrockEmbeddingsClient)
    assert provider._model_id == "amazon.titan-embed-text-v2:0"
    assert provider._expected_dim == 1024
