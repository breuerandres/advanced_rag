from __future__ import annotations

import asyncio
import base64
import hashlib
import hmac
import json
import re
from datetime import UTC, datetime, timedelta
from decimal import Decimal
from pathlib import Path
from typing import Any
from uuid import UUID, uuid4

import asyncpg  # type: ignore[import-untyped]
from alembic import command
from alembic.config import Config
from fastapi.testclient import TestClient
from sqlalchemy.ext.asyncio import create_async_engine
from testcontainers.postgres import PostgresContainer  # type: ignore[import-untyped]

from advanced_rag.auth.chat_tokens import ChatTokenClaims
from advanced_rag.core.config import Settings
from advanced_rag.main import create_app
from advanced_rag.providers.base import (
    ChatCompletionDelta,
    ChatCompletionRequest,
    ChatUsage,
    ImageInput,
)
from advanced_rag.rag.hybrid_retrieval import HybridRetrievalParams, hybrid_retrieve
from advanced_rag.rag.multimodal_images import SelectedMultimodalImage


SERVICE_ROOT = Path(__file__).resolve().parents[1]
POSTGRES_IMAGE = "pgvector/pgvector:pg16"
POSTGRES_USER = "postgres"
POSTGRES_PASSWORD = "postgres"
POSTGRES_DB = "advanced_rag_chat_test"
USER_ID = UUID("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")
ALLOWED_GROUP_ID = UUID("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")
DENIED_GROUP_ID = UUID("cccccccc-cccc-cccc-cccc-cccccccccccc")
OTHER_USER_ID = UUID("dddddddd-dddd-dddd-dddd-dddddddddddd")

# Hierarchical-access fixtures. The root unit id matches both the .NET migration seed
# and the ChatTokenClaims default, so the branch-aware SQL treats a rule scoped to it as
# the explicit company-wide rule. The org tree is:
#   Empresa ── Comunicación ── Marketing
#          │                └─ Producción Audiovisual
#          └─ Sistemas
EMPRESA_ORG_UNIT_ID = UUID("01000000-0000-0000-0000-000000000001")
COMUNICACION_ORG_UNIT_ID = UUID("01000000-0000-0000-0000-0000000000c0")
MARKETING_ORG_UNIT_ID = UUID("01000000-0000-0000-0000-0000000000a1")
AUDIOVISUAL_ORG_UNIT_ID = UUID("01000000-0000-0000-0000-0000000000a2")
SISTEMAS_ORG_UNIT_ID = UUID("01000000-0000-0000-0000-0000000000a3")
CRISIS_GROUP_ID = UUID("0c000000-0000-0000-0000-0000000000c1")
HIERARCHY_AUTHOR_ID = UUID("0a000000-0000-0000-0000-00000000000a")

EMBEDDING_DIMENSIONS = 1024
EMBEDDING_MODEL = "text-embedding-3-large"
CHAT_MODEL = "gpt-4.1-nano"
TEST_CSRF_SIGNING_KEY = "test-csrf-signing-key"


def test_public_chat_retrieves_only_published_allowed_chunks_and_writes_audit() -> None:
    with _postgres() as database:
        allowed_document_id = uuid4()
        denied_document_id = uuid4()
        preview_document_id = uuid4()
        asyncio.run(
            database.seed_chat_corpus(
                allowed_document_id=allowed_document_id,
                denied_document_id=denied_document_id,
                preview_document_id=preview_document_id,
                monthly_budget=Decimal("5.0000"),
            )
        )
        app = create_app(
            Settings(
                rag_database_url=database.async_url,
                openai_chat_model=CHAT_MODEL,
                openai_embedding_model=EMBEDDING_MODEL,
                openai_embedding_dimensions=EMBEDDING_DIMENSIONS,
                customer_timezone="UTC",
                enable_reranker=False,
                csrf_signing_key=TEST_CSRF_SIGNING_KEY,
            ),
            embedding_provider=FakeEmbeddingProvider(),
            llm_provider=FakeLlmProvider(),
            session_validator=FakeSessionValidator(
                ChatTokenClaims(
                    user_id=str(USER_ID),
                    role="Viewer",
                    groups=[str(ALLOWED_GROUP_ID)],
                    access_scope_hash="scope-allowed",
                    corpus="published",
                )
            ),
        )
        client = TestClient(app)
        client.cookies.set("__Host-session", "valid")
        set_csrf(client)

        with client.stream(
            "POST",
            "/api/chat",
            json={"question": "What credential rule applies?"},
            headers={"X-Request-ID": "req-chat-1"},
        ) as response:
            body = "".join(response.iter_text())

        assert response.status_code == 200
        assert "event: answer-token" in body
        assert "Wear visible credentials." in body
        assert str(denied_document_id) not in body
        assert str(preview_document_id) not in body
        state = asyncio.run(database.read_audit_state())

    assert state["audit_count"] == 1
    assert state["citation_document_ids"] == [allowed_document_id]
    assert state["audit"]["request_id"] == "req-chat-1"
    assert state["audit"]["cache_hit"] is False
    assert state["audit"]["access_scope_hash"] == "scope-allowed"
    assert state["audit"]["chat_model"] == CHAT_MODEL
    assert state["audit"]["embedding_model"] == EMBEDDING_MODEL
    assert state["audit"]["embedding_dimensions"] == EMBEDDING_DIMENSIONS
    assert state["audit"]["estimated_cost_usd"] > Decimal("0")


def _published_chat_app(database: "ChatDatabase") -> Any:
    """Build a chat app with the standard published-corpus Viewer claims."""
    return create_app(
        Settings(
            rag_database_url=database.async_url,
            openai_chat_model=CHAT_MODEL,
            openai_embedding_model=EMBEDDING_MODEL,
            openai_embedding_dimensions=EMBEDDING_DIMENSIONS,
            customer_timezone="UTC",
            enable_reranker=False,
            csrf_signing_key=TEST_CSRF_SIGNING_KEY,
        ),
        embedding_provider=FakeEmbeddingProvider(),
        llm_provider=FakeLlmProvider(),
        session_validator=FakeSessionValidator(
            ChatTokenClaims(
                user_id=str(USER_ID),
                role="Viewer",
                groups=[str(ALLOWED_GROUP_ID)],
                access_scope_hash="scope-allowed",
                corpus="published",
            )
        ),
    )


def test_archived_document_is_not_retrieved() -> None:
    with _postgres() as database:
        allowed_document_id = uuid4()
        asyncio.run(
            database.seed_chat_corpus(
                allowed_document_id=allowed_document_id,
                denied_document_id=uuid4(),
                preview_document_id=uuid4(),
                monthly_budget=Decimal("5.0000"),
            )
        )
        asyncio.run(database.archive_document(allowed_document_id))
        app = _published_chat_app(database)
        client = TestClient(app)
        client.cookies.set("__Host-session", "valid")
        set_csrf(client)

        with client.stream(
            "POST",
            "/api/chat",
            json={"question": "What credential rule applies?"},
            headers={"X-Request-ID": "req-archived"},
        ) as response:
            body = "".join(response.iter_text())

        state = asyncio.run(database.read_audit_state())

    assert response.status_code == 200
    assert "Wear visible credentials." not in body
    assert state["citation_document_ids"] == []


def test_restored_to_draft_document_is_not_retrieved() -> None:
    with _postgres() as database:
        allowed_document_id = uuid4()
        asyncio.run(
            database.seed_chat_corpus(
                allowed_document_id=allowed_document_id,
                denied_document_id=uuid4(),
                preview_document_id=uuid4(),
                monthly_budget=Decimal("5.0000"),
            )
        )
        asyncio.run(database.restore_document_to_draft(allowed_document_id))
        app = _published_chat_app(database)
        client = TestClient(app)
        client.cookies.set("__Host-session", "valid")
        set_csrf(client)

        with client.stream(
            "POST",
            "/api/chat",
            json={"question": "What credential rule applies?"},
            headers={"X-Request-ID": "req-restored-draft"},
        ) as response:
            body = "".join(response.iter_text())

        state = asyncio.run(database.read_audit_state())

    assert response.status_code == 200
    assert "Wear visible credentials." not in body
    assert state["citation_document_ids"] == []


def test_only_current_published_version_is_retrieved() -> None:
    with _postgres() as database:
        allowed_document_id = uuid4()
        asyncio.run(
            database.seed_chat_corpus(
                allowed_document_id=allowed_document_id,
                denied_document_id=uuid4(),
                preview_document_id=uuid4(),
                monthly_budget=Decimal("5.0000"),
            )
        )
        version_a = asyncio.run(database.read_published_chunk_versions(allowed_document_id))[0]
        version_b = asyncio.run(
            database.add_superseded_published_version(
                allowed_document_id, "Wear visible credentials at all times."
            )
        )
        app = _published_chat_app(database)
        client = TestClient(app)
        client.cookies.set("__Host-session", "valid")
        set_csrf(client)

        with client.stream(
            "POST",
            "/api/chat",
            json={"question": "What credential rule applies?"},
            headers={"X-Request-ID": "req-superseded"},
        ) as response:
            "".join(response.iter_text())

        state = asyncio.run(database.read_audit_state())

    assert response.status_code == 200
    assert state["citation_document_ids"] == [allowed_document_id]
    assert set(state["citation_document_version_ids"]) == {version_b}
    assert version_a not in state["citation_document_version_ids"]


