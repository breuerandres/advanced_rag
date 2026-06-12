from __future__ import annotations

import asyncio
from datetime import UTC, datetime, timedelta
from pathlib import Path
from uuid import UUID, uuid4

import asyncpg  # type: ignore[import-untyped]
from alembic import command
from alembic.config import Config
from fastapi import FastAPI
from fastapi.testclient import TestClient
from testcontainers.postgres import PostgresContainer  # type: ignore[import-untyped]

from advanced_rag.core.config import Settings
from advanced_rag.db.session import create_database_engine, create_session_factory
from advanced_rag.main import create_app
from advanced_rag.rag.maintenance_service import MaintenanceService

SERVICE_ROOT = Path(__file__).resolve().parents[1]
POSTGRES_IMAGE = "pgvector/pgvector:pg16"
POSTGRES_USER = "postgres"
POSTGRES_PASSWORD = "postgres"
POSTGRES_DB = "advanced_rag_maintenance_test"
INTERNAL_TOKEN = "test-internal-token"

EMBEDDING_DIMENSIONS = 1024
EMBEDDING_MODEL = "text-embedding-3-large"


def test_purge_deletes_old_inactive_chunks_only() -> None:
    with _postgres() as database:
        old_inactive = uuid4()
        recent_inactive = uuid4()
        old_active = uuid4()
        asyncio.run(
            database.seed_chunk(old_inactive, is_active=False, age_days=10)
        )
        asyncio.run(
            database.seed_chunk(recent_inactive, is_active=False, age_days=1)
        )
        asyncio.run(database.seed_chunk(old_active, is_active=True, age_days=10))

        service = MaintenanceService(database.session_factory)
        result = asyncio.run(service.purge(retention_days=7))
        remaining = asyncio.run(database.chunk_ids())

    assert result.inactive_chunks_deleted == 1
    assert old_inactive not in remaining
    assert recent_inactive in remaining
    assert old_active in remaining


def test_purge_deletes_expired_cache_entries() -> None:
    with _postgres() as database:
        expired = uuid4()
        live = uuid4()
        asyncio.run(database.seed_cache_entry(expired, expires_in_hours=-1))
        asyncio.run(database.seed_cache_entry(live, expires_in_hours=24))

        service = MaintenanceService(database.session_factory)
        result = asyncio.run(service.purge(retention_days=7))
        remaining = asyncio.run(database.cache_entry_ids())
        remaining_sources = asyncio.run(database.cache_source_entry_ids())

    assert result.cache_entries_deleted == 1
    assert expired not in remaining
    assert live in remaining
    # Sources cascade with their entry.
    assert expired not in remaining_sources
    assert live in remaining_sources


def test_purge_endpoint_requires_internal_token() -> None:
    with _postgres() as database:
        app = _maintenance_app(database)
        client = TestClient(app)

        missing = client.post("/internal/maintenance/purge")
        invalid = client.post(
            "/internal/maintenance/purge",
            headers={"X-Internal-Service-Token": "wrong"},
        )

    assert missing.status_code == 401
    assert missing.json()["error"]["code"] == "AUTH_INTERNAL_TOKEN_INVALID"
    assert invalid.status_code == 401
    assert invalid.json()["error"]["code"] == "AUTH_INTERNAL_TOKEN_INVALID"


def test_purge_endpoint_returns_deletion_counts() -> None:
    with _postgres() as database:
        asyncio.run(database.seed_chunk(uuid4(), is_active=False, age_days=10))
        asyncio.run(database.seed_cache_entry(uuid4(), expires_in_hours=-1))
        app = _maintenance_app(database)
        client = TestClient(app)

        response = client.post(
            "/internal/maintenance/purge",
            headers={"X-Internal-Service-Token": INTERNAL_TOKEN},
        )

    assert response.status_code == 200
    body = response.json()
    assert body == {"inactiveChunksDeleted": 1, "cacheEntriesDeleted": 1}


def _maintenance_app(database: MaintenanceDatabase) -> FastAPI:
    return create_app(
        Settings(
            rag_database_url=database.async_url,
            internal_service_token=INTERNAL_TOKEN,
            rag_inactive_chunk_retention_days=7,
        )
    )


def _postgres() -> MaintenanceDatabase:
    return MaintenanceDatabase()


