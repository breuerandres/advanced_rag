from http import HTTPStatus
from decimal import Decimal
from uuid import uuid4

from fastapi.testclient import TestClient

from advanced_rag.auth.chat_tokens import ChatTokenClaims
from advanced_rag.main import create_app
from advanced_rag.rag.chat_service import ChatAnswer
from test_chat_rag import (
    FakeChatTokenValidator,
    FakeEmbeddingProvider,
    FakeLlmProvider,
)


def test_chat_after_thirty_questions_per_user_is_rate_limited() -> None:
    app = create_app(
        embedding_provider=FakeEmbeddingProvider(),
        llm_provider=FakeLlmProvider(),
        chat_token_validator=FakeChatTokenValidator(
            ChatTokenClaims(
                user_id="aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
                role="Viewer",
                groups=["bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"],
                access_scope_hash="scope-allowed",
                corpus="published",
            )
        ),
    )
    app.state.chat_service = FakeChatService()
    client = TestClient(app)
    client.cookies.set("__Host-chat-token", "valid")

    for _ in range(30):
        response = client.post("/api/chat", json={"question": "Como hago el onboarding?"})
        assert response.status_code != HTTPStatus.TOO_MANY_REQUESTS

    limited = client.post("/api/chat", json={"question": "Como hago el onboarding?"})

    assert limited.status_code == HTTPStatus.TOO_MANY_REQUESTS
    assert limited.json()["error"]["code"] == "CHAT_RATE_LIMITED"


class FakeChatService:
    async def answer(self, **_: object) -> ChatAnswer:
        return ChatAnswer(
            query_audit_event_id=uuid4(),
            answer="Respuesta",
            citations=[],
            cache_hit=False,
            cached_at=None,
            input_tokens=0,
            cached_tokens=0,
            output_tokens=0,
            estimated_cost_usd=Decimal("0"),
        )