def _multimodal_chat_app(database: "ChatDatabase", llm: "MultimodalFakeLlmProvider") -> Any:
    return create_app(
        Settings(
            rag_database_url=database.async_url,
            openai_chat_model=CHAT_MODEL,
            openai_embedding_model=EMBEDDING_MODEL,
            openai_embedding_dimensions=EMBEDDING_DIMENSIONS,
            customer_timezone="UTC",
            enable_reranker=False,
            internal_service_token="test-internal",
            csrf_signing_key=TEST_CSRF_SIGNING_KEY,
        ),
        embedding_provider=FakeEmbeddingProvider(),
        llm_provider=llm,
        session_validator=FakeSessionValidator(
            ChatTokenClaims(
                user_id=str(USER_ID),
                role="Viewer",
                groups=[str(ALLOWED_GROUP_ID)],
                access_scope_hash="scope-allowed",
                corpus="published",
            )
        ),
    )


def test_chat_uses_multimodal_generation_when_retrieved_chunks_have_images(
    monkeypatch: Any,
) -> None:
    image_id = uuid4()

    async def fake_fetch(candidates: list[Any], **kwargs: Any) -> list[SelectedMultimodalImage]:
        return [
            SelectedMultimodalImage(
                image_id=image_id,
                content_type="image/png",
                bytes_data=b"\x89PNG",
                byte_count=4,
                detail="low",
            )
        ]

    monkeypatch.setattr("advanced_rag.rag.chat_service.fetch_selected_images", fake_fetch)

    with _postgres() as database:
        allowed_document_id = uuid4()
        asyncio.run(
            database.seed_chat_corpus(
                allowed_document_id=allowed_document_id,
                denied_document_id=uuid4(),
                preview_document_id=uuid4(),
                monthly_budget=Decimal("5.0000"),
            )
        )
        asyncio.run(database.seed_chunk_image(allowed_document_id, image_id))
        llm = MultimodalFakeLlmProvider()
        app = _multimodal_chat_app(database, llm)
        client = TestClient(app)
        client.cookies.set("__Host-session", "valid")
        set_csrf(client)

        response = client.post("/api/chat", json={"question": "What credential rule applies?"})
        state = asyncio.run(database.read_audit_state())

    assert response.status_code == 200
    assert len(llm.multimodal_calls) == 1
    assert state["audit"]["multimodal_used"] is True
    assert state["audit"]["multimodal_image_count"] == 1
    assert state["audit"]["multimodal_image_detail"] == "low"
    assert state["audit"]["multimodal_image_bytes_total"] == 4
    assert state["citation_document_ids"] == [allowed_document_id]


def test_multimodal_answer_is_not_written_to_semantic_cache(monkeypatch: Any) -> None:
    image_id = uuid4()

    async def fake_fetch(candidates: list[Any], **kwargs: Any) -> list[SelectedMultimodalImage]:
        return [
            SelectedMultimodalImage(
                image_id=image_id,
                content_type="image/png",
                bytes_data=b"\x89PNG",
                byte_count=4,
                detail="low",
            )
        ]

    monkeypatch.setattr("advanced_rag.rag.chat_service.fetch_selected_images", fake_fetch)

    with _postgres() as database:
        allowed_document_id = uuid4()
        asyncio.run(
            database.seed_chat_corpus(
                allowed_document_id=allowed_document_id,
                denied_document_id=uuid4(),
                preview_document_id=uuid4(),
                monthly_budget=Decimal("5.0000"),
            )
        )
        asyncio.run(database.seed_chunk_image(allowed_document_id, image_id))
        app = _multimodal_chat_app(database, MultimodalFakeLlmProvider())
        client = TestClient(app)
        client.cookies.set("__Host-session", "valid")
        set_csrf(client)

        response = client.post("/api/chat", json={"question": "What credential rule applies?"})
        cache_count = asyncio.run(database.count_semantic_cache_entries())

    assert response.status_code == 200
    assert cache_count == 0


def test_chat_falls_back_to_text_when_image_fetch_returns_no_images(monkeypatch: Any) -> None:
    image_id = uuid4()

    async def fake_fetch(candidates: list[Any], **kwargs: Any) -> list[SelectedMultimodalImage]:
        return []

    monkeypatch.setattr("advanced_rag.rag.chat_service.fetch_selected_images", fake_fetch)

    with _postgres() as database:
        allowed_document_id = uuid4()
        asyncio.run(
            database.seed_chat_corpus(
                allowed_document_id=allowed_document_id,
                denied_document_id=uuid4(),
                preview_document_id=uuid4(),
                monthly_budget=Decimal("5.0000"),
            )
        )
        asyncio.run(database.seed_chunk_image(allowed_document_id, image_id))
        llm = MultimodalFakeLlmProvider()
        app = _multimodal_chat_app(database, llm)
        client = TestClient(app)
        client.cookies.set("__Host-session", "valid")
        set_csrf(client)

        response = client.post("/api/chat", json={"question": "What credential rule applies?"})
        state = asyncio.run(database.read_audit_state())
        cache_count = asyncio.run(database.count_semantic_cache_entries())

    assert response.status_code == 200
    assert llm.multimodal_calls == []
    assert state["audit"]["multimodal_used"] is False
    assert state["citation_document_ids"] == [allowed_document_id]
    assert cache_count == 1


def test_semantic_cache_reuses_only_matching_access_scope_and_can_be_invalidated() -> None:
    with _postgres() as database:
        document_id = uuid4()
        asyncio.run(
            database.seed_chat_corpus(
                allowed_document_id=document_id,
                denied_document_id=uuid4(),
                preview_document_id=uuid4(),
                monthly_budget=Decimal("5.0000"),
            )
        )
        embedding_provider = FakeEmbeddingProvider()
        llm_provider = FakeLlmProvider()
        app = create_app(
            Settings(
                rag_database_url=database.async_url,
                openai_chat_model=CHAT_MODEL,
                openai_embedding_model=EMBEDDING_MODEL,
                openai_embedding_dimensions=EMBEDDING_DIMENSIONS,
                customer_timezone="UTC",
                rag_semantic_cache_similarity_threshold=0.90,
                rag_semantic_cache_ttl_hours=24,
                internal_service_token="test-internal",
                enable_reranker=False,
                csrf_signing_key=TEST_CSRF_SIGNING_KEY,
            ),
            embedding_provider=embedding_provider,
            llm_provider=llm_provider,
            session_validator=FakeSessionValidator(
                ChatTokenClaims(
                    user_id=str(USER_ID),
                    role="Viewer",
                    groups=[str(ALLOWED_GROUP_ID)],
                    access_scope_hash="scope-allowed",
                    corpus="published",
                )
            ),
        )
        client = TestClient(app)
        client.cookies.set("__Host-session", "valid")
        set_csrf(client)

        first = client.post(
            "/api/chat",
            json={"question": "What credential rule applies?"},
        )
        second = client.post(
            "/api/chat",
            json={"question": "What credential rule applies?"},
        )
        app.state.session_validator.claims = ChatTokenClaims(
            user_id=str(USER_ID),
            role="Viewer",
            groups=[str(DENIED_GROUP_ID)],
            access_scope_hash="scope-denied",
            corpus="published",
        )
        third = client.post(
            "/api/chat",
            json={"question": "What credential rule applies?"},
        )
        invalidation = client.post(
            "/internal/cache-invalidations",
            json={"documentIds": [str(document_id)]},
            headers={"X-Internal-Service-Token": "test-internal"},
        )
        invalidated_source_count = asyncio.run(database.count_cache_entries_for_document(document_id))

    assert first.status_code == 200
    assert second.status_code == 200
    assert "event: cache-hit" in second.text
    assert third.status_code == 200
    assert "event: cache-hit" not in third.text
    assert invalidation.status_code == 200
    assert invalidated_source_count == 0
    assert llm_provider.calls == 2


def test_semantic_cache_hit_returns_one_citation_per_source_document() -> None:
    with _postgres() as database:
        document_id = uuid4()
        asyncio.run(
            database.seed_chat_corpus(
                allowed_document_id=document_id,
                denied_document_id=uuid4(),
                preview_document_id=uuid4(),
                monthly_budget=Decimal("5.0000"),
            )
        )
        embedding_provider = FakeEmbeddingProvider()
        llm_provider = FakeLlmProvider()
        app = create_app(
            Settings(
                rag_database_url=database.async_url,
                openai_chat_model=CHAT_MODEL,
                openai_embedding_model=EMBEDDING_MODEL,
                openai_embedding_dimensions=EMBEDDING_DIMENSIONS,
                customer_timezone="UTC",
                rag_semantic_cache_similarity_threshold=0.90,
                rag_semantic_cache_ttl_hours=24,
                enable_reranker=False,
                csrf_signing_key=TEST_CSRF_SIGNING_KEY,
            ),
            embedding_provider=embedding_provider,
            llm_provider=llm_provider,
            session_validator=FakeSessionValidator(
                ChatTokenClaims(
                    user_id=str(USER_ID),
                    role="Viewer",
                    groups=[str(ALLOWED_GROUP_ID)],
                    access_scope_hash="scope-allowed",
                    corpus="published",
                )
            ),
        )
        client = TestClient(app)
        client.cookies.set("__Host-session", "valid")
        set_csrf(client)

        first = client.post(
            "/api/chat",
            json={"question": "What credential rule applies?"},
        )
        asyncio.run(database.seed_additional_active_chunk_for_document(document_id))
        second = client.post(
            "/api/chat",
            json={"question": "What credential rule applies?"},
        )

    assert first.status_code == 200
    assert second.status_code == 200
    assert "event: cache-hit" in second.text
    assert second.text.count(f'"document_id":"{document_id}"') == 1


