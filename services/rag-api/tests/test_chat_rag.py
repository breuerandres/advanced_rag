from __future__ import annotations

import asyncio
import base64
import hashlib
import hmac
import json
import re
from decimal import Decimal
from pathlib import Path
from typing import Any
from uuid import UUID, uuid4

import asyncpg  # type: ignore[import-untyped]
from alembic import command
from alembic.config import Config
from fastapi.testclient import TestClient
from testcontainers.postgres import PostgresContainer  # type: ignore[import-untyped]

from advanced_rag.auth.chat_tokens import ChatTokenClaims
from advanced_rag.core.config import Settings
from advanced_rag.main import create_app
from advanced_rag.providers.base import (
    ChatCompletionDelta,
    ChatCompletionRequest,
    ChatUsage,
)


SERVICE_ROOT = Path(__file__).resolve().parents[1]
POSTGRES_IMAGE = "pgvector/pgvector:pg16"
POSTGRES_USER = "postgres"
POSTGRES_PASSWORD = "postgres"
POSTGRES_DB = "advanced_rag_chat_test"
USER_ID = UUID("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")
ALLOWED_GROUP_ID = UUID("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")
DENIED_GROUP_ID = UUID("cccccccc-cccc-cccc-cccc-cccccccccccc")

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
            await connection.execute(
                """
                CREATE TABLE app.document_permissions (
                    "Id" uuid primary key,
                    document_id uuid not null,
                    group_id uuid null,
                    attribute_key text null,
                    attribute_value text null,
                    created_at timestamptz not null default now()
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
            for document_id, title, group_id in [
                (allowed_document_id, "Allowed", ALLOWED_GROUP_ID),
                (denied_document_id, "Denied", DENIED_GROUP_ID),
                (preview_document_id, "Preview", ALLOWED_GROUP_ID),
            ]:
                await connection.execute(
                    """
                    INSERT INTO app.documents ("Id", title, current_state, created_by_user_id)
                    VALUES ($1, $2, 'Published', $3)
                    """,
                    document_id,
                    title,
                    USER_ID,
                )
                await connection.execute(
                    """
                    INSERT INTO app.document_permissions ("Id", document_id, group_id)
                    VALUES ($1, $2, $3)
                    """,
                    uuid4(),
                    document_id,
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
            await self._insert_chunk(connection, allowed_document_id, "published", "Wear visible credentials.")
            await self._insert_chunk(connection, denied_document_id, "published", "Denied group content.")
            await self._insert_chunk(connection, preview_document_id, "preview", "Preview-only content.")
        finally:
            await connection.close()

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

    async def _insert_chunk(
        self,
        connection: asyncpg.Connection,
        document_id: UUID,
        corpus: str,
        content: str,
    ) -> None:
        job_id = uuid4()
        version_id = uuid4()
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

    async def read_audit_state(self) -> dict[str, Any]:
        connection = await asyncpg.connect(self.dsn)
        try:
            audit = await connection.fetchrow("SELECT * FROM rag.query_audit_events ORDER BY created_at DESC LIMIT 1")
            citation_ids = await connection.fetch(
                """
                SELECT document_id
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
            "audit_count": audit_count,
        }

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
