from __future__ import annotations

import asyncio
from pathlib import Path
from typing import Any
from uuid import uuid4

import asyncpg  # type: ignore[import-untyped]
from alembic import command
from alembic.config import Config
from testcontainers.postgres import PostgresContainer  # type: ignore[import-untyped]


SERVICE_ROOT = Path(__file__).resolve().parents[1]
REPO_ROOT = SERVICE_ROOT.parents[1]
POSTGRES_IMAGE = "pgvector/pgvector:pg16"
POSTGRES_USER = "postgres"
POSTGRES_PASSWORD = "postgres"
POSTGRES_DB = "advanced_rag_test"

EXPECTED_TABLES = {
    "indexing_jobs",
    "document_chunks",
    "semantic_cache_entries",
    "semantic_cache_sources",
    "query_audit_events",
    "query_audit_citations",
    "model_pricing",
    # Added by the v2 migration 20260522_120400.
    "unresolved_questions",
    # Added by the multimodal migration 20260611_130000.
    "document_chunk_images",
}

EXPECTED_INDEXES = {
    "ix_indexing_jobs_document_version_id",
    "ix_document_chunks_document_version_id",
    "ix_document_chunks_corpus_is_active",
    # Partial per-corpus HNSW indexes (migration 20260611_150000) replace the global one.
    "ix_document_chunks_embedding_hnsw_published",
    "ix_document_chunks_embedding_hnsw_preview",
    "ix_semantic_cache_entries_scope_lookup",
    "ix_query_audit_events_created_at",
    "ix_query_audit_events_user_created_at",
    "ix_query_audit_citations_document_id",
    # v2 indexes
    "ix_document_chunks_content_tsv",
    "ix_document_chunks_content_trgm",
    "ix_document_chunks_language",
    "ix_query_audit_session",
    "ix_query_audit_filters_hash",
    "ix_semantic_cache_corpus_scope_filters",
    "ix_unresolved_questions_cluster",
    "ix_unresolved_questions_status_created",
    "ix_unresolved_questions_embedding_hnsw",
    # multimodal migration 20260611_130000
    "ix_document_chunk_images_chunk_id",
    "ix_document_chunk_images_document_version_id",
}


def test_initial_alembic_migration_creates_owned_rag_schema() -> None:
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

        asyncio.run(_bootstrap_superuser_rag_schema(asyncpg_dsn))

        config = Config(str(SERVICE_ROOT / "alembic.ini"))
        config.set_main_option("sqlalchemy.url", async_url)

        command.upgrade(config, "head")

        state = asyncio.run(_read_database_state(asyncpg_dsn))

    assert state["rag_tables"] == EXPECTED_TABLES
    assert "app" not in state["schemas"]
    assert state["vector_extension_exists"] is True
    # v2 migration `20260522_120000_v2_change_embedding_dimensions` resizes the
    # embedding column to 1024 dims for multilingual support.
    assert state["document_chunks_embedding_type"] == "vector(1024)"
    assert EXPECTED_INDEXES.issubset(state["indexes"])
    # The global HNSW index is replaced by the partial per-corpus indexes.
    assert "ix_document_chunks_embedding_hnsw" not in state["indexes"]
    assert state["active_model_pricing"] == {
        ("gpt-4.1-nano", "chat"),
        ("text-embedding-3-small", "embedding"),
    }


def test_alembic_migration_runs_as_runtime_rag_owner_without_database_create_privilege() -> None:
    with PostgresContainer(
        image=POSTGRES_IMAGE,
        username=POSTGRES_USER,
        password=POSTGRES_PASSWORD,
        dbname=POSTGRES_DB,
    ) as postgres:
        host = postgres.get_container_host_ip()
        port = postgres.get_exposed_port(5432)
        superuser_dsn = _asyncpg_dsn(host, port)
        runtime_url = _runtime_sqlalchemy_url(host, port)
        asyncpg_dsn = _runtime_asyncpg_dsn(host, port)

        asyncio.run(_bootstrap_runtime_rag_schema(superuser_dsn))

        config = Config(str(SERVICE_ROOT / "alembic.ini"))
        config.set_main_option("sqlalchemy.url", runtime_url)

        command.upgrade(config, "head")

        state = asyncio.run(_read_database_state(asyncpg_dsn))

    assert state["rag_tables"] == EXPECTED_TABLES
    assert state["vector_extension_exists"] is True
    assert state["reporting_reader_can_select_views"] is True
    assert state["active_model_pricing"] == {
        ("gpt-4.1-nano", "chat"),
        ("text-embedding-3-small", "embedding"),
    }