def test_chat_filters_by_dimension_partitions_cache_separately() -> None:
    """Same question + same user + different filters → no cache hit between them."""
    with _postgres() as database:
        document_id = uuid4()
        asyncio.run(
            database.seed_chat_corpus(
                allowed_document_id=document_id,
                denied_document_id=uuid4(),
                preview_document_id=uuid4(),
                monthly_budget=Decimal("5.0000"),
            )
        )
        dimension_value_id = asyncio.run(
            database.seed_dimension_value(document_id=document_id)
        )
        llm_provider = FakeLlmProvider()
        app = create_app(
            Settings(
                rag_database_url=database.async_url,
                openai_chat_model=CHAT_MODEL,
                openai_embedding_model=EMBEDDING_MODEL,
                openai_embedding_dimensions=EMBEDDING_DIMENSIONS,
                customer_timezone="UTC",
                enable_reranker=False,
                csrf_signing_key=TEST_CSRF_SIGNING_KEY,
            ),
            embedding_provider=FakeEmbeddingProvider(),
            llm_provider=llm_provider,
            session_validator=FakeSessionValidator(
                ChatTokenClaims(
                    user_id=str(USER_ID),
                    role="Viewer",
                    groups=[str(ALLOWED_GROUP_ID)],
                    access_scope_hash="scope-allowed",
                    corpus="published",
                )
            ),
        )
        client = TestClient(app)
        client.cookies.set("__Host-session", "valid")
        set_csrf(client)

        unfiltered = client.post(
            "/api/chat",
            json={"question": "What credential rule applies?"},
        )
        # Second call WITH a filter must not hit the unfiltered cache.
        filtered = client.post(
            "/api/chat",
            json={
                "question": "What credential rule applies?",
                "filters": {"dimensionValueIds": [str(dimension_value_id)]},
            },
        )

    assert unfiltered.status_code == 200
    assert filtered.status_code == 200
    # Both went all the way to the LLM (no cross-filter cache hit).
    assert llm_provider.calls == 2


def test_chat_persists_session_id_and_rewritten_question_for_follow_up() -> None:
    with _postgres() as database:
        document_id = uuid4()
        session_id = uuid4()
        asyncio.run(
            database.seed_chat_corpus(
                allowed_document_id=document_id,
                denied_document_id=uuid4(),
                preview_document_id=uuid4(),
                monthly_budget=Decimal("5.0000"),
            )
        )
        embedding_provider = FakeEmbeddingProvider()
        llm_provider = CondensingFakeLlmProvider(
            rewritten_question="What credential rule applies for contractors?"
        )
        app = create_app(
            Settings(
                rag_database_url=database.async_url,
                openai_chat_model=CHAT_MODEL,
                openai_embedding_model=EMBEDDING_MODEL,
                openai_embedding_dimensions=EMBEDDING_DIMENSIONS,
                customer_timezone="UTC",
                enable_reranker=False,
                csrf_signing_key=TEST_CSRF_SIGNING_KEY,
            ),
            embedding_provider=embedding_provider,
            llm_provider=llm_provider,
            session_validator=FakeSessionValidator(
                ChatTokenClaims(
                    user_id=str(USER_ID),
                    role="Viewer",
                    groups=[str(ALLOWED_GROUP_ID)],
                    access_scope_hash="scope-allowed",
                    corpus="published",
                )
            ),
        )
        client = TestClient(app)
        client.cookies.set("__Host-session", "valid")
        set_csrf(client)

        first = client.post(
            "/api/chat",
            json={
                "question": "What credential rule applies?",
                "sessionId": str(session_id),
            },
        )
        second = client.post(
            "/api/chat",
            json={
                "question": "And what about contractors?",
                "sessionId": str(session_id),
            },
        )
        audit_rows = asyncio.run(database.read_session_audit_events(session_id))

    assert first.status_code == 200
    assert second.status_code == 200
    assert [row["question"] for row in audit_rows] == [
        "What credential rule applies?",
        "And what about contractors?",
    ]
    assert audit_rows[0]["session_id"] == session_id
    assert audit_rows[0]["rewritten_question"] is None
    assert audit_rows[1]["session_id"] == session_id
    assert audit_rows[1]["rewritten_question"] == "What credential rule applies for contractors?"
    assert embedding_provider.calls[-1] == ["What credential rule applies for contractors?"]
    assert len(llm_provider.condense_calls) == 1


def test_chat_session_list_and_history_are_user_scoped() -> None:
    with _postgres() as database:
        document_id = uuid4()
        session_id = uuid4()
        other_session_id = uuid4()
        asyncio.run(
            database.seed_chat_corpus(
                allowed_document_id=document_id,
                denied_document_id=uuid4(),
                preview_document_id=uuid4(),
                monthly_budget=Decimal("5.0000"),
            )
        )
        seeded = asyncio.run(
            database.seed_session_history(
                session_id=session_id,
                other_session_id=other_session_id,
                document_id=document_id,
            )
        )
        app = create_app(
            Settings(
                rag_database_url=database.async_url,
                openai_chat_model=CHAT_MODEL,
                openai_embedding_model=EMBEDDING_MODEL,
                openai_embedding_dimensions=EMBEDDING_DIMENSIONS,
                customer_timezone="UTC",
                enable_reranker=False,
                csrf_signing_key=TEST_CSRF_SIGNING_KEY,
            ),
            embedding_provider=FakeEmbeddingProvider(),
            llm_provider=FakeLlmProvider(),
            session_validator=FakeSessionValidator(
                ChatTokenClaims(
                    user_id=str(USER_ID),
                    role="Viewer",
                    groups=[str(ALLOWED_GROUP_ID)],
                    access_scope_hash="scope-allowed",
                    corpus="published",
                )
            ),
        )
        client = TestClient(app)
        client.cookies.set("__Host-session", "valid")
        set_csrf(client)

        sessions_response = client.get("/api/chat/sessions")
        history_response = client.get(f"/api/chat/sessions/{session_id}")
        other_history_response = client.get(f"/api/chat/sessions/{other_session_id}")

    assert sessions_response.status_code == 200
    sessions = sessions_response.json()["sessions"]
    assert [session["sessionId"] for session in sessions] == [str(session_id)]
    assert sessions[0]["title"] == "How do credentials work?"
    assert sessions[0]["lastQuestion"] == "And contractors?"
    assert sessions[0]["lastAnswer"] == "Contractors wear visitor badges."
    assert sessions[0]["turnCount"] == 2

    assert history_response.status_code == 200
    history = history_response.json()
    assert history["sessionId"] == str(session_id)
    assert [turn["question"] for turn in history["turns"]] == [
        "How do credentials work?",
        "And contractors?",
    ]
    assert history["turns"][0]["queryAuditEventId"] == str(seeded["first_event_id"])
    assert history["turns"][0]["cacheHit"] is False
    assert history["turns"][0]["citations"] == [
        {
            "chunkId": str(seeded["chunk_id"]),
            "documentId": str(document_id),
            "documentVersionId": str(seeded["document_version_id"]),
            "headingPath": ["Policy"],
        }
    ]
    assert history["turns"][1]["feedbackValue"] == "up"
    assert history["turns"][1]["feedbackComment"] == "Useful answer."
    assert other_history_response.status_code == 404
    assert other_history_response.json()["error"]["code"] == "CHAT_SESSION_NOT_FOUND"


def test_budget_exhaustion_blocks_before_paid_provider_calls() -> None:
    with _postgres() as database:
        asyncio.run(
            database.seed_chat_corpus(
                allowed_document_id=uuid4(),
                denied_document_id=uuid4(),
                preview_document_id=uuid4(),
                monthly_budget=Decimal("0.0001"),
                existing_spend=Decimal("0.0001"),
            )
        )
        embedding_provider = FakeEmbeddingProvider()
        llm_provider = FakeLlmProvider()
        app = create_app(
            Settings(
                rag_database_url=database.async_url,
                openai_chat_model=CHAT_MODEL,
                openai_embedding_model=EMBEDDING_MODEL,
                openai_embedding_dimensions=EMBEDDING_DIMENSIONS,
                customer_timezone="UTC",
                enable_reranker=False,
                csrf_signing_key=TEST_CSRF_SIGNING_KEY,
            ),
            embedding_provider=embedding_provider,
            llm_provider=llm_provider,
            session_validator=FakeSessionValidator(
                ChatTokenClaims(
                    user_id=str(USER_ID),
                    role="Viewer",
                    groups=[str(ALLOWED_GROUP_ID)],
                    access_scope_hash="scope-allowed",
                    corpus="published",
                )
            ),
        )
        client = TestClient(app)
        client.cookies.set("__Host-session", "valid")
        set_csrf(client)

        response = client.post(
            "/api/chat",
            json={"question": "What credential rule applies?"},
        )

    assert response.status_code == 429
    assert response.json()["error"]["code"] == "AI_BUDGET_EXCEEDED"
    assert embedding_provider.calls == []
    assert llm_provider.calls == 0


