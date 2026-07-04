"""The custom HTTP routes: /health (readiness) and /metrics (Prometheus)."""

from starlette.testclient import TestClient

from config import Settings
from conftest import FakeDatabase, FakeEmbeddings
from server.main import create_server


class DownDatabase(FakeDatabase):
    async def ping(self) -> None:
        raise ConnectionError("postgres is down")


def http_client(db) -> TestClient:
    server = create_server(Settings(), db, FakeEmbeddings())
    return TestClient(server.http_app())


def test_health_is_healthy_when_postgres_answers():
    with http_client(FakeDatabase()) as client:
        response = client.get("/health")

    assert response.status_code == 200
    assert response.json() == {"status": "healthy", "dependencies": {"postgres": "healthy"}}


def test_health_is_503_when_postgres_is_unreachable():
    with http_client(DownDatabase()) as client:
        response = client.get("/health")

    assert response.status_code == 503
    assert response.json()["dependencies"]["postgres"] == "unhealthy"


def test_metrics_exposes_the_prometheus_exposition():
    with http_client(FakeDatabase()) as client:
        response = client.get("/metrics")

    assert response.status_code == 200
    assert "mcp_tool_calls_total" in response.text
