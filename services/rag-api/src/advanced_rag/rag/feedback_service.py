from __future__ import annotations

import re
from datetime import UTC, datetime
from uuid import UUID

from sqlalchemy import text
from sqlalchemy.ext.asyncio import AsyncSession, async_sessionmaker

from advanced_rag.core.errors import ApiException


class FeedbackService:
    def __init__(self, session_factory: async_sessionmaker[AsyncSession]) -> None:
        self._session_factory = session_factory

    async def submit_feedback(
        self,
        *,
        query_audit_event_id: UUID,
        user_id: UUID,
        value: str,
        comment: str | None,
    ) -> str | None:
        sanitized_comment = _sanitize_comment(comment)
        async with self._session_factory() as session:
            result = await session.execute(
                text(
                    """
                    update rag.query_audit_events
                    set feedback_value = :feedback_value,
                        feedback_comment = :feedback_comment,
                        feedback_updated_at = :feedback_updated_at
                    where id = :id
                      and user_id = :user_id
                    """
                ),
                {
                    "id": query_audit_event_id,
                    "user_id": user_id,
                    "feedback_value": value,
                    "feedback_comment": sanitized_comment,
                    "feedback_updated_at": datetime.now(UTC),
                },
            )
            if int(getattr(result, "rowcount", 0) or 0) != 1:
                await session.rollback()
                raise ApiException("NOT_FOUND", 404, "Query audit event not found.")
            await session.commit()
        return sanitized_comment


def _sanitize_comment(comment: str | None) -> str | None:
    if comment is None:
        return None
    without_tags = re.sub(r"<[^>]*>", "", comment)
    normalized = " ".join(without_tags.split())
    if not normalized:
        return None
    return normalized[:1000]