def test_doc_scoped_chat_answers_from_scoped_document_and_bypasses_cache() -> None:
    with _postgres() as database:
        allowed_document_id = uuid4()
        denied_document_id = uuid4()
        preview_document_id = uuid4()
        asyncio.run(
            database.seed_chat_corpus(
                allowed_document_id=allowed_document_id,
                denied_document_id=denied_document_id,
                preview_document_id=preview_document_id,
                monthly_budget=Decimal("5.0000"),
            )
        )
        app = create_app(
            Settings(
                rag_database_url=database.async_url,
                openai_chat_model=CHAT_MODEL,
                openai_embedding_model=EMBEDDING_MODEL,
                openai_embedding_dimensions=EMBEDDING_DIMENSIONS,
                customer_timezone="UTC",
                enable_reranker=False,
                csrf_signing_key=TEST_CSRF_SIGNING_KEY,
            ),
            embedding_provider=FakeEmbeddingProvider(),
            llm_provider=FakeLlmProvider(),
            session_validator=FakeSessionValidator(
                ChatTokenClaims(
                    user_id=str(USER_ID),
                    role="Viewer",
                    groups=[str(ALLOWED_GROUP_ID)],
                    access_scope_hash="scope-allowed",
                    corpus="published",
                )
            ),
        )
        client = TestClient(app)
        client.cookies.set("__Host-session", "valid")
        set_csrf(client)

        request_body = {
            "question": "What credential rule applies?",
            "documentId": str(allowed_document_id),
            "sessionId": str(uuid4()),
        }
        with client.stream(
            "POST", "/api/chat", json=request_body, headers={"X-Request-ID": "req-doc-1"}
        ) as first:
            first_body = "".join(first.iter_text())
        with client.stream(
            "POST", "/api/chat", json=request_body, headers={"X-Request-ID": "req-doc-2"}
        ) as second:
            second_body = "".join(second.iter_text())

        cache_entries = asyncio.run(
            database.count_cache_entries_for_document(allowed_document_id)
        )
        state = asyncio.run(database.read_audit_state())

    assert first.status_code == 200
    assert "Wear visible credentials." in first_body
    # Identical repeated question: a corpus-wide chat would hit the semantic cache here.
    assert "event: cache-hit" not in second_body
    assert cache_entries == 0
    assert state["audit"]["cache_hit"] is False
    assert state["audit"]["scope_document_id"] == allowed_document_id
    assert state["audit"]["corpus"] == "published"


def test_doc_scoped_chat_forces_published_corpus_for_management_roles() -> None:
    with _postgres() as database:
        allowed_document_id = uuid4()
        denied_document_id = uuid4()
        preview_document_id = uuid4()
        asyncio.run(
            database.seed_chat_corpus(
                allowed_document_id=allowed_document_id,
                denied_document_id=denied_document_id,
                preview_document_id=preview_document_id,
                monthly_budget=Decimal("5.0000"),
            )
        )
        app = create_app(
            Settings(
                rag_database_url=database.async_url,
                openai_chat_model=CHAT_MODEL,
                openai_embedding_model=EMBEDDING_MODEL,
                openai_embedding_dimensions=EMBEDDING_DIMENSIONS,
                customer_timezone="UTC",
                enable_reranker=False,
                csrf_signing_key=TEST_CSRF_SIGNING_KEY,
            ),
            embedding_provider=FakeEmbeddingProvider(),
            llm_provider=FakeLlmProvider(),
            session_validator=FakeSessionValidator(
                ChatTokenClaims(
                    user_id=str(USER_ID),
                    role="DocumentEditor",
                    is_global_admin=False,
                    groups=[str(ALLOWED_GROUP_ID)],
                    access_scope_hash="scope-editor",
                    corpus="management",
                )
            ),
        )
        client = TestClient(app)
        client.cookies.set("__Host-session", "valid")
        set_csrf(client)

        with client.stream(
            "POST",
            "/api/chat",
            json={
                "question": "What credential rule applies?",
                "documentId": str(allowed_document_id),
            },
            headers={"X-Request-ID": "req-doc-3"},
        ) as response:
            body = "".join(response.iter_text())

        state = asyncio.run(database.read_audit_state())

    assert response.status_code == 200
    # The published chunk is found even though the claims corpus is "management",
    # proving the doc-scoped path forces the published corpus.
    assert "Wear visible credentials." in body
    assert state["audit"]["corpus"] == "published"


def test_doc_scoped_chat_outside_access_returns_scoped_no_results_message() -> None:
    with _postgres() as database:
        allowed_document_id = uuid4()
        denied_document_id = uuid4()
        preview_document_id = uuid4()
        asyncio.run(
            database.seed_chat_corpus(
                allowed_document_id=allowed_document_id,
                denied_document_id=denied_document_id,
                preview_document_id=preview_document_id,
                monthly_budget=Decimal("5.0000"),
            )
        )
        app = create_app(
            Settings(
                rag_database_url=database.async_url,
                openai_chat_model=CHAT_MODEL,
                openai_embedding_model=EMBEDDING_MODEL,
                openai_embedding_dimensions=EMBEDDING_DIMENSIONS,
                customer_timezone="UTC",
                enable_reranker=False,
                csrf_signing_key=TEST_CSRF_SIGNING_KEY,
            ),
            embedding_provider=FakeEmbeddingProvider(),
            llm_provider=FakeLlmProvider(),
            session_validator=FakeSessionValidator(
                ChatTokenClaims(
                    user_id=str(USER_ID),
                    role="Viewer",
                    groups=[str(ALLOWED_GROUP_ID)],
                    access_scope_hash="scope-allowed",
                    corpus="published",
                )
            ),
        )
        client = TestClient(app)
        client.cookies.set("__Host-session", "valid")
        set_csrf(client)

        with client.stream(
            "POST",
            "/api/chat",
            json={
                "question": "What credential rule applies?",
                # The user is NOT in the denied document's group.
                "documentId": str(denied_document_id),
                "locale": "en-US",
            },
            headers={"X-Request-ID": "req-doc-4"},
        ) as response:
            body = "".join(response.iter_text())

        state = asyncio.run(database.read_audit_state())

    assert response.status_code == 200
    # ASCII assertion on purpose: the SSE payload is JSON with ensure_ascii escapes,
    # so the Spanish copy would appear as é sequences in the raw body.
    assert "I couldn't find information in this document" in body
    assert "Denied group content." not in body
    assert state["audit"]["scope_document_id"] == denied_document_id
    assert state["citation_document_ids"] == []


def test_doc_scoped_turns_are_excluded_from_session_list() -> None:
    with _postgres() as database:
        allowed_document_id = uuid4()
        denied_document_id = uuid4()
        preview_document_id = uuid4()
        asyncio.run(
            database.seed_chat_corpus(
                allowed_document_id=allowed_document_id,
                denied_document_id=denied_document_id,
                preview_document_id=preview_document_id,
                monthly_budget=Decimal("5.0000"),
            )
        )
        app = create_app(
            Settings(
                rag_database_url=database.async_url,
                openai_chat_model=CHAT_MODEL,
                openai_embedding_model=EMBEDDING_MODEL,
                openai_embedding_dimensions=EMBEDDING_DIMENSIONS,
                customer_timezone="UTC",
                enable_reranker=False,
                csrf_signing_key=TEST_CSRF_SIGNING_KEY,
            ),
            embedding_provider=FakeEmbeddingProvider(),
            llm_provider=FakeLlmProvider(),
            session_validator=FakeSessionValidator(
                ChatTokenClaims(
                    user_id=str(USER_ID),
                    role="Viewer",
                    groups=[str(ALLOWED_GROUP_ID)],
                    access_scope_hash="scope-allowed",
                    corpus="published",
                )
            ),
        )
        client = TestClient(app)
        client.cookies.set("__Host-session", "valid")
        set_csrf(client)

        doc_session_id = uuid4()
        corpus_session_id = uuid4()
        with client.stream(
            "POST",
            "/api/chat",
            json={
                "question": "What credential rule applies?",
                "documentId": str(allowed_document_id),
                "sessionId": str(doc_session_id),
            },
            headers={"X-Request-ID": "req-list-1"},
        ) as scoped:
            scoped.read()
        with client.stream(
            "POST",
            "/api/chat",
            json={
                "question": "What credential rule applies everywhere?",
                "sessionId": str(corpus_session_id),
            },
            headers={"X-Request-ID": "req-list-2"},
        ) as unscoped:
            unscoped.read()

        sessions_response = client.get("/api/chat/sessions")

    assert sessions_response.status_code == 200
    session_ids = [item["sessionId"] for item in sessions_response.json()["sessions"]]
    # The ephemeral doc-chat session never appears in chat-web's drawer.
    assert session_ids == [str(corpus_session_id)]


def test_hierarchical_retrieval_allows_ancestor_descendant_but_not_sibling_documents() -> None:
    with _postgres() as database:
        seeded = asyncio.run(database.seed_hierarchical_corpus())
        documents = asyncio.run(
            _retrieve_document_ids(
                database.async_url,
                HybridRetrievalParams(
                    corpus="published",
                    user_groups=[],
                    user_organizational_unit_id=seeded["marketing"],
                    root_organizational_unit_id=seeded["empresa"],
                    is_global_admin=False,
                ),
            )
        )

    # A Marketing user sees company-wide (Empresa), its ancestor branch (Comunicación),
    # and its own unit (Marketing); never a sibling (Producción Audiovisual) or another
    # branch (Sistemas), and not the crisis-group document without that group.
    assert documents == {
        seeded["empresa_doc"],
        seeded["comunicacion_doc"],
        seeded["marketing_doc"],
    }


