from http import HTTPStatus

from fastapi.testclient import TestClient

from advanced_rag.main import create_app


def test_live_health_returns_ok_status() -> None:
    client = TestClient(create_app())

    response = client.get("/health/live")

    assert response.status_code == HTTPStatus.OK
    assert response.json() == {"status": "ok"}


def test_ready_health_returns_ok_status() -> None:
    client = TestClient(create_app())

    response = client.get("/health/ready")

    assert response.status_code == HTTPStatus.OK
    assert response.json() == {"status": "ok"}