class MaintenanceDatabase:
    def __init__(self) -> None:
        self._container = PostgresContainer(
            image=POSTGRES_IMAGE,
            username=POSTGRES_USER,
            password=POSTGRES_PASSWORD,
            dbname=POSTGRES_DB,
        )

    def __enter__(self) -> MaintenanceDatabase:
        self._container.__enter__()
        host = self._container.get_container_host_ip()
        port = self._container.get_exposed_port(5432)
        self.async_url = (
            f"postgresql+asyncpg://{POSTGRES_USER}:{POSTGRES_PASSWORD}@{host}:{port}/{POSTGRES_DB}"
        )
        self.dsn = f"postgresql://{POSTGRES_USER}:{POSTGRES_PASSWORD}@{host}:{port}/{POSTGRES_DB}"
        asyncio.run(self._bootstrap())
        _run_migrations(self.async_url)
        self._engine = create_database_engine(self.async_url)
        self.session_factory = create_session_factory(self._engine)
        return self

    def __exit__(self, exc_type: object, exc: object, traceback: object) -> None:
        asyncio.run(self._engine.dispose())
        self._container.__exit__(exc_type, exc, traceback)

    async def _bootstrap(self) -> None:
        connection = await asyncpg.connect(self.dsn)
        try:
            await connection.execute("CREATE EXTENSION IF NOT EXISTS vector")
            await connection.execute("CREATE EXTENSION IF NOT EXISTS pg_trgm")
            await connection.execute("CREATE EXTENSION IF NOT EXISTS unaccent")
            await connection.execute("CREATE SCHEMA rag")
        finally:
            await connection.close()

    async def seed_chunk(self, chunk_id: UUID, *, is_active: bool, age_days: int) -> None:
        connection = await asyncpg.connect(self.dsn)
        try:
            job_id = uuid4()
            document_id = uuid4()
            version_id = uuid4()
            created_at = datetime.now(UTC) - timedelta(days=age_days)
            await connection.execute(
                """
                INSERT INTO rag.indexing_jobs (
                    id, document_id, document_version_id, corpus, status,
                    attempts, chunker_version, embedding_dimensions, chunk_count,
                    embedding_model, embedding_tokens
                )
                VALUES ($1, $2, $3, 'published', 'Succeeded', 1, 1, $4, 1, $5, 4)
                """,
                job_id,
                document_id,
                version_id,
                EMBEDDING_DIMENSIONS,
                EMBEDDING_MODEL,
            )
            await connection.execute(
                f"""
                INSERT INTO rag.document_chunks (
                    id, indexing_job_id, document_id, document_version_id,
                    corpus, chunk_index, heading_path, token_count, char_count,
                    content, content_html, embedding, embedding_model, is_active, created_at
                )
                VALUES ($1, $2, $3, $4, 'published', 0, ARRAY['Policy'], 4, 5, 'text', '<p>text</p>',
                        ('[' || repeat('0.01,', {EMBEDDING_DIMENSIONS - 1}) || '0.01]')::vector,
                        $5, $6, $7)
                """,
                chunk_id,
                job_id,
                document_id,
                version_id,
                EMBEDDING_MODEL,
                is_active,
                created_at,
            )
        finally:
            await connection.close()

    async def seed_cache_entry(self, entry_id: UUID, *, expires_in_hours: int) -> None:
        connection = await asyncpg.connect(self.dsn)
        try:
            cached_at = datetime.now(UTC)
            expires_at = cached_at + timedelta(hours=expires_in_hours)
            await connection.execute(
                f"""
                INSERT INTO rag.semantic_cache_entries (
                    id, corpus, access_scope_hash, filters_hash,
                    question_hash, question, answer, citations,
                    question_embedding, embedding_model, embedding_dimensions,
                    similarity_threshold, cached_at, expires_at
                )
                VALUES ($1, 'published', 'scope', NULL, $2, 'q', 'a', '[]'::jsonb,
                        ('[' || repeat('0.01,', {EMBEDDING_DIMENSIONS - 1}) || '0.01]')::vector,
                        $3, $4, 0.90, $5, $6)
                """,
                entry_id,
                f"hash-{entry_id}",
                EMBEDDING_MODEL,
                EMBEDDING_DIMENSIONS,
                cached_at,
                expires_at,
            )
            await connection.execute(
                """
                INSERT INTO rag.semantic_cache_sources (cache_entry_id, document_id, document_version_id)
                VALUES ($1, $2, $3)
                """,
                entry_id,
                uuid4(),
                uuid4(),
            )
        finally:
            await connection.close()

    async def chunk_ids(self) -> set[UUID]:
        return await self._fetch_ids("SELECT id FROM rag.document_chunks")

    async def cache_entry_ids(self) -> set[UUID]:
        return await self._fetch_ids("SELECT id FROM rag.semantic_cache_entries")

    async def cache_source_entry_ids(self) -> set[UUID]:
        return await self._fetch_ids("SELECT cache_entry_id AS id FROM rag.semantic_cache_sources")

    async def _fetch_ids(self, query: str) -> set[UUID]:
        connection = await asyncpg.connect(self.dsn)
        try:
            rows = await connection.fetch(query)
        finally:
            await connection.close()
        return {row["id"] for row in rows}


def _run_migrations(async_url: str) -> None:
    config = Config(str(SERVICE_ROOT / "alembic.ini"))
    config.set_main_option("sqlalchemy.url", async_url)
    command.upgrade(config, "head")
