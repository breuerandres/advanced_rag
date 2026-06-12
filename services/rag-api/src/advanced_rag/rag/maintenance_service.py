from __future__ import annotations

from pydantic import BaseModel, ConfigDict
from sqlalchemy import text
from sqlalchemy.ext.asyncio import AsyncSession, async_sessionmaker


class PurgeResult(BaseModel):
    model_config = ConfigDict(frozen=True)

    inactive_chunks_deleted: int
    cache_entries_deleted: int


class MaintenanceService:
    """Periodic hygiene for the rag schema, invoked via the internal API.

    Keeps the schema healthy under sustained load by deleting dead vectors (chunks
    deactivated by a re-index/supersede) once they are older than the retention window,
    and by deleting expired semantic cache entries (their `semantic_cache_sources` rows
    cascade with the entry). Invoked on demand by operators via the internal maintenance
    endpoint, consistent with the project's "internal endpoints over background daemons"
    stance.
    """

    def __init__(self, session_factory: async_sessionmaker[AsyncSession]) -> None:
        self._session_factory = session_factory

    async def purge(self, *, retention_days: int) -> PurgeResult:
        async with self._session_factory() as session:
            chunks = await session.execute(
                text(
                    """
                    delete from rag.document_chunks
                    where is_active = false
                      and created_at < now() - make_interval(days => :retention_days)
                    """
                ),
                {"retention_days": retention_days},
            )
            cache = await session.execute(
                text("delete from rag.semantic_cache_entries where expires_at <= now()")
            )
            await session.commit()
        return PurgeResult(
            inactive_chunks_deleted=int(getattr(chunks, "rowcount", 0) or 0),
            cache_entries_deleted=int(getattr(cache, "rowcount", 0) or 0),
        )