def test_v2_embedding_dimension_migration_preserves_historical_chunk_references() -> None:
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

        asyncio.run(_bootstrap_superuser_rag_schema(asyncpg_dsn))

        config = Config(str(SERVICE_ROOT / "alembic.ini"))
        config.set_main_option("sqlalchemy.url", async_url)

        command.upgrade(config, "20260520_180000")
        seeded_ids = asyncio.run(_seed_mvp_chunk_cache_and_citation(asyncpg_dsn))

        command.upgrade(config, "head")

        state = asyncio.run(_read_v2_embedding_upgrade_state(asyncpg_dsn, seeded_ids["chunk_id"]))

    assert state["embedding_type"] == "vector(1024)"
    assert state["chunk_still_exists"] is True
    assert state["chunk_is_active"] is False
    assert state["chunk_embedding_is_null"] is True
    assert state["citation_still_references_chunk"] is True
    assert state["semantic_cache_entries"] == 0


def test_migrations_create_multimodal_image_reference_schema() -> None:
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

        asyncio.run(_bootstrap_superuser_rag_schema(asyncpg_dsn))

        config = Config(str(SERVICE_ROOT / "alembic.ini"))
        config.set_main_option("sqlalchemy.url", async_url)

        command.upgrade(config, "head")

        schema = asyncio.run(_read_multimodal_schema(asyncpg_dsn))

    assert schema["image_table"] is True
    assert schema["image_columns"] == [
        "id",
        "chunk_id",
        "document_id",
        "document_version_id",
        "image_id",
        "ordinal",
        "alt_text",
        "caption",
        "created_at",
    ]
    assert schema["audit_columns"] == [
        "multimodal_image_bytes_total",
        "multimodal_image_count",
        "multimodal_image_detail",
        "multimodal_image_ids",
        "multimodal_used",
    ]


async def _read_multimodal_schema(dsn: str) -> dict[str, object]:
    connection = await asyncpg.connect(dsn)
    try:
        image_table = await connection.fetchval(
            "select to_regclass('rag.document_chunk_images') is not null"
        )
        image_columns = await connection.fetch(
            """
            select column_name
            from information_schema.columns
            where table_schema = 'rag'
              and table_name = 'document_chunk_images'
            order by ordinal_position
            """
        )
        audit_columns = await connection.fetch(
            """
            select column_name
            from information_schema.columns
            where table_schema = 'rag'
              and table_name = 'query_audit_events'
              and column_name like 'multimodal_%'
            order by column_name
            """
        )
    finally:
        await connection.close()

    return {
        "image_table": bool(image_table),
        "image_columns": [row["column_name"] for row in image_columns],
        "audit_columns": [row["column_name"] for row in audit_columns],
    }


def test_postgres_init_does_not_grant_table_access_before_app_migrations() -> None:
    init_sql = (REPO_ROOT / "infra" / "compose" / "postgres-init" / "init.sql").read_text(
        encoding="utf-8"
    )

    assert "GRANT USAGE ON SCHEMA app TO %I" in init_sql
    assert "CREATE EXTENSION IF NOT EXISTS vector;" in init_sql
    assert "CREATE EXTENSION IF NOT EXISTS pg_trgm;" in init_sql
    assert "CREATE EXTENSION IF NOT EXISTS unaccent;" in init_sql
    assert "GRANT SELECT ON app.document_permissions" not in init_sql
    assert "GRANT SELECT ON app.user_ai_budget_limits" not in init_sql


def _async_sqlalchemy_url(host: str, port: str | int) -> str:
    return f"postgresql+asyncpg://{POSTGRES_USER}:{POSTGRES_PASSWORD}@{host}:{port}/{POSTGRES_DB}"