def test_group_rule_requires_membership_and_empty_rule_does_not_match() -> None:
    with _postgres() as database:
        seeded = asyncio.run(database.seed_hierarchical_corpus())
        with_group = asyncio.run(
            _retrieve_document_ids(
                database.async_url,
                HybridRetrievalParams(
                    corpus="published",
                    user_groups=[seeded["crisis_group"]],
                    user_organizational_unit_id=seeded["comunicacion"],
                    root_organizational_unit_id=seeded["empresa"],
                ),
            )
        )
        without_group = asyncio.run(
            _retrieve_document_ids(
                database.async_url,
                HybridRetrievalParams(
                    corpus="published",
                    user_groups=[],
                    user_organizational_unit_id=seeded["comunicacion"],
                    root_organizational_unit_id=seeded["empresa"],
                ),
            )
        )

    # The crisis document requires both the Comunicación branch AND the crisis group.
    assert seeded["crisis_doc"] in with_group
    assert seeded["crisis_doc"] not in without_group
    # An empty rule never grants access, regardless of group membership.
    assert seeded["empty_doc"] not in with_group
    assert seeded["empty_doc"] not in without_group


def test_admin_global_scope_bypasses_rule_filter() -> None:
    with _postgres() as database:
        seeded = asyncio.run(database.seed_hierarchical_corpus())
        documents = asyncio.run(
            _retrieve_document_ids(
                database.async_url,
                HybridRetrievalParams(
                    corpus="published",
                    user_groups=[],
                    user_organizational_unit_id=seeded["sistemas"],
                    root_organizational_unit_id=seeded["empresa"],
                    is_global_admin=True,
                ),
            )
        )

    # A global admin retrieves every published document, regardless of branch or group,
    # and even an otherwise invalid empty rule.
    assert documents == {
        seeded["empresa_doc"],
        seeded["comunicacion_doc"],
        seeded["marketing_doc"],
        seeded["audiovisual_doc"],
        seeded["sistemas_doc"],
        seeded["crisis_doc"],
        seeded["empty_doc"],
    }


def test_scope_document_filter_limits_retrieval_to_one_accessible_document() -> None:
    with _postgres() as database:
        seeded = asyncio.run(database.seed_hierarchical_corpus())
        scoped = asyncio.run(
            _retrieve_document_ids(
                database.async_url,
                HybridRetrievalParams(
                    corpus="published",
                    user_groups=[],
                    user_organizational_unit_id=seeded["marketing"],
                    root_organizational_unit_id=seeded["empresa"],
                    scope_document_id=seeded["comunicacion_doc"],
                ),
            )
        )
        out_of_scope = asyncio.run(
            _retrieve_document_ids(
                database.async_url,
                HybridRetrievalParams(
                    corpus="published",
                    user_groups=[],
                    user_organizational_unit_id=seeded["marketing"],
                    root_organizational_unit_id=seeded["empresa"],
                    scope_document_id=seeded["sistemas_doc"],
                ),
            )
        )

    # The scope narrows retrieval to exactly one accessible document; scoping to a
    # document outside the user's branch yields nothing (access predicate still applies).
    assert scoped == {seeded["comunicacion_doc"]}
    assert out_of_scope == set()


def _postgres() -> ChatDatabase:
    return ChatDatabase()


