from http import HTTPStatus

from fastapi.testclient import TestClient

from advanced_rag.core.health import ReadinessResult
from advanced_rag.main import create_app


def test_live_health_returns_ok_status() -> None:
    client = TestClient(create_app())

    response = client.get("/health/live")

    assert response.status_code == HTTPStatus.OK
    assert response.json() == {"status": "ok"}


def test_ready_health_returns_ok_status() -> None:
    app = create_app()
    app.state.readiness_checker = FakeReadinessChecker(ReadinessResult(is_ready=True, failed_checks=[]))
    client = TestClient(app)

    response = client.get("/health/ready")

    assert response.status_code == HTTPStatus.OK
    assert response.json() == {"status": "ok"}


def test_ready_health_returns_service_unavailable_when_dependency_fails() -> None:
    app = create_app()
    app.state.readiness_checker = FakeReadinessChecker(
        ReadinessResult(is_ready=False, failed_checks=["database", "openai_api_key"])
    )
    client = TestClient(app)

    response = client.get("/health/ready")

    assert response.status_code == HTTPStatus.SERVICE_UNAVAILABLE
    assert response.json() == {"status": "unhealthy", "checks": ["database", "openai_api_key"]}


def test_live_health_remains_ok_when_readiness_fails() -> None:
    app = create_app()
    app.state.readiness_checker = FakeReadinessChecker(
        ReadinessResult(is_ready=False, failed_checks=["database"])
    )
    client = TestClient(app)

    response = client.get("/health/live")

    assert response.status_code == HTTPStatus.OK
    assert response.json() == {"status": "ok"}


class FakeReadinessChecker:
    def __init__(self, result: ReadinessResult) -> None:
        self._result = result

    async def check(self) -> ReadinessResult:
        return self._result
