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
from advanced_rag.providers.base import ChatUsage


SERVICE_ROOT = Path(__file__).resolve().parents[1]
POSTGRES_IMAGE = "pgvector/pgvector:pg16"
POSTGRES_USER = "postgres"
POSTGRES_PASSWORD = "postgres"
POSTGRES_DB = "advanced_rag_indexing_test"
INTERNAL_TOKEN = "test-internal-token"

EMBEDDING_DIMENSIONS = 1024
EMBEDDING_MODEL = "text-embedding-3-large"


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
                openai_embedding_model=EMBEDDING_MODEL,
                openai_embedding_dimensions=EMBEDDING_DIMENSIONS,
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
        assert len(embedding_provider.calls) == 1
        # The fake records the texts it embedded; we only care that the chunker
        # produced something for the seeded HTML.
        assert all(text for text in embedding_provider.calls[0])
        state = asyncio.run(_read_indexing_state(asyncpg_dsn, UUID(body["jobId"])))

    assert state["job_status"] == "Succeeded"
    assert state["chunk_count"] == body["chunkCount"]
    assert state["embedding_type"] == f"vector({EMBEDDING_DIMENSIONS})"
    assert state["active_chunk_count_for_version"] == body["chunkCount"]
    assert state["job_embedding_model"] == EMBEDDING_MODEL


def test_new_version_deactivates_previous_version_chunks() -> None:
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

        app = create_app(
            Settings(
                rag_database_url=async_url,
                internal_service_token=INTERNAL_TOKEN,
                openai_embedding_model=EMBEDDING_MODEL,
                openai_embedding_dimensions=EMBEDDING_DIMENSIONS,
            ),
            embedding_provider=FakeEmbeddingProvider(),
        )
        client = TestClient(app)
        headers = {"X-Internal-Service-Token": INTERNAL_TOKEN}

        document_id = uuid4()
        version_v1 = uuid4()
        version_v2 = uuid4()
        other_document_id = uuid4()
        other_version = uuid4()

        # A different document indexed first must stay active after D is reindexed.
        other = client.post(
            "/internal/indexing-jobs",
            json={
                "documentId": str(other_document_id),
                "documentVersionId": str(other_version),
                "contentHtml": "<h1>Other</h1><p>Unrelated content.</p>",
                "corpusMode": "published",
            },
            headers=headers,
        )
        assert other.status_code == 200

        first = client.post(
            "/internal/indexing-jobs",
            json={
                "documentId": str(document_id),
                "documentVersionId": str(version_v1),
                "contentHtml": "<h1>Safety</h1><p>Wear visible credentials.</p>",
                "corpusMode": "published",
            },
            headers=headers,
        )
        assert first.status_code == 200
        assert first.json()["status"] == "Succeeded"

        second = client.post(
            "/internal/indexing-jobs",
            json={
                "documentId": str(document_id),
                "documentVersionId": str(version_v2),
                "contentHtml": "<h1>Safety</h1><p>Always wear visible credentials.</p>",
                "corpusMode": "published",
            },
            headers=headers,
        )
        assert second.status_code == 200
        assert second.json()["status"] == "Succeeded"

        v1_active = asyncio.run(_count_active_chunks_for_version(asyncpg_dsn, version_v1))
        v2_active = asyncio.run(_count_active_chunks_for_version(asyncpg_dsn, version_v2))
        other_active = asyncio.run(_count_active_chunks_for_version(asyncpg_dsn, other_version))

    assert v1_active == 0
    assert v2_active >= 1
    assert other_active >= 1


async def _count_active_chunks_for_version(dsn: str, version_id: UUID) -> int:
    connection = await asyncpg.connect(dsn)
    try:
        return await connection.fetchval(
            """
            select count(*)
            from rag.document_chunks
            where document_version_id = $1 and is_active = true
            """,
            version_id,
        )
    finally:
        await connection.close()


