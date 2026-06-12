from collections.abc import AsyncIterator
from http import HTTPStatus
import base64
import hashlib
import hmac
from uuid import uuid4

from fastapi.testclient import TestClient

from advanced_rag.auth.chat_tokens import ChatTokenClaims
from advanced_rag.core.config import Settings
from advanced_rag.main import create_app
from advanced_rag.rag.chat_service import ChatStreamEvent
from test_chat_rag import (
    FakeEmbeddingProvider,
    FakeLlmProvider,
)


def test_chat_authenticates_with_unified_session_cookie() -> None:
    session_validator = FakeSessionValidator(
        ChatTokenClaims(
            user_id="aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
            role="Viewer",
            groups=["bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"],
            access_scope_hash="scope-allowed",
            corpus="published",
        )
    )
    app = create_app(
        Settings(csrf_signing_key="test-csrf-signing-key"),
        embedding_provider=FakeEmbeddingProvider(),
        llm_provider=FakeLlmProvider(),
        session_validator=session_validator,
    )
    app.state.chat_service = FakeChatService()
    client = TestClient(app)
    client.cookies.set("__Host-session", "session-cookie-value")
    set_csrf(client)

    response = client.post(
        "/api/chat",
        json={"question": "Como hago el onboarding?"},
        headers={
            "X-Request-ID": "req-session-cookie",
            "X-CSRF-Token": client.cookies.get("__Host-CSRF") or "",
        },
    )

    assert response.status_code == HTTPStatus.OK
    assert session_validator.calls == [("session-cookie-value", "req-session-cookie")]


def test_chat_rejects_missing_csrf_token_before_session_validation() -> None:
    session_validator = FakeSessionValidator(
        ChatTokenClaims(
            user_id="aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
            role="Viewer",
            groups=["bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"],
            access_scope_hash="scope-allowed",
            corpus="published",
        )
    )
    app = create_app(
        Settings(csrf_signing_key="test-csrf-signing-key"),
        embedding_provider=FakeEmbeddingProvider(),
        llm_provider=FakeLlmProvider(),
        session_validator=session_validator,
    )
    app.state.chat_service = FakeChatService()
    client = TestClient(app)
    client.cookies.set("__Host-session", "session-cookie-value")

    response = client.post("/api/chat", json={"question": "Como hago el onboarding?"})

    assert response.status_code == HTTPStatus.BAD_REQUEST
    assert response.json()["error"]["code"] == "CSRF_TOKEN_INVALID"
    assert session_validator.calls == []


def test_feedback_rejects_invalid_csrf_token_before_session_validation() -> None:
    session_validator = FakeSessionValidator(
        ChatTokenClaims(
            user_id="aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
            role="Viewer",
            groups=["bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"],
            access_scope_hash="scope-allowed",
            corpus="published",
        )
    )
    app = create_app(
        Settings(csrf_signing_key="test-csrf-signing-key"),
        embedding_provider=FakeEmbeddingProvider(),
        llm_provider=FakeLlmProvider(),
        session_validator=session_validator,
    )
    client = TestClient(app)
    client.cookies.set("__Host-session", "session-cookie-value")
    client.cookies.set("__Host-CSRF", create_csrf_token("test-csrf-signing-key"))

    response = client.post(
        f"/api/feedback/{uuid4()}",
        json={"value": "up", "comment": ""},
        headers={"X-CSRF-Token": "tampered"},
    )

    assert response.status_code == HTTPStatus.BAD_REQUEST
    assert response.json()["error"]["code"] == "CSRF_TOKEN_INVALID"
    assert session_validator.calls == []


def test_chat_after_thirty_questions_per_user_is_rate_limited() -> None:
    session_validator = FakeSessionValidator(
        ChatTokenClaims(
            user_id="aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
            role="Viewer",
            groups=["bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"],
            access_scope_hash="scope-allowed",
            corpus="published",
        )
    )
    app = create_app(
        Settings(csrf_signing_key="test-csrf-signing-key"),
        embedding_provider=FakeEmbeddingProvider(),
        llm_provider=FakeLlmProvider(),
        session_validator=session_validator,
    )
    app.state.chat_service = FakeChatService()
    client = TestClient(app)
    client.cookies.set("__Host-session", "valid")
    set_csrf(client)

    for _ in range(30):
        response = client.post(
            "/api/chat",
            json={"question": "Como hago el onboarding?"},
            headers={"X-CSRF-Token": client.cookies.get("__Host-CSRF") or ""},
        )
        assert response.status_code != HTTPStatus.TOO_MANY_REQUESTS

    limited = client.post(
        "/api/chat",
        json={"question": "Como hago el onboarding?"},
        headers={"X-CSRF-Token": client.cookies.get("__Host-CSRF") or ""},
    )

    assert limited.status_code == HTTPStatus.TOO_MANY_REQUESTS
    assert limited.json()["error"]["code"] == "CHAT_RATE_LIMITED"


class FakeChatService:
    async def precheck(self, **_: object) -> None:
        return None

    async def answer_stream(self, **_: object) -> AsyncIterator[ChatStreamEvent]:
        yield ChatStreamEvent(event="answer-token", payload={"delta": "Respuesta"})
        yield ChatStreamEvent(
            event="citations",
            payload={"query_audit_event_id": str(uuid4()), "citations": []},
        )
        yield ChatStreamEvent(
            event="usage",
            payload={
                "input_tokens": 0,
                "cached_tokens": 0,
                "output_tokens": 0,
                "cost_usd": 0.0,
            },
        )


class FakeSessionValidator:
    def __init__(self, claims: ChatTokenClaims) -> None:
        self._claims = claims
        self.calls: list[tuple[str, str | None]] = []

    async def validate(self, session_cookie: str, request_id: str | None = None) -> ChatTokenClaims:
        self.calls.append((session_cookie, request_id))
        return self._claims


def set_csrf(client: TestClient, key: str = "test-csrf-signing-key") -> str:
    token = create_csrf_token(key)
    client.cookies.set("__Host-CSRF", token)
    return token


def create_csrf_token(key: str) -> str:
    payload = "nonce.1778467200"
    signature = hmac.new(key.encode("utf-8"), payload.encode("utf-8"), hashlib.sha256).digest()
    encoded = base64.urlsafe_b64encode(signature).decode("ascii").rstrip("=")
    return f"{payload}.{encoded}"
