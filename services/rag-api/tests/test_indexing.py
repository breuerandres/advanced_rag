from __future__ import annotations

import asyncio
from pathlib import Path
from typing import Any
from uuid import UUID, uuid4

import asyncpg  # type: ignore[import-untyped]
from alembic import command
from alembic.config import Config
from fastapi.testclient import TestClient
from testcontainers.postgres import PostgresContainer  # type: ignore[import-untyped]

from advanced_rag.core.config import Settings
from advanced_rag.main import create_app


SERVICE_ROOT = Path(__file__).resolve().parents[1]
POSTGRES_IMAGE = "pgvector/pgvector:pg16"
POSTGRES_USER = "postgres"
POSTGRES_PASSWORD = "postgres"
POSTGRES_DB = "advanced_rag_indexing_test"
INTERNAL_TOKEN = "test-internal-token"


def test_internal_indexing_rejects_missing_or_invalid_service_token() -> None:
    app = create_app(
        Settings(
            rag_database_url="postgresql+asyncpg://postgres:postgres@localhost:5432/unused",
            internal_service_token=INTERNAL_TOKEN,
        )
    )
    client = TestClient(app)

    missing = client.post("/internal/indexing-jobs", json=_indexing_payload())
    invalid = client.post(
        "/internal/indexing-jobs",
        json=_indexing_payload(),
        headers={"X-Internal-Service-Token": "wrong"},
    )

    assert missing.status_code == 401
    assert missing.json()["error"]["code"] == "AUTH_INTERNAL_TOKEN_INVALID"
    assert invalid.status_code == 401
    assert invalid.json()["error"]["code"] == "AUTH_INTERNAL_TOKEN_INVALID"


def test_valid_indexing_request_persists_job_chunks_and_embedding_dimensions() -> None:
    with PostgresContainer(
        image=POSTGRES_IMAGE,
        username=POSTGRES_USER,
        password=POSTGRES_PASSWORD,
        dbname=POSTGRES_DB,
    ) as postgres:
        host = postgres.get_container_host_ip()
        port = postgres.get_exposed_port(5432)
        async_url = _async_sqlalchemy_url(host, port)
        asyncpg_dsn = _asyncpg_dsn(host, port)
        asyncio.run(_bootstrap_rag_schema(asyncpg_dsn))
        _run_migrations(async_url)

        embedding_provider = FakeEmbeddingProvider()
        app = create_app(
            Settings(
                rag_database_url=async_url,
                internal_service_token=INTERNAL_TOKEN,
                openai_embedding_model="text-embedding-3-large",
                openai_embedding_dimensions=1536,
            ),
            embedding_provider=embedding_provider,
        )
        client = TestClient(app)

        response = client.post(
            "/internal/indexing-jobs",
            json=_indexing_payload(),
            headers={"X-Internal-Service-Token": INTERNAL_TOKEN},
        )

        assert response.status_code == 200
        body = response.json()
        assert body["status"] == "Succeeded"
        assert body["chunkCount"] >= 1
        assert embedding_provider.calls == [
            EmbeddingCall(model="text-embedding-3-large", dimensions=1536)
        ]
        state = asyncio.run(_read_indexing_state(asyncpg_dsn, UUID(body["jobId"])))

    assert state["job_status"] == "Succeeded"
    assert state["chunk_count"] == body["chunkCount"]
    assert state["embedding_type"] == "vector(1536)"
    assert state["active_chunk_count_for_version"] == body["chunkCount"]


def _indexing_payload() -> dict[str, str]:
    return {
        "instructionId": str(uuid4()),
        "instructionVersionId": str(uuid4()),
        "contentHtml": "<h1>Safety</h1><p>Wear visible credentials.</p>",
        "corpusMode": "published",
    }


def _run_migrations(async_url: str) -> None:
    config = Config(str(SERVICE_ROOT / "alembic.ini"))
    config.set_main_option("sqlalchemy.url", async_url)
    command.upgrade(config, "head")


def _async_sqlalchemy_url(host: str, port: str | int) -> str:
    return f"postgresql+asyncpg://{POSTGRES_USER}:{POSTGRES_PASSWORD}@{host}:{port}/{POSTGRES_DB}"


def _asyncpg_dsn(host: str, port: str | int) -> str:
    return f"postgresql://{POSTGRES_USER}:{POSTGRES_PASSWORD}@{host}:{port}/{POSTGRES_DB}"


async def _read_indexing_state(dsn: str, job_id: UUID) -> dict[str, Any]:
    connection = await asyncpg.connect(dsn)
    try:
        job = await connection.fetchrow(
            """
            select status, instruction_version_id
            from rag.indexing_jobs
            where id = $1
            """,
            job_id,
        )
        chunk_count = await connection.fetchval(
            "select count(*) from rag.document_chunks where indexing_job_id = $1",
            job_id,
        )
        active_count = await connection.fetchval(
            """
            select count(*)
            from rag.document_chunks
            where instruction_version_id = $1 and is_active = true
            """,
            job["instruction_version_id"],
        )
        embedding_type = await connection.fetchval(
            """
            select format_type(attribute.atttypid, attribute.atttypmod)
            from pg_attribute attribute
            join pg_class class on class.oid = attribute.attrelid
            join pg_namespace namespace on namespace.oid = class.relnamespace
            where namespace.nspname = 'rag'
              and class.relname = 'document_chunks'
              and attribute.attname = 'embedding'
              and attribute.attnum > 0
            """
        )
    finally:
        await connection.close()

    return {
        "job_status": job["status"],
        "chunk_count": chunk_count,
        "active_chunk_count_for_version": active_count,
        "embedding_type": embedding_type,
    }


async def _bootstrap_rag_schema(dsn: str) -> None:
    connection = await asyncpg.connect(dsn)
    try:
        await connection.execute("CREATE EXTENSION IF NOT EXISTS vector")
        await connection.execute("CREATE SCHEMA rag")
    finally:
        await connection.close()


class EmbeddingCall:
    def __init__(self, *, model: str, dimensions: int) -> None:
        self.model = model
        self.dimensions = dimensions

    def __eq__(self, other: object) -> bool:
        return (
            isinstance(other, EmbeddingCall)
            and self.model == other.model
            and self.dimensions == other.dimensions
        )


class FakeEmbeddingProvider:
    def __init__(self) -> None:
        self.calls: list[EmbeddingCall] = []

    async def embed_texts(
        self,
        texts: list[str],
        *,
        model: str,
        dimensions: int,
    ) -> list[list[float]]:
        self.calls.append(EmbeddingCall(model=model, dimensions=dimensions))
        return [[0.01] * dimensions for _ in texts]