class ChatDatabase:
    def __init__(self) -> None:
        self._container = PostgresContainer(
            image=POSTGRES_IMAGE,
            username=POSTGRES_USER,
            password=POSTGRES_PASSWORD,
            dbname=POSTGRES_DB,
        )

    def __enter__(self) -> ChatDatabase:
        self._container.__enter__()
        host = self._container.get_container_host_ip()
        port = self._container.get_exposed_port(5432)
        self.async_url = f"postgresql+asyncpg://{POSTGRES_USER}:{POSTGRES_PASSWORD}@{host}:{port}/{POSTGRES_DB}"
        self.dsn = f"postgresql://{POSTGRES_USER}:{POSTGRES_PASSWORD}@{host}:{port}/{POSTGRES_DB}"
        asyncio.run(self._bootstrap())
        _run_migrations(self.async_url)
        return self

    def __exit__(self, exc_type: object, exc: object, traceback: object) -> None:
        self._container.__exit__(exc_type, exc, traceback)

    async def _bootstrap(self) -> None:
        connection = await asyncpg.connect(self.dsn)
        try:
            # v2 hybrid retrieval needs all three.
            await connection.execute("CREATE EXTENSION IF NOT EXISTS vector")
            await connection.execute("CREATE EXTENSION IF NOT EXISTS pg_trgm")
            await connection.execute("CREATE EXTENSION IF NOT EXISTS unaccent")
            await connection.execute("CREATE SCHEMA app")
            await connection.execute("CREATE SCHEMA rag")
            await connection.execute(
                """
                CREATE TABLE app.users (
                    "Id" uuid primary key,
                    email text not null,
                    display_name text not null,
                    password_hash text not null,
                    is_active boolean not null default true,
                    created_at timestamptz not null default now()
                )
                """
            )
            await connection.execute(
                """
                CREATE TABLE app.groups (
                    "Id" uuid primary key,
                    name text not null
                )
                """
            )
            await connection.execute(
                """
                CREATE TABLE app.documents (
                    "Id" uuid primary key,
                    title text not null,
                    current_state text not null,
                    current_draft_version_id uuid null,
                    current_published_version_id uuid null,
                    created_by_user_id uuid not null,
                    created_at timestamptz not null default now(),
                    updated_at timestamptz not null default now()
                )
                """
            )
            # Mirrors the .NET hierarchical-access schema: organizational units with a
            # closure table, document permissions scoped to an org unit, and a
            # permission-to-group join. The legacy `group_id` column is kept for schema
            # parity but the branch-aware retrieval reads `document_permission_groups`.
            await connection.execute(
                """
                CREATE TABLE app.organizational_units (
                    "Id" uuid primary key,
                    name text not null,
                    parent_id uuid null references app.organizational_units("Id"),
                    is_active boolean not null default true,
                    created_at timestamptz not null default now(),
                    updated_at timestamptz not null default now()
                )
                """
            )
            await connection.execute(
                """
                CREATE TABLE app.organizational_unit_closure (
                    ancestor_id uuid not null references app.organizational_units("Id") on delete cascade,
                    descendant_id uuid not null references app.organizational_units("Id") on delete cascade,
                    depth integer not null,
                    primary key (ancestor_id, descendant_id)
                )
                """
            )
            await connection.execute(
                """
                CREATE TABLE app.document_permissions (
                    "Id" uuid primary key,
                    document_id uuid not null,
                    organizational_unit_id uuid null,
                    group_id uuid null,
                    attribute_key text null,
                    attribute_value text null,
                    created_at timestamptz not null default now()
                )
                """
            )
            await connection.execute(
                """
                CREATE TABLE app.document_permission_groups (
                    document_permission_id uuid not null references app.document_permissions("Id") on delete cascade,
                    group_id uuid not null references app.groups("Id") on delete cascade,
                    primary key (document_permission_id, group_id)
                )
                """
            )
            await connection.execute(
                """
                CREATE TABLE app.user_ai_budget_limits (
                    user_id uuid primary key,
                    monthly_budget_usd numeric(12,4) null,
                    is_disabled boolean not null default false,
                    updated_at timestamptz not null default now(),
                    updated_by_user_id uuid null
                )
                """
            )
            # Minimal dimensions schema so the hybrid retrieval filter join compiles
            # even when the v1 SQL migrations for these tables have not been applied
            # yet. The chat path passes NULL for the filter unless tests opt in.
            await connection.execute(
                """
                CREATE TABLE app.dimensions (
                    id uuid primary key,
                    key text not null unique,
                    label text not null
                )
                """
            )
            await connection.execute(
                """
                CREATE TABLE app.dimension_values (
                    id uuid primary key,
                    dimension_id uuid not null references app.dimensions(id) on delete cascade,
                    label text not null
                )
                """
            )
            await connection.execute(
                """
                CREATE TABLE app.document_dimension_values (
                    document_id uuid not null references app.documents("Id") on delete cascade,
                    dimension_value_id uuid not null references app.dimension_values(id) on delete cascade,
                    primary key (document_id, dimension_value_id)
                )
                """
            )
        finally:
            await connection.close()

    async def seed_chat_corpus(
        self,
        *,
        allowed_document_id: UUID,
        denied_document_id: UUID,
        preview_document_id: UUID,
        monthly_budget: Decimal,
        existing_spend: Decimal = Decimal("0"),
    ) -> None:
        connection = await asyncpg.connect(self.dsn)
        try:
            await connection.execute(
                "INSERT INTO app.users (\"Id\", email, display_name, password_hash) VALUES ($1, $2, $3, $4)",
                USER_ID,
                "viewer@example.com",
                "Viewer",
                "hash",
            )
            await connection.execute("INSERT INTO app.groups (\"Id\", name) VALUES ($1, $2)", ALLOWED_GROUP_ID, "Allowed")
            await connection.execute("INSERT INTO app.groups (\"Id\", name) VALUES ($1, $2)", DENIED_GROUP_ID, "Denied")
            await connection.execute(
                """
                INSERT INTO app.user_ai_budget_limits (user_id, monthly_budget_usd, is_disabled)
                VALUES ($1, $2, false)
                """,
                USER_ID,
                monthly_budget,
            )
            version_by_document: dict[UUID, UUID] = {
                allowed_document_id: uuid4(),
                denied_document_id: uuid4(),
                preview_document_id: uuid4(),
            }
            for document_id, title, group_id, corpus in [
                (allowed_document_id, "Allowed", ALLOWED_GROUP_ID, "published"),
                (denied_document_id, "Denied", DENIED_GROUP_ID, "published"),
                (preview_document_id, "Preview", ALLOWED_GROUP_ID, "preview"),
            ]:
                version_id = version_by_document[document_id]
                # Published-corpus docs pin current_published_version_id so the
                # retrieval lifecycle predicate matches; the preview doc pins its
                # draft pointer instead (preview corpus only checks non-archived).
                version_column = (
                    "current_published_version_id"
                    if corpus == "published"
                    else "current_draft_version_id"
                )
                await connection.execute(
                    f"""
                    INSERT INTO app.documents (
                        "Id", title, current_state, {version_column}, created_by_user_id
                    )
                    VALUES ($1, $2, 'Published', $3, $4)
                    """,
                    document_id,
                    title,
                    version_id,
                    USER_ID,
                )
                # Group-only rule in the hierarchical model: a permission with no org
                # unit plus a permission-to-group join row.
                permission_id = uuid4()
                await connection.execute(
                    """
                    INSERT INTO app.document_permissions ("Id", document_id, organizational_unit_id)
                    VALUES ($1, $2, NULL)
                    """,
                    permission_id,
                    document_id,
                )
                await connection.execute(
                    """
                    INSERT INTO app.document_permission_groups (document_permission_id, group_id)
                    VALUES ($1, $2)
                    """,
                    permission_id,
                    group_id,
                )
            chat_price_id = uuid4()
            embedding_price_id = uuid4()
            await connection.execute(
                """
                INSERT INTO rag.model_pricing (
                    id, model_id, model_kind, input_token_price_usd,
                    cached_token_price_usd, output_token_price_usd, effective_from
                )
                VALUES ($1, $2, 'chat', 0.0000001, 0.00000001, 0.0000004, now()),
                       ($3, $4, 'embedding', 0.00000002, null, null, now())
                """,
                chat_price_id,
                CHAT_MODEL,
                embedding_price_id,
                EMBEDDING_MODEL,
            )
            if existing_spend:
                await connection.execute(
                    """
                    INSERT INTO rag.query_audit_events (
                        id, user_id, request_id, question, answer, cache_hit,
                        chat_model, embedding_model, embedding_dimensions,
                        input_tokens, cached_tokens, output_tokens, pricing_snapshot_id,
                        estimated_cost_usd, latency_ms, access_scope_hash, corpus,
                        prompt_version, chunker_version
                    )
                    VALUES ($1, $2, 'spent', 'q', 'a', false, $3, $4, $5,
                            1, 0, 1, $6, $7, 1, 'scope-allowed', 'published', 1, 1)
                    """,
                    uuid4(),
                    USER_ID,
                    CHAT_MODEL,
                    EMBEDDING_MODEL,
                    EMBEDDING_DIMENSIONS,
                    chat_price_id,
                    existing_spend,
                )
            await self._insert_chunk(
                connection,
                allowed_document_id,
                "published",
                "Wear visible credentials.",
                document_version_id=version_by_document[allowed_document_id],
            )
            await self._insert_chunk(
                connection,
                denied_document_id,
                "published",
                "Denied group content.",
                document_version_id=version_by_document[denied_document_id],
            )
            await self._insert_chunk(
                connection,
                preview_document_id,
                "preview",
                "Preview-only content.",
                document_version_id=version_by_document[preview_document_id],
            )
        finally:
            await connection.close()

    async def seed_hierarchical_corpus(self) -> dict[str, UUID]:
        """Seed an org tree, a transverse group, and published documents with rules.

        Returns org-unit ids and document ids so tests can assert exact retrieval sets.
        Document access rules:
          * empresa_doc      -> org rule on the root unit (company-wide)
          * comunicacion_doc -> org rule on Comunicación
          * marketing_doc    -> org rule on Marketing
          * audiovisual_doc  -> org rule on Producción Audiovisual
          * sistemas_doc     -> org rule on Sistemas
          * crisis_doc       -> org rule on Comunicación AND the crisis group
          * empty_doc        -> an invalid empty rule (no org unit, no group)
        """
        connection = await asyncpg.connect(self.dsn)
        try:
            await self._insert_org_unit(connection, EMPRESA_ORG_UNIT_ID, "Empresa", None)
            await self._insert_org_unit(
                connection, COMUNICACION_ORG_UNIT_ID, "Comunicación", EMPRESA_ORG_UNIT_ID
            )
            await self._insert_org_unit(
                connection, MARKETING_ORG_UNIT_ID, "Marketing", COMUNICACION_ORG_UNIT_ID
            )
            await self._insert_org_unit(
                connection, AUDIOVISUAL_ORG_UNIT_ID, "Producción Audiovisual", COMUNICACION_ORG_UNIT_ID
            )
            await self._insert_org_unit(
                connection, SISTEMAS_ORG_UNIT_ID, "Sistemas", EMPRESA_ORG_UNIT_ID
            )

            # Closure rows: a self row (depth 0) plus one row per ancestor (nearest first).
            await self._insert_closure(connection, EMPRESA_ORG_UNIT_ID, [])
            await self._insert_closure(connection, COMUNICACION_ORG_UNIT_ID, [EMPRESA_ORG_UNIT_ID])
            await self._insert_closure(
                connection, MARKETING_ORG_UNIT_ID, [COMUNICACION_ORG_UNIT_ID, EMPRESA_ORG_UNIT_ID]
            )
            await self._insert_closure(
                connection, AUDIOVISUAL_ORG_UNIT_ID, [COMUNICACION_ORG_UNIT_ID, EMPRESA_ORG_UNIT_ID]
            )
            await self._insert_closure(connection, SISTEMAS_ORG_UNIT_ID, [EMPRESA_ORG_UNIT_ID])

            await connection.execute(
                'INSERT INTO app.groups ("Id", name) VALUES ($1, $2)',
                CRISIS_GROUP_ID,
                "Comité de crisis",
            )

            empresa_doc = uuid4()
            comunicacion_doc = uuid4()
            marketing_doc = uuid4()
            audiovisual_doc = uuid4()
            sistemas_doc = uuid4()
            crisis_doc = uuid4()
            empty_doc = uuid4()

            await self._insert_org_document(
                connection,
                document_id=empresa_doc,
                organizational_unit_id=EMPRESA_ORG_UNIT_ID,
                group_ids=[],
                content="Empresa onboarding handbook.",
            )
            await self._insert_org_document(
                connection,
                document_id=comunicacion_doc,
                organizational_unit_id=COMUNICACION_ORG_UNIT_ID,
                group_ids=[],
                content="Comunicación area guide.",
            )
            await self._insert_org_document(
                connection,
                document_id=marketing_doc,
                organizational_unit_id=MARKETING_ORG_UNIT_ID,
                group_ids=[],
                content="Marketing campaign calendar.",
            )
            await self._insert_org_document(
                connection,
                document_id=audiovisual_doc,
                organizational_unit_id=AUDIOVISUAL_ORG_UNIT_ID,
                group_ids=[],
                content="Audiovisual production checklist.",
            )
            await self._insert_org_document(
                connection,
                document_id=sistemas_doc,
                organizational_unit_id=SISTEMAS_ORG_UNIT_ID,
                group_ids=[],
                content="Sistemas operations runbook.",
            )
            await self._insert_org_document(
                connection,
                document_id=crisis_doc,
                organizational_unit_id=COMUNICACION_ORG_UNIT_ID,
                group_ids=[CRISIS_GROUP_ID],
                content="Crisis committee escalation protocol.",
            )
            await self._insert_org_document(
                connection,
                document_id=empty_doc,
                organizational_unit_id=None,
                group_ids=[],
                content="Orphan document with an empty access rule.",
            )

            return {
                "empresa": EMPRESA_ORG_UNIT_ID,
                "comunicacion": COMUNICACION_ORG_UNIT_ID,
                "marketing": MARKETING_ORG_UNIT_ID,
                "audiovisual": AUDIOVISUAL_ORG_UNIT_ID,
                "sistemas": SISTEMAS_ORG_UNIT_ID,
                "crisis_group": CRISIS_GROUP_ID,
                "empresa_doc": empresa_doc,
                "comunicacion_doc": comunicacion_doc,
                "marketing_doc": marketing_doc,
                "audiovisual_doc": audiovisual_doc,
                "sistemas_doc": sistemas_doc,
                "crisis_doc": crisis_doc,
                "empty_doc": empty_doc,
            }
        finally:
            await connection.close()

    async def _insert_org_unit(
        self,
        connection: asyncpg.Connection,
        unit_id: UUID,
        name: str,
        parent_id: UUID | None,
    ) -> None:
        await connection.execute(
            """
            INSERT INTO app.organizational_units ("Id", name, parent_id, is_active)
            VALUES ($1, $2, $3, true)
            """,
            unit_id,
            name,
            parent_id,
        )

    async def _insert_closure(
        self,
        connection: asyncpg.Connection,
        unit_id: UUID,
        ancestors: list[UUID],
    ) -> None:
        await connection.execute(
            """
            INSERT INTO app.organizational_unit_closure (ancestor_id, descendant_id, depth)
            VALUES ($1, $1, 0)
            """,
            unit_id,
        )
        for depth, ancestor_id in enumerate(ancestors, start=1):
            await connection.execute(
                """
                INSERT INTO app.organizational_unit_closure (ancestor_id, descendant_id, depth)
                VALUES ($1, $2, $3)
                """,
                ancestor_id,
                unit_id,
                depth,
            )

    async def _insert_org_document(
        self,
        connection: asyncpg.Connection,
        *,
        document_id: UUID,
        organizational_unit_id: UUID | None,
        group_ids: list[UUID],
        content: str,
    ) -> None:
        version_id = uuid4()
        await connection.execute(
            """
            INSERT INTO app.documents (
                "Id", title, current_state, current_published_version_id, created_by_user_id
            )
            VALUES ($1, $2, 'Published', $3, $4)
            """,
            document_id,
            content,
            version_id,
            HIERARCHY_AUTHOR_ID,
        )
        permission_id = uuid4()
        await connection.execute(
            """
            INSERT INTO app.document_permissions ("Id", document_id, organizational_unit_id)
            VALUES ($1, $2, $3)
            """,
            permission_id,
            document_id,
            organizational_unit_id,
        )
        for group_id in group_ids:
            await connection.execute(
                """
                INSERT INTO app.document_permission_groups (document_permission_id, group_id)
                VALUES ($1, $2)
                """,
                permission_id,
                group_id,
            )
        await self._insert_chunk(
            connection,
            document_id,
            "published",
            content,
            document_version_id=version_id,
        )

    async def seed_dimension_value(self, *, document_id: UUID) -> UUID:
        """Insert one dimension/value pair and tag the given document with it.

        Tagging is required because the hybrid retrieval SQL EXISTS-joins on
        `app.document_dimension_values`; an unassociated value would always
        filter to zero chunks and we want this test to exercise the cache
        partition logic, not the empty-result fallback path.
        """
        connection = await asyncpg.connect(self.dsn)
        try:
            dimension_id = uuid4()
            value_id = uuid4()
            await connection.execute(
                "INSERT INTO app.dimensions (id, key, label) VALUES ($1, $2, $3)",
                dimension_id,
                "modulo",
                "Modulo",
            )
            await connection.execute(
                "INSERT INTO app.dimension_values (id, dimension_id, label) VALUES ($1, $2, $3)",
                value_id,
                dimension_id,
                "ABR522",
            )
            await connection.execute(
                """
                INSERT INTO app.document_dimension_values (document_id, dimension_value_id)
                VALUES ($1, $2)
                """,
                document_id,
                value_id,
            )
            return value_id
        finally:
            await connection.close()

    async def seed_additional_active_chunk_for_document(self, document_id: UUID) -> None:
        connection = await asyncpg.connect(self.dsn)
        try:
            existing = await connection.fetchrow(
                """
                SELECT indexing_job_id, document_version_id, embedding_model
                FROM rag.document_chunks
                WHERE document_id = $1
                LIMIT 1
                """,
                document_id,
            )
            assert existing is not None
            await connection.execute(
                f"""
                INSERT INTO rag.document_chunks (
                    id, indexing_job_id, document_id, document_version_id,
                    corpus, chunk_index, heading_path, token_count, char_count,
                    content, content_html, embedding, embedding_model, is_active
                )
                VALUES ($1, $2, $3, $4, 'published', 1, ARRAY['Policy', 'Details'], 4, 26,
                        'Extra citation source.', '<p>Extra citation source.</p>',
                        ('[' || repeat('0.01,', {EMBEDDING_DIMENSIONS - 1}) || '0.01]')::vector,
                        $5, true)
                """,
                uuid4(),
                existing["indexing_job_id"],
                document_id,
                existing["document_version_id"],
                existing["embedding_model"],
            )
        finally:
            await connection.close()

    async def _insert_chunk(
        self,
        connection: asyncpg.Connection,
        document_id: UUID,
        corpus: str,
        content: str,
        document_version_id: UUID | None = None,
    ) -> UUID:
        job_id = uuid4()
        version_id = document_version_id if document_version_id is not None else uuid4()
        await connection.execute(
            """
            INSERT INTO rag.indexing_jobs (
                id, document_id, document_version_id, corpus, status,
                attempts, chunker_version, embedding_dimensions, chunk_count,
                embedding_model, embedding_tokens
            )
            VALUES ($1, $2, $3, $4, 'Succeeded', 1, 1, $5, 1, $6, 4)
            """,
            job_id,
            document_id,
            version_id,
            corpus,
            EMBEDDING_DIMENSIONS,
            EMBEDDING_MODEL,
        )
        await connection.execute(
            f"""
            INSERT INTO rag.document_chunks (
                id, indexing_job_id, document_id, document_version_id,
                corpus, chunk_index, heading_path, token_count, char_count,
                content, content_html, embedding, embedding_model, is_active
            )
            VALUES ($1, $2, $3, $4, $5, 0, ARRAY['Policy'], 4, $6, $7, $8,
                    ('[' || repeat('0.01,', {EMBEDDING_DIMENSIONS - 1}) || '0.01]')::vector,
                    $9, true)
            """,
            uuid4(),
            job_id,
            document_id,
            version_id,
            corpus,
            len(content),
            content,
            f"<p>{content}</p>",
            EMBEDDING_MODEL,
        )
        return version_id

    async def archive_document(self, document_id: UUID) -> None:
        connection = await asyncpg.connect(self.dsn)
        try:
            await connection.execute(
                "UPDATE app.documents SET current_state = 'Archived' WHERE \"Id\" = $1",
                document_id,
            )
        finally:
            await connection.close()

    async def restore_document_to_draft(self, document_id: UUID) -> None:
        connection = await asyncpg.connect(self.dsn)
        try:
            await connection.execute(
                """
                UPDATE app.documents
                SET current_state = 'Draft', current_published_version_id = NULL
                WHERE "Id" = $1
                """,
                document_id,
            )
        finally:
            await connection.close()

    async def add_superseded_published_version(self, document_id: UUID, content: str) -> UUID:
        """Insert a new active published chunk and point the document at its version.

        The prior published chunk is intentionally left active to simulate the
        pre-fix supersede bug, so retrieval correctness must come from the
        lifecycle predicate (current_published_version_id), not `is_active`.
        """
        connection = await asyncpg.connect(self.dsn)
        try:
            new_version_id = await self._insert_chunk(
                connection, document_id, "published", content, document_version_id=uuid4()
            )
            await connection.execute(
                "UPDATE app.documents SET current_published_version_id = $1 WHERE \"Id\" = $2",
                new_version_id,
                document_id,
            )
        finally:
            await connection.close()
        return new_version_id

    async def read_published_chunk_versions(self, document_id: UUID) -> list[UUID]:
        connection = await asyncpg.connect(self.dsn)
        try:
            rows = await connection.fetch(
                """
                SELECT DISTINCT document_version_id
                FROM rag.document_chunks
                WHERE document_id = $1 AND corpus = 'published' AND is_active = true
                """,
                document_id,
            )
        finally:
            await connection.close()
        return [row["document_version_id"] for row in rows]

    async def seed_chunk_image(self, document_id: UUID, image_id: UUID, ordinal: int = 0) -> None:
        connection = await asyncpg.connect(self.dsn)
        try:
            row = await connection.fetchrow(
                """
                SELECT id, document_version_id
                FROM rag.document_chunks
                WHERE document_id = $1 AND corpus = 'published' AND is_active = true
                ORDER BY chunk_index
                LIMIT 1
                """,
                document_id,
            )
            await connection.execute(
                """
                INSERT INTO rag.document_chunk_images (
                    id, chunk_id, document_id, document_version_id, image_id, ordinal
                )
                VALUES ($1, $2, $3, $4, $5, $6)
                """,
                uuid4(),
                row["id"],
                document_id,
                row["document_version_id"],
                image_id,
                ordinal,
            )
        finally:
            await connection.close()

    async def count_semantic_cache_entries(self) -> int:
        connection = await asyncpg.connect(self.dsn)
        try:
            return await connection.fetchval("SELECT count(*) FROM rag.semantic_cache_entries")
        finally:
            await connection.close()

    async def read_audit_state(self) -> dict[str, Any]:
        connection = await asyncpg.connect(self.dsn)
        try:
            audit = await connection.fetchrow("SELECT * FROM rag.query_audit_events ORDER BY created_at DESC LIMIT 1")
            citation_ids = await connection.fetch(
                """
                SELECT document_id, document_version_id
                FROM rag.query_audit_citations
                WHERE query_audit_event_id = $1
                ORDER BY created_at
                """,
                audit["id"],
            )
            audit_count = await connection.fetchval("SELECT count(*) FROM rag.query_audit_events")
        finally:
            await connection.close()
        return {
            "audit": dict(audit),
            "citation_document_ids": [row["document_id"] for row in citation_ids],
            "citation_document_version_ids": [row["document_version_id"] for row in citation_ids],
            "audit_count": audit_count,
        }

    async def read_session_audit_events(self, session_id: UUID) -> list[dict[str, Any]]:
        connection = await asyncpg.connect(self.dsn)
        try:
            rows = await connection.fetch(
                """
                SELECT question, session_id, rewritten_question
                FROM rag.query_audit_events
                WHERE session_id = $1
                ORDER BY created_at ASC
                """,
                session_id,
            )
        finally:
            await connection.close()
        return [dict(row) for row in rows]

    async def seed_session_history(
        self,
        *,
        session_id: UUID,
        other_session_id: UUID,
        document_id: UUID,
    ) -> dict[str, UUID]:
        connection = await asyncpg.connect(self.dsn)
        try:
            chunk = await connection.fetchrow(
                """
                SELECT id, document_version_id
                FROM rag.document_chunks
                WHERE document_id = $1
                LIMIT 1
                """,
                document_id,
            )
            assert chunk is not None
            first_event_id = uuid4()
            second_event_id = uuid4()
            other_event_id = uuid4()
            created_at = datetime.now(UTC) - timedelta(minutes=3)
            await self._insert_audit_turn(
                connection,
                event_id=first_event_id,
                user_id=USER_ID,
                session_id=session_id,
                previous_event_id=None,
                created_at=created_at,
                question="How do credentials work?",
                rewritten_question=None,
                answer="Wear visible credentials.",
                feedback_value=None,
                feedback_comment=None,
            )
            await connection.execute(
                """
                INSERT INTO rag.query_audit_citations (
                    id, query_audit_event_id, chunk_id, document_id,
                    document_version_id, heading_path
                )
                VALUES ($1, $2, $3, $4, $5, $6)
                """,
                uuid4(),
                first_event_id,
                chunk["id"],
                document_id,
                chunk["document_version_id"],
                ["Policy"],
            )
            await self._insert_audit_turn(
                connection,
                event_id=second_event_id,
                user_id=USER_ID,
                session_id=session_id,
                previous_event_id=first_event_id,
                created_at=created_at + timedelta(minutes=1),
                question="And contractors?",
                rewritten_question="How do credentials work for contractors?",
                answer="Contractors wear visitor badges.",
                feedback_value="up",
                feedback_comment="Useful answer.",
            )
            await self._insert_audit_turn(
                connection,
                event_id=other_event_id,
                user_id=OTHER_USER_ID,
                session_id=other_session_id,
                previous_event_id=None,
                created_at=created_at + timedelta(minutes=2),
                question="Other user question",
                rewritten_question=None,
                answer="Other user answer.",
                feedback_value=None,
                feedback_comment=None,
            )
            return {
                "first_event_id": first_event_id,
                "second_event_id": second_event_id,
                "other_event_id": other_event_id,
                "chunk_id": chunk["id"],
                "document_version_id": chunk["document_version_id"],
            }
        finally:
            await connection.close()

    async def _insert_audit_turn(
        self,
        connection: asyncpg.Connection,
        *,
        event_id: UUID,
        user_id: UUID,
        session_id: UUID,
        previous_event_id: UUID | None,
        created_at: datetime,
        question: str,
        rewritten_question: str | None,
        answer: str,
        feedback_value: str | None,
        feedback_comment: str | None,
    ) -> None:
        feedback_updated_at = created_at + timedelta(seconds=5) if feedback_value else None
        await connection.execute(
            """
            INSERT INTO rag.query_audit_events (
                id, user_id, request_id, created_at, question, answer,
                cache_hit, chat_model, embedding_model, embedding_dimensions,
                input_tokens, cached_tokens, output_tokens, estimated_cost_usd,
                latency_ms, access_scope_hash, corpus, prompt_version,
                chunker_version, session_id, previous_event_id, rewritten_question,
                feedback_value, feedback_comment, feedback_updated_at
            )
            VALUES (
                $1, $2, $3, $4, $5, $6,
                false, $7, $8, $9,
                10, 0, 5, 0.00000100,
                25, 'scope-allowed', 'published', 1,
                1, $10, $11, $12,
                $13, $14, $15
            )
            """,
            event_id,
            user_id,
            f"req-{event_id}",
            created_at,
            question,
            answer,
            CHAT_MODEL,
            EMBEDDING_MODEL,
            EMBEDDING_DIMENSIONS,
            session_id,
            previous_event_id,
            rewritten_question,
            feedback_value,
            feedback_comment,
            feedback_updated_at,
        )

    async def count_cache_entries_for_document(self, document_id: UUID) -> int:
        connection = await asyncpg.connect(self.dsn)
        try:
            return int(
                await connection.fetchval(
                    """
                    SELECT count(*)
                    FROM rag.semantic_cache_sources
                    WHERE document_id = $1
                    """,
                    document_id,
                )
            )
        finally:
            await connection.close()