def _asyncpg_dsn(host: str, port: str | int) -> str:
    return f"postgresql://{POSTGRES_USER}:{POSTGRES_PASSWORD}@{host}:{port}/{POSTGRES_DB}"


def _runtime_sqlalchemy_url(host: str, port: str | int) -> str:
    return f"postgresql+asyncpg://rag_owner:rag-password@{host}:{port}/{POSTGRES_DB}"


def _runtime_asyncpg_dsn(host: str, port: str | int) -> str:
    return f"postgresql://rag_owner:rag-password@{host}:{port}/{POSTGRES_DB}"


async def _bootstrap_runtime_rag_schema(dsn: str) -> None:
    connection = await asyncpg.connect(dsn)
    try:
        # v2 BM25 migration depends on unaccent + pg_trgm; install them as superuser.
        await connection.execute("CREATE EXTENSION IF NOT EXISTS vector")
        await connection.execute("CREATE EXTENSION IF NOT EXISTS pg_trgm")
        await connection.execute("CREATE EXTENSION IF NOT EXISTS unaccent")
        await connection.execute("CREATE ROLE rag_owner LOGIN PASSWORD 'rag-password'")
        await connection.execute("CREATE ROLE app_reporting_reader LOGIN PASSWORD 'reporting-password'")
        await connection.execute("CREATE SCHEMA rag AUTHORIZATION rag_owner")
    finally:
        await connection.close()


async def _bootstrap_superuser_rag_schema(dsn: str) -> None:
    connection = await asyncpg.connect(dsn)
    try:
        await connection.execute("CREATE EXTENSION IF NOT EXISTS vector")
        await connection.execute("CREATE EXTENSION IF NOT EXISTS pg_trgm")
        await connection.execute("CREATE EXTENSION IF NOT EXISTS unaccent")
        await connection.execute("CREATE SCHEMA rag")
    finally:
        await connection.close()


