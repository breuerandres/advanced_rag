from __future__ import annotations

from collections.abc import AsyncIterator

import asyncpg  # type: ignore[import-untyped]
import pytest
import pytest_asyncio
from sqlalchemy import text
from sqlalchemy.ext.asyncio import AsyncSession, async_sessionmaker, create_async_engine
from testcontainers.postgres import PostgresContainer  # type: ignore[import-untyped]

from advanced_rag.core.config import Settings
from advanced_rag.core.tenant_config_refresher import TenantConfigRefresher

POSTGRES_IMAGE = "pgvector/pgvector:pg16"
POSTGRES_USER = "postgres"
POSTGRES_PASSWORD = "postgres"
POSTGRES_DB = "advanced_rag_tenant_config_test"


@pytest_asyncio.fixture
async def tenant_config_session_factory() -> AsyncIterator[async_sessionmaker[AsyncSession]]:
    with PostgresContainer(
        image=POSTGRES_IMAGE,
        username=POSTGRES_USER,
        password=POSTGRES_PASSWORD,
        dbname=POSTGRES_DB,
    ) as container:
        host = container.get_container_host_ip()
        port = container.get_exposed_port(5432)
        async_url = f"postgresql+asyncpg://{POSTGRES_USER}:{POSTGRES_PASSWORD}@{host}:{port}/{POSTGRES_DB}"
        dsn = f"postgresql://{POSTGRES_USER}:{POSTGRES_PASSWORD}@{host}:{port}/{POSTGRES_DB}"

        connection = await asyncpg.connect(dsn)
        try:
            await connection.execute("CREATE SCHEMA app")
            await connection.execute(
                """
                CREATE TABLE app.tenant_config (
                    llm_model text not null,
                    cache_ttl_hours int not null,
                    cache_similarity_threshold numeric(4, 3) not null,
                    default_monthly_budget_usd numeric(8, 2) not null,
                    customer_timezone text not null,
                    chat_max_question_chars int not null,
                    created_at timestamptz not null default now()
                )
                """
            )
            await connection.execute(
                """
                INSERT INTO app.tenant_config (
                    llm_model, cache_ttl_hours, cache_similarity_threshold,
                    default_monthly_budget_usd, customer_timezone, chat_max_question_chars
                )
                VALUES ('gpt-4.1-mini', 48, 0.80, 9.00, 'UTC', 2500)
                """
            )
        finally:
            await connection.close()

        engine = create_async_engine(async_url)
        try:
            yield async_sessionmaker(engine, expire_on_commit=False)
        finally:
            await engine.dispose()


@pytest.mark.asyncio
async def test_refresh_applies_tenant_config(
    tenant_config_session_factory: async_sessionmaker[AsyncSession],
) -> None:
    settings = Settings(openai_chat_model="gpt-4.1-nano")
    refresher = TenantConfigRefresher(settings, ttl_seconds=30)

    async with tenant_config_session_factory() as session:
        await refresher.refresh_if_stale(session)

    assert settings.resolved_chat_model == "gpt-4.1-mini"
    assert settings.rag_semantic_cache_ttl_hours == 48
    assert settings.rag_semantic_cache_similarity_threshold == pytest.approx(0.80)
    assert settings.default_monthly_ai_budget_usd == pytest.approx(9.00)
    assert settings.customer_timezone == "UTC"
    assert settings.chat_max_question_chars == 2500


@pytest.mark.asyncio
async def test_refresh_is_skipped_within_ttl(
    tenant_config_session_factory: async_sessionmaker[AsyncSession],
) -> None:
    settings = Settings(openai_chat_model="gpt-4.1-nano")
    refresher = TenantConfigRefresher(settings, ttl_seconds=1000)
    async with tenant_config_session_factory() as session:
        await refresher.refresh_if_stale(session)  # first read applies
        await session.execute(text("update app.tenant_config set llm_model = 'changed'"))
        await session.commit()
        await refresher.refresh_if_stale(session)  # within TTL -> no re-read
    assert settings.resolved_chat_model != "changed"