def _run_migrations(async_url: str) -> None:
    config = Config(str(SERVICE_ROOT / "alembic.ini"))
    config.set_main_option("sqlalchemy.url", async_url)
    command.upgrade(config, "head")


async def _retrieve_document_ids(async_url: str, params: HybridRetrievalParams) -> set[UUID]:
    """Run the branch-aware retrieval directly and return the matched document ids.

    Every fake chunk is embedded with the same vector, so the vector-candidate leg
    surfaces all published chunks and the access predicate is the only thing that
    narrows the result. This isolates the SQL access rules from ranking behaviour.
    """
    engine = create_async_engine(async_url)
    try:
        async with engine.connect() as connection:
            candidates = await hybrid_retrieve(
                connection,
                q_text="policy",
                q_embedding=[0.01] * EMBEDDING_DIMENSIONS,
                params=params,
            )
    finally:
        await engine.dispose()
    return {candidate.document_id for candidate in candidates}


class FakeEmbeddingProvider:
    """In-memory `IEmbeddingProvider` that returns a constant vector per text.

    The vector is shaped to `EMBEDDING_DIMENSIONS` so the SQL `vector(N)` column
    accepts it. The exact values are arbitrary because the test corpus has only
    a few chunks; vector ordering does not depend on their similarity.
    """

    name = "fake"
    model = EMBEDDING_MODEL
    dimensions = EMBEDDING_DIMENSIONS

    def __init__(self) -> None:
        self.calls: list[list[str]] = []

    async def embed(self, texts: list[str]) -> tuple[list[list[float]], ChatUsage]:
        self.calls.append(texts)
        return [[0.01] * self.dimensions for _ in texts], ChatUsage(input_tokens=4)


