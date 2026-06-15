from __future__ import annotations

import time
from asyncio import Lock
from collections.abc import Mapping
from typing import Any

from sqlalchemy import text
from sqlalchemy.ext.asyncio import AsyncSession

from advanced_rag.core.config import Settings


class TenantConfigRefresher:
    """Refresh the in-memory Settings from app.tenant_config on a short TTL.

    .NET owns app.tenant_config; rag-api reads it through a read-only grant. Mutating the
    shared Settings instance lets every existing `settings.*` read in ChatService pick up
    admin edits without per-call-site changes. A single refresh happens at the start of
    each chat request (ChatService.precheck), so values are consistent within a request.
    """

    _SELECT = text(
        """
        select llm_model,
               cache_ttl_hours,
               cache_similarity_threshold,
               default_monthly_budget_usd,
               customer_timezone,
               chat_max_question_chars
        from app.tenant_config
        order by created_at
        limit 1
        """
    )

    def __init__(self, settings: Settings, ttl_seconds: int = 30) -> None:
        self._settings = settings
        self._ttl_seconds = ttl_seconds
        self._last_refresh: float | None = None
        self._lock = Lock()

    async def refresh_if_stale(self, session: AsyncSession) -> None:
        if not self._is_stale():
            return
        async with self._lock:
            if not self._is_stale():
                return
            row = (await session.execute(self._SELECT)).mappings().first()
            if row is not None:
                self._apply(dict(row))
            self._last_refresh = time.monotonic()

    def _is_stale(self) -> bool:
        return self._last_refresh is None or (time.monotonic() - self._last_refresh) >= self._ttl_seconds

    def _apply(self, row: Mapping[str, Any]) -> None:
        s = self._settings
        if row["llm_model"]:
            s.openai_chat_model = str(row["llm_model"])
        s.rag_semantic_cache_ttl_hours = int(row["cache_ttl_hours"])
        s.rag_semantic_cache_similarity_threshold = float(row["cache_similarity_threshold"])
        s.default_monthly_ai_budget_usd = float(row["default_monthly_budget_usd"])
        if row["customer_timezone"]:
            s.customer_timezone = str(row["customer_timezone"])
        s.chat_max_question_chars = int(row["chat_max_question_chars"])
