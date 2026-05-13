from http import HTTPStatus

from fastapi import FastAPI
from fastapi.testclient import TestClient

from advanced_rag.core.errors import ApiException
from advanced_rag.main import create_app


def test_unknown_route_returns_shared_error_envelope() -> None:
    client = TestClient(create_app())

    response = client.get("/missing-route", headers={"X-Request-ID": "test-request-id"})

    assert response.status_code == HTTPStatus.NOT_FOUND
    assert response.headers["X-Request-ID"] == "test-request-id"
    assert response.json() == {
        "error": {
            "code": "NOT_FOUND",
            "message": "Resource not found.",
            "details": {},
            "requestId": "test-request-id",
        }
    }


def test_explicit_validation_error_returns_shared_error_envelope() -> None:
    app = create_app()
    _add_validation_probe(app)
    client = TestClient(app)

    response = client.get("/__test/validation", headers={"X-Request-ID": "validation-request-id"})

    assert response.status_code == HTTPStatus.BAD_REQUEST
    assert response.headers["X-Request-ID"] == "validation-request-id"
    assert response.json() == {
        "error": {
            "code": "VALIDATION_FAILED",
            "message": "Validation failed.",
            "details": {"field": "question"},
            "requestId": "validation-request-id",
        }
    }


def _add_validation_probe(app: FastAPI) -> None:
    @app.get("/__test/validation")
    async def validation_probe() -> None:
        raise ApiException(
            code="VALIDATION_FAILED",
            http_status=400,
            message="Validation failed.",
            details={"field": "question"},
        )