class FakeLlmProvider:
    """Minimal `ILlmProvider` used in tests.

    Extracts the first `chunk_id=<uuid>` from the user message, copies the chunk's
    content into `answer`, and reports that chunk as the only citation. Honours the
    JSON `response_format` contract.
    """

    name = "fake"

    def __init__(self) -> None:
        self.calls = 0

    async def chat_complete(
        self, req: ChatCompletionRequest
    ) -> tuple[str, ChatUsage]:
        self.calls += 1
        user_msg = next((m for m in req.messages if m.role == "user"), None)
        if user_msg is None:
            return self._empty_response()
        match = re.search(r"chunk_id=([0-9a-fA-F-]+)", user_msg.content)
        if not match:
            return self._empty_response()
        chunk_id = match.group(1)
        content_match = re.search(
            rf"chunk_id={re.escape(chunk_id)}[^\n]*\n(.*?)(?:\n\n\[chunk_id=|\n\nQuestion:|\Z)",
            user_msg.content,
            re.DOTALL,
        )
        first_line = ""
        if content_match:
            lines = content_match.group(1).strip().splitlines()
            first_line = lines[0] if lines else ""
        payload = json.dumps({"answer": first_line, "cited_chunk_ids": [chunk_id]})
        return payload, ChatUsage(input_tokens=20, output_tokens=10)

    async def chat_stream(self, req: ChatCompletionRequest):
        content, _ = await self.chat_complete(req)
        yield ChatCompletionDelta(content=content, finish_reason="stop")

    def _empty_response(self) -> tuple[str, ChatUsage]:
        return (
            json.dumps({"answer": "", "cited_chunk_ids": []}),
            ChatUsage(input_tokens=20, output_tokens=10),
        )


class CondensingFakeLlmProvider(FakeLlmProvider):
    def __init__(self, *, rewritten_question: str) -> None:
        super().__init__()
        self.rewritten_question = rewritten_question
        self.condense_calls: list[ChatCompletionRequest] = []

    async def chat_complete(
        self, req: ChatCompletionRequest
    ) -> tuple[str, ChatUsage]:
        if req.response_format is None:
            self.condense_calls.append(req)
            return self.rewritten_question, ChatUsage(input_tokens=8, output_tokens=4)
        return await super().chat_complete(req)


class MultimodalFakeLlmProvider(FakeLlmProvider):
    """`FakeLlmProvider` that also advertises the multimodal capability.

    Reuses the text answer logic so citations still resolve, while recording the
    images it was handed so tests can assert the multimodal path ran.
    """

    def __init__(self) -> None:
        super().__init__()
        self.multimodal_calls: list[list[ImageInput]] = []

    async def multimodal_complete(
        self, req: ChatCompletionRequest, images: list[ImageInput]
    ) -> tuple[str, ChatUsage]:
        self.multimodal_calls.append(list(images))
        return await self.chat_complete(req)


class FakeSessionValidator:
    def __init__(self, claims: ChatTokenClaims) -> None:
        self.claims = claims

    async def validate(self, session_cookie: str, request_id: str | None = None) -> ChatTokenClaims:
        _ = session_cookie
        _ = request_id
        return self.claims


def set_csrf(client: TestClient, key: str = TEST_CSRF_SIGNING_KEY) -> str:
    token = create_csrf_token(key)
    client.cookies.set("__Host-CSRF", token)
    client.headers.update({"X-CSRF-Token": token})
    return token


def create_csrf_token(key: str) -> str:
    payload = "nonce.1778467200"
    signature = hmac.new(key.encode("utf-8"), payload.encode("utf-8"), hashlib.sha256).digest()
    encoded = base64.urlsafe_b64encode(signature).decode("ascii").rstrip("=")
    return f"{payload}.{encoded}"