async def _read_database_state(dsn: str) -> dict[str, Any]:
    connection = await asyncpg.connect(dsn)
    try:
        schemas = set(
            await connection.fetchval(
                """
                select array_agg(schema_name::text order by schema_name)
                from information_schema.schemata
                where schema_name in ('app', 'rag')
                """
            )
            or []
        )
        rag_tables = set(
            await connection.fetchval(
                """
                select array_agg(table_name::text order by table_name)
                from information_schema.tables
                where table_schema = 'rag'
                  and table_type = 'BASE TABLE'
                  and table_name <> 'alembic_version'
                """
            )
            or []
        )
        indexes = set(
            await connection.fetchval(
                """
                select array_agg(indexname::text order by indexname)
                from pg_indexes
                where schemaname = 'rag'
                """
            )
            or []
        )
        vector_extension_exists = await connection.fetchval(
            "select exists(select 1 from pg_extension where extname = 'vector')"
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
        reporting_reader_can_select_views = await connection.fetchval(
            """
            select case
                when to_regrole('app_reporting_reader') is null then false
                else
                    has_table_privilege(
                        'app_reporting_reader',
                        'rag.v_query_audit_with_citations',
                        'SELECT'
                    )
                    and has_table_privilege(
                        'app_reporting_reader',
                        'rag.v_feedback_summary',
                        'SELECT'
                    )
                end
            """
        )
        active_model_pricing = set(
            await connection.fetch(
                """
                select model_id, model_kind
                from rag.model_pricing
                where model_id in ('gpt-4.1-nano', 'text-embedding-3-small')
                  and effective_from <= now()
                  and (effective_to is null or effective_to > now())
                order by model_id, model_kind
                """
            )
        )
    finally:
        await connection.close()

    return {
        "schemas": schemas,
        "rag_tables": rag_tables,
        "indexes": indexes,
        "vector_extension_exists": vector_extension_exists,
        "document_chunks_embedding_type": embedding_type,
        "reporting_reader_can_select_views": reporting_reader_can_select_views,
        "active_model_pricing": {
            (row["model_id"], row["model_kind"]) for row in active_model_pricing
        },
    }


async def _seed_mvp_chunk_cache_and_citation(dsn: str) -> dict[str, Any]:
    connection = await asyncpg.connect(dsn)
    job_id = uuid4()
    document_id = uuid4()
    document_version_id = uuid4()
    chunk_id = uuid4()
    audit_id = uuid4()
    citation_id = uuid4()
    cache_id = uuid4()
    old_vector = _vector_literal(1536)
    try:
        await connection.execute(
            """
            insert into rag.indexing_jobs (
                id, document_id, document_version_id, corpus, status, attempts,
                chunker_version, embedding_dimensions
            )
            values ($1, $2, $3, 'published', 'Succeeded', 1, 1, 1536)
            """,
            job_id,
            document_id,
            document_version_id,
        )
        await connection.execute(
            """
            insert into rag.document_chunks (
                id, indexing_job_id, document_id, document_version_id, corpus,
                chunk_index, heading_path, token_count, char_count, content,
                content_html, embedding, embedding_model, is_active
            )
            values (
                $1, $2, $3, $4, 'published', 0, array['Existing'], 10, 42,
                'existing content', '<p>existing content</p>', $5::vector,
                'text-embedding-3-small', true
            )
            """,
            chunk_id,
            job_id,
            document_id,
            document_version_id,
            old_vector,
        )
        await connection.execute(
            """
            insert into rag.query_audit_events (
                id, user_id, request_id, question, answer, cache_hit,
                embedding_model, embedding_dimensions, input_tokens,
                cached_tokens, output_tokens, estimated_cost_usd, latency_ms,
                access_scope_hash, corpus, prompt_version, chunker_version
            )
            values (
                $1, $2, 'req-existing', 'question', 'answer', false,
                'text-embedding-3-small', 1536, 1, 0, 1, 0.00000001, 25,
                'scope', 'published', 1, 1
            )
            """,
            audit_id,
            uuid4(),
        )
        await connection.execute(
            """
            insert into rag.query_audit_citations (
                id, query_audit_event_id, chunk_id, document_id,
                document_version_id, heading_path
            )
            values ($1, $2, $3, $4, $5, array['Existing'])
            """,
            citation_id,
            audit_id,
            chunk_id,
            document_id,
            document_version_id,
        )
        await connection.execute(
            """
            insert into rag.semantic_cache_entries (
                id, corpus, access_scope_hash, question_hash, question, answer,
                question_embedding, embedding_model, embedding_dimensions,
                similarity_threshold, cached_at, expires_at
            )
            values (
                $1, 'published', 'scope', 'question-hash', 'question', 'answer',
                $2::vector, 'text-embedding-3-small', 1536, 0.9000,
                now(), now() + interval '1 hour'
            )
            """,
            cache_id,
            old_vector,
        )
        await connection.execute(
            """
            insert into rag.semantic_cache_sources (
                cache_entry_id, document_id, document_version_id
            )
            values ($1, $2, $3)
            """,
            cache_id,
            document_id,
            document_version_id,
        )
    finally:
        await connection.close()

    return {"chunk_id": chunk_id}


async def _read_v2_embedding_upgrade_state(dsn: str, chunk_id: Any) -> dict[str, Any]:
    connection = await asyncpg.connect(dsn)
    try:
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
        chunk = await connection.fetchrow(
            """
            select is_active, embedding is null as embedding_is_null
            from rag.document_chunks
            where id = $1
            """,
            chunk_id,
        )
        citation_still_references_chunk = await connection.fetchval(
            """
            select exists(
                select 1 from rag.query_audit_citations where chunk_id = $1
            )
            """,
            chunk_id,
        )
        semantic_cache_entries = await connection.fetchval(
            "select count(*) from rag.semantic_cache_entries"
        )
    finally:
        await connection.close()

    return {
        "embedding_type": embedding_type,
        "chunk_still_exists": chunk is not None,
        "chunk_is_active": bool(chunk["is_active"]) if chunk is not None else None,
        "chunk_embedding_is_null": bool(chunk["embedding_is_null"]) if chunk is not None else None,
        "citation_still_references_chunk": citation_still_references_chunk,
        "semantic_cache_entries": semantic_cache_entries,
    }


def _vector_literal(dimensions: int) -> str:
    return "[" + ",".join("0.001" for _ in range(dimensions)) + "]"
