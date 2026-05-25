from __future__ import annotations

import asyncio
from uuid import UUID, uuid4

import asyncpg  # type: ignore[import-untyped]
from fastapi.testclient import TestClient

from advanced_rag.auth.chat_tokens import ChatTokenClaims
from advanced_rag.core.config import Settings
from advanced_rag.main import create_app

from test_chat_rag import (
    ALLOWED_GROUP_ID,
    CHAT_MODEL,
    EMBEDDING_DIMENSIONS,
    EMBEDDING_MODEL,
    USER_ID,
    ChatDatabase,
    FakeChatTokenValidator,
    FakeEmbeddingProvider,
    FakeLlmProvider,
)


OTHER_USER_ID = UUID("dddddddd-dddd-dddd-dddd-dddddddddddd")


def test_authenticated_user_can_submit_feedback_and_comment_is_sanitized() -> None:
    with ChatDatabase() as database:
        audit_id = asyncio.run(_seed_feedback_audit_event(database, user_id=USER_ID))
        client = _client(database)

        response = client.post(
            f"/api/feedback/{audit_id}",
            json={"value": "down", "comment": "<b>No sirve</b>" + ("x" * 1100)},
        )
        stored = asyncio.run(_read_feedback(database, audit_id))

    assert response.status_code == 200
    assert response.json() == {
        "queryAuditEventId": str(audit_id),
        "value": "down",
        "comment": "No sirve" + ("x" * 992),
    }
    assert stored["feedback_value"] == "down"
    assert stored["feedback_comment"] == "No sirve" + ("x" * 992)
    assert stored["feedback_updated_at"] is not None


def test_duplicate_feedback_updates_the_same_audit_row() -> None:
    with ChatDatabase() as database:
        audit_id = asyncio.run(_seed_feedback_audit_event(database, user_id=USER_ID))
        client = _client(database)

        first = client.post(
            f"/api/feedback/{audit_id}",
            json={"value": "up", "comment": "Primera opinion"},
        )
        second = client.post(
            f"/api/feedback/{audit_id}",
            json={"value": "down", "comment": "Cambio de opinion"},
        )
        stored = asyncio.run(_read_feedback(database, audit_id))

    assert first.status_code == 200
    assert second.status_code == 200
    assert stored["feedback_value"] == "down"
    assert stored["feedback_comment"] == "Cambio de opinion"
    assert stored["audit_count"] == 1


def test_feedback_cannot_be_submitted_for_another_users_query() -> None:
    with ChatDatabase() as database:
        audit_id = asyncio.run(_seed_feedback_audit_event(database, user_id=OTHER_USER_ID))
        client = _client(database)

        response = client.post(
            f"/api/feedback/{audit_id}",
            json={"value": "up", "comment": "ok"},
        )
        stored = asyncio.run(_read_feedback(database, audit_id))

    assert response.status_code == 404
    assert response.json()["error"]["code"] == "NOT_FOUND"
    assert stored["feedback_value"] is None
    assert stored["feedback_comment"] is None


def _client(database: ChatDatabase) -> TestClient:
    app = create_app(
        Settings(
            rag_database_url=database.async_url,
            customer_timezone="UTC",
        ),
        embedding_provider=FakeEmbeddingProvider(),
        llm_provider=FakeLlmProvider(),
        chat_token_validator=FakeChatTokenValidator(
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
    client.cookies.set("__Host-chat-token", "valid")
    return client


async def _seed_feedback_audit_event(database: ChatDatabase, user_id: UUID) -> UUID:
    audit_id = uuid4()
    connection = await asyncpg.connect(database.dsn)
    try:
        price_id = await connection.fetchval("select id from rag.model_pricing limit 1")
        if price_id is None:
            price_id = uuid4()
            await connection.execute(
                """
                INSERT INTO rag.model_pricing (
                    id, model_id, model_kind, input_token_price_usd,
                    cached_token_price_usd, output_token_price_usd, effective_from
                )
                VALUES ($1, $2, 'chat', 0.0000001, 0.00000001, 0.0000004, now())
                """,
                price_id,
                CHAT_MODEL,
            )
        await connection.execute(
            """
            INSERT INTO rag.query_audit_events (
                id, user_id, request_id, question, answer, cache_hit,
                chat_model, embedding_model, embedding_dimensions,
                input_tokens, cached_tokens, output_tokens, pricing_snapshot_id,
                estimated_cost_usd, latency_ms, access_scope_hash, corpus,
                prompt_version, chunker_version
            )
            VALUES ($1, $2, 'req-feedback', 'Pregunta', 'Respuesta', false,
                    $3, $4, $5, 10, 0, 5,
                    $6, 0.0001, 25, 'scope-allowed', 'published', 1, 1)
            """,
            audit_id,
            user_id,
            CHAT_MODEL,
            EMBEDDING_MODEL,
            EMBEDDING_DIMENSIONS,
            price_id,
        )
    finally:
        await connection.close()
    return audit_id


async def _read_feedback(database: ChatDatabase, audit_id: UUID):
    connection = await asyncpg.connect(database.dsn)
    try:
        row = await connection.fetchrow(
            """
            SELECT feedback_value, feedback_comment, feedback_updated_at
            FROM rag.query_audit_events
            WHERE id = $1
            """,
            audit_id,
        )
        audit_count = await connection.fetchval("SELECT count(*) FROM rag.query_audit_events")
    finally:
        await connection.close()
    assert row is not None
    data = dict(row)
    data["audit_count"] = audit_count
    return data