def test_valid_indexing_request_persists_chunk_image_references() -> None:
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

        app = create_app(
            Settings(
                rag_database_url=async_url,
                internal_service_token=INTERNAL_TOKEN,
                openai_embedding_model=EMBEDDING_MODEL,
                openai_embedding_dimensions=EMBEDDING_DIMENSIONS,
            ),
            embedding_provider=FakeEmbeddingProvider(),
        )
        client = TestClient(app)

        response = client.post(
            "/internal/indexing-jobs",
            json=_indexing_payload_with_image(),
            headers={"X-Internal-Service-Token": INTERNAL_TOKEN},
        )

        assert response.status_code == 200
        body = response.json()
        assert body["status"] == "Succeeded"
        image_rows = asyncio.run(_read_chunk_images(asyncpg_dsn, UUID(body["jobId"])))

    assert image_rows == [
        {
            "image_id": UUID("11111111-1111-1111-1111-111111111111"),
            "ordinal": 0,
            "alt_text": "Breaker panel with red emergency switch",
            "caption": None,
        }
    ]


def _indexing_payload_with_image() -> dict[str, str]:
    return {
        "documentId": str(uuid4()),
        "documentVersionId": str(uuid4()),
        "contentHtml": (
            "<h1>Safety panel</h1>"
            "<figure>"
            '<img src="/api/document-images/11111111-1111-1111-1111-111111111111/content" '
            'alt="Breaker panel with red emergency switch" />'
            "<figcaption>North wall panel.</figcaption>"
            "</figure>"
        ),
        "corpusMode": "published",
    }


async def _read_chunk_images(dsn: str, job_id: UUID) -> list[dict[str, Any]]:
    connection = await asyncpg.connect(dsn)
    try:
        rows = await connection.fetch(
            """
            select image.image_id, image.ordinal, image.alt_text, image.caption
            from rag.document_chunk_images image
            join rag.document_chunks chunk on chunk.id = image.chunk_id
            where chunk.indexing_job_id = $1
            order by image.ordinal
            """,
            job_id,
        )
    finally:
        await connection.close()
    return [dict(row) for row in rows]


def _indexing_payload() -> dict[str, str]:
    return {
        "documentId": str(uuid4()),
        "documentVersionId": str(uuid4()),
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
            select status, document_version_id, embedding_model
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
            where document_version_id = $1 and is_active = true
            """,
            job["document_version_id"],
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
        "job_embedding_model": job["embedding_model"],
    }


async def _bootstrap_rag_schema(dsn: str) -> None:
    connection = await asyncpg.connect(dsn)
    try:
        # v2 BM25 migration creates `rag.f_immutable_unaccent` wrapping `public.unaccent`,
        # and the hybrid retrieval SQL uses `pg_trgm`. Without these extensions Alembic
        # `upgrade head` fails when the BM25 migration runs.
        await connection.execute("CREATE EXTENSION IF NOT EXISTS vector")
        await connection.execute("CREATE EXTENSION IF NOT EXISTS pg_trgm")
        await connection.execute("CREATE EXTENSION IF NOT EXISTS unaccent")
        await connection.execute("CREATE SCHEMA rag")
    finally:
        await connection.close()


class FakeEmbeddingProvider:
    """`IEmbeddingProvider` test double for indexing tests.

    Carries its own `model` / `dimensions` (the provider abstraction makes these
    intrinsic, no longer a per-call argument), and returns a constant vector
    shaped to match the schema column.
    """

    name = "fake"
    model = EMBEDDING_MODEL
    dimensions = EMBEDDING_DIMENSIONS

    def __init__(self) -> None:
        self.calls: list[list[str]] = []

    async def embed(self, texts: list[str]) -> tuple[list[list[float]], ChatUsage]:
        self.calls.append(list(texts))
        return [[0.01] * self.dimensions for _ in texts], ChatUsage(input_tokens=4)
