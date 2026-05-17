from __future__ import annotations

import asyncio
from pathlib import Path
from typing import Any

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
}

EXPECTED_INDEXES = {
    "ix_indexing_jobs_instruction_version_id",
    "ix_document_chunks_instruction_version_id",
    "ix_document_chunks_corpus_is_active",
    "ix_document_chunks_embedding_hnsw",
    "ix_semantic_cache_entries_scope_lookup",
    "ix_query_audit_events_created_at",
    "ix_query_audit_events_user_created_at",
    "ix_query_audit_citations_instruction_id",
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
    assert state["document_chunks_embedding_type"] == "vector(1536)"
    assert EXPECTED_INDEXES.issubset(state["indexes"])


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


def test_postgres_init_grants_rag_owner_read_only_access_to_approved_app_tables() -> None:
    init_sql = (REPO_ROOT / "infra" / "compose" / "postgres-init" / "init.sql").read_text(
        encoding="utf-8"
    )

    assert "GRANT USAGE ON SCHEMA app TO %I" in init_sql
    assert "GRANT SELECT ON app.instruction_permissions TO %I" in init_sql
    assert "GRANT SELECT ON app.user_ai_budget_limits TO %I" in init_sql


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
        await connection.execute("CREATE EXTENSION IF NOT EXISTS vector")
        await connection.execute("CREATE ROLE rag_owner LOGIN PASSWORD 'rag-password'")
        await connection.execute("CREATE SCHEMA rag AUTHORIZATION rag_owner")
    finally:
        await connection.close()


async def _bootstrap_superuser_rag_schema(dsn: str) -> None:
    connection = await asyncpg.connect(dsn)
    try:
        await connection.execute("CREATE EXTENSION IF NOT EXISTS vector")
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
    finally:
        await connection.close()

    return {
        "schemas": schemas,
        "rag_tables": rag_tables,
        "indexes": indexes,
        "vector_extension_exists": vector_extension_exists,
        "document_chunks_embedding_type": embedding_type,
    }
