from __future__ import annotations

from pydantic import BaseModel, ConfigDict
from sqlalchemy import text
from sqlalchemy.ext.asyncio import AsyncEngine

from advanced_rag.core.config import Settings


class ReadinessResult(BaseModel):
    model_config = ConfigDict(frozen=True)

    is_ready: bool
    failed_checks: list[str]


class OperationalReadinessChecker:
    def __init__(self, settings: Settings, engine: AsyncEngine) -> None:
        self._settings = settings
        self._engine = engine

    async def check(self) -> ReadinessResult:
        failed: list[str] = []

        if not await self._database_ready():
            failed.append("database")

        if not self._settings.resolved_openai_api_key:
            failed.append("openai_api_key")

        if not self._settings.resolved_internal_service_token:
            failed.append("internal_service_token")

        return ReadinessResult(is_ready=not failed, failed_checks=failed)

    async def _database_ready(self) -> bool:
        try:
            async with self._engine.connect() as connection:
                await connection.execute(text("select 1"))
                extension = await connection.execute(
                    text("select 1 from pg_extension where extname = 'vector'")
                )
                return extension.scalar_one_or_none() == 1
        except Exception:
            return False
