from __future__ import annotations

import asyncio
from dataclasses import dataclass
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


SERVICE_ROOT = Path(__file__).resolve().parents[1]
POSTGRES_IMAGE = "pgvector/pgvector:pg16"
POSTGRES_USER = "postgres"
POSTGRES_PASSWORD = "postgres"
POSTGRES_DB = "advanced_rag_chat_test"
USER_ID = UUID("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")
ALLOWED_GROUP_ID = UUID("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")
DENIED_GROUP_ID = UUID("cccccccc-cccc-cccc-cccc-cccccccccccc")


def test_public_chat_retrieves_only_published_allowed_chunks_and_writes_audit() -> None:
    with _postgres() as database:
        allowed_instruction_id = uuid4()
        denied_instruction_id = uuid4()
        preview_instruction_id = uuid4()
        asyncio.run(
            database.seed_chat_corpus(
                allowed_instruction_id=allowed_instruction_id,
                denied_instruction_id=denied_instruction_id,
                preview_instruction_id=preview_instruction_id,
                monthly_budget=Decimal("5.0000"),
            )
        )
        app = create_app(
            Settings(
                rag_database_url=database.async_url,
                openai_chat_model="gpt-4.1-nano",
                openai_embedding_model="text-embedding-3-small",
                openai_embedding_dimensions=1536,
                customer_timezone="UTC",
            ),
            embedding_provider=FakeEmbeddingProvider(),
            chat_completion_provider=FakeChatCompletionProvider(),
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
        assert str(denied_instruction_id) not in body
        assert str(preview_instruction_id) not in body
        state = asyncio.run(database.read_audit_state())

    assert state["audit_count"] == 1
    assert state["citation_instruction_ids"] == [allowed_instruction_id]
    assert state["audit"]["request_id"] == "req-chat-1"
    assert state["audit"]["cache_hit"] is False
    assert state["audit"]["access_scope_hash"] == "scope-allowed"
    assert state["audit"]["chat_model"] == "gpt-4.1-nano"
    assert state["audit"]["embedding_model"] == "text-embedding-3-small"
    assert state["audit"]["estimated_cost_usd"] > Decimal("0")


def test_semantic_cache_reuses_only_matching_access_scope_and_can_be_invalidated() -> None:
    with _postgres() as database:
        instruction_id = uuid4()
        asyncio.run(
            database.seed_chat_corpus(
                allowed_instruction_id=instruction_id,
                denied_instruction_id=uuid4(),
                preview_instruction_id=uuid4(),
                monthly_budget=Decimal("5.0000"),
            )
        )
        embedding_provider = FakeEmbeddingProvider()
        chat_provider = FakeChatCompletionProvider()
        app = create_app(
            Settings(
                rag_database_url=database.async_url,
                customer_timezone="UTC",
                rag_semantic_cache_similarity_threshold=0.90,
                rag_semantic_cache_ttl_hours=24,
                internal_service_token="test-internal",
            ),
            embedding_provider=embedding_provider,
            chat_completion_provider=chat_provider,
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

        first = client.post(
            "/api/chat",
            json={"question": "What credential rule applies?"},
        )
        second = client.post(
            "/api/chat",
            json={"question": "What credential rule applies?"},
        )
        app.state.chat_token_validator = FakeChatTokenValidator(
            ChatTokenClaims(
                user_id=str(USER_ID),
                role="Viewer",
                groups=[str(DENIED_GROUP_ID)],
                access_scope_hash="scope-denied",
                corpus="published",
            )
        )
        third = client.post(
            "/api/chat",
            json={"question": "What credential rule applies?"},
        )
        invalidation = client.post(
            "/internal/cache-invalidations",
            json={"instructionIds": [str(instruction_id)]},
            headers={"X-Internal-Service-Token": "test-internal"},
        )
        invalidated_source_count = asyncio.run(database.count_cache_entries_for_instruction(instruction_id))

    assert first.status_code == 200
    assert second.status_code == 200
    assert "event: cache-hit" in second.text
    assert third.status_code == 200
    assert "event: cache-hit" not in third.text
    assert invalidation.status_code == 200
    assert invalidated_source_count == 0
    assert chat_provider.calls == 2


def test_budget_exhaustion_blocks_before_paid_provider_calls() -> None:
    with _postgres() as database:
        asyncio.run(
            database.seed_chat_corpus(
                allowed_instruction_id=uuid4(),
                denied_instruction_id=uuid4(),
                preview_instruction_id=uuid4(),
                monthly_budget=Decimal("0.0001"),
                existing_spend=Decimal("0.0001"),
            )
        )
        embedding_provider = FakeEmbeddingProvider()
        chat_provider = FakeChatCompletionProvider()
        app = create_app(
            Settings(
                rag_database_url=database.async_url,
                customer_timezone="UTC",
            ),
            embedding_provider=embedding_provider,
            chat_completion_provider=chat_provider,
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

        response = client.post(
            "/api/chat",
            json={"question": "What credential rule applies?"},
        )

    assert response.status_code == 429
    assert response.json()["error"]["code"] == "AI_BUDGET_EXCEEDED"
    assert embedding_provider.calls == []
    assert chat_provider.calls == 0


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
            await connection.execute("CREATE EXTENSION IF NOT EXISTS vector")
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
                CREATE TABLE app.instructions (
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
                CREATE TABLE app.instruction_permissions (
                    "Id" uuid primary key,
                    instruction_id uuid not null,
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
        finally:
            await connection.close()

    async def seed_chat_corpus(
        self,
        *,
        allowed_instruction_id: UUID,
        denied_instruction_id: UUID,
        preview_instruction_id: UUID,
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
            for instruction_id, title, group_id in [
                (allowed_instruction_id, "Allowed", ALLOWED_GROUP_ID),
                (denied_instruction_id, "Denied", DENIED_GROUP_ID),
                (preview_instruction_id, "Preview", ALLOWED_GROUP_ID),
            ]:
                await connection.execute(
                    """
                    INSERT INTO app.instructions ("Id", title, current_state, created_by_user_id)
                    VALUES ($1, $2, 'Published', $3)
                    """,
                    instruction_id,
                    title,
                    USER_ID,
                )
                await connection.execute(
                    """
                    INSERT INTO app.instruction_permissions ("Id", instruction_id, group_id)
                    VALUES ($1, $2, $3)
                    """,
                    uuid4(),
                    instruction_id,
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
                VALUES ($1, 'gpt-4.1-nano', 'chat', 0.0000001, 0.00000001, 0.0000004, now()),
                       ($2, 'text-embedding-3-small', 'embedding', 0.00000002, null, null, now())
                """,
                chat_price_id,
                embedding_price_id,
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
                    VALUES ($1, $2, 'spent', 'q', 'a', false, 'gpt-4.1-nano',
                            'text-embedding-3-small', 1536, 1, 0, 1, $3, $4, 1,
                            'scope-allowed', 'published', 1, 1)
                    """,
                    uuid4(),
                    USER_ID,
                    chat_price_id,
                    existing_spend,
                )
            await self._insert_chunk(connection, allowed_instruction_id, "published", "Wear visible credentials.")
            await self._insert_chunk(connection, denied_instruction_id, "published", "Denied group content.")
            await self._insert_chunk(connection, preview_instruction_id, "preview", "Preview-only content.")
        finally:
            await connection.close()

    async def _insert_chunk(
        self,
        connection: asyncpg.Connection,
        instruction_id: UUID,
        corpus: str,
        content: str,
    ) -> None:
        job_id = uuid4()
        version_id = uuid4()
        await connection.execute(
            """
            INSERT INTO rag.indexing_jobs (
                id, instruction_id, instruction_version_id, corpus, status,
                attempts, chunker_version, embedding_dimensions, chunk_count,
                embedding_model, embedding_tokens
            )
            VALUES ($1, $2, $3, $4, 'Succeeded', 1, 1, 1536, 1, 'text-embedding-3-small', 4)
            """,
            job_id,
            instruction_id,
            version_id,
            corpus,
        )
        await connection.execute(
            """
            INSERT INTO rag.document_chunks (
                id, indexing_job_id, instruction_id, instruction_version_id,
                corpus, chunk_index, heading_path, token_count, char_count,
                content, content_html, embedding, embedding_model, is_active
            )
            VALUES ($1, $2, $3, $4, $5, 0, ARRAY['Policy'], 4, $6, $7, $8,
                    ('[' || repeat('0.01,', 1535) || '0.01]')::vector,
                    'text-embedding-3-small', true)
            """,
            uuid4(),
            job_id,
            instruction_id,
            version_id,
            corpus,
            len(content),
            content,
            f"<p>{content}</p>",
        )

    async def read_audit_state(self) -> dict[str, Any]:
        connection = await asyncpg.connect(self.dsn)
        try:
            audit = await connection.fetchrow("SELECT * FROM rag.query_audit_events ORDER BY created_at DESC LIMIT 1")
            citation_ids = await connection.fetch(
                """
                SELECT instruction_id
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
            "citation_instruction_ids": [row["instruction_id"] for row in citation_ids],
            "audit_count": audit_count,
        }

    async def count_cache_entries_for_instruction(self, instruction_id: UUID) -> int:
        connection = await asyncpg.connect(self.dsn)
        try:
            return int(
                await connection.fetchval(
                    """
                    SELECT count(*)
                    FROM rag.semantic_cache_sources
                    WHERE instruction_id = $1
                    """,
                    instruction_id,
                )
            )
        finally:
            await connection.close()


def _run_migrations(async_url: str) -> None:
    config = Config(str(SERVICE_ROOT / "alembic.ini"))
    config.set_main_option("sqlalchemy.url", async_url)
    command.upgrade(config, "head")


class FakeEmbeddingProvider:
    def __init__(self) -> None:
        self.calls: list[list[str]] = []

    async def embed_texts(
        self,
        texts: list[str],
        *,
        model: str,
        dimensions: int,
    ) -> list[list[float]]:
        self.calls.append(texts)
        return [[0.01] * dimensions for _ in texts]


class FakeChatCompletionProvider:
    def __init__(self) -> None:
        self.calls = 0

    async def complete(
        self,
        *,
        question: str,
        context_chunks: list[Any],
        model: str,
    ) -> Any:
        self.calls += 1
        chunk = context_chunks[0]
        return FakeChatCompletion(
            answer=f"Respuesta basada en: {chunk.content}",
            cited_chunk_ids=[chunk.id],
            input_tokens=20,
            output_tokens=10,
        )


class FakeChatTokenValidator:
    def __init__(self, claims: ChatTokenClaims) -> None:
        self._claims = claims

    def validate(self, token: str) -> ChatTokenClaims:
        return self._claims


@dataclass(frozen=True)
class FakeChatCompletion:
    answer: str
    cited_chunk_ids: list[UUID]
    input_tokens: int
    output_tokens: int
