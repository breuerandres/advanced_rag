"""Conversational memory: condense multi-turn history into a standalone question.

See docs/adr/0009-conversational-memory.md.

When the chat client passes `session_id`, the server loads the last N audit turns and
asks a cheap LLM to rewrite the new question as a standalone query. The rewritten
question drives retrieval and cache lookup; the original question still appears in the
final answer-generation prompt.
"""

from __future__ import annotations

from pathlib import Path
from uuid import UUID

from sqlalchemy import text
from sqlalchemy.ext.asyncio import AsyncConnection

from advanced_rag.providers.base import (
    ChatCompletionRequest,
    ChatMessage,
    ILlmProvider,
)


CONDENSER_PROMPT_DIR = Path(__file__).parent / "prompts"


def _load_condenser_prompt(locale: str) -> str:
    """Load the condenser prompt for the given locale, falling back to en-US."""
    for candidate in (locale, "en-US"):
        path = CONDENSER_PROMPT_DIR / f"condenser_{candidate}.md"
        if path.exists():
            return path.read_text(encoding="utf-8")
    raise FileNotFoundError(
        f"No condenser prompt found for locale '{locale}' nor for 'en-US' fallback "
        f"in {CONDENSER_PROMPT_DIR}"
    )


HISTORY_SQL = text(
    """
SELECT question, rewritten_question, answer, created_at
FROM rag.query_audit_events
WHERE session_id = :session_id
  AND user_id = :user_id
ORDER BY created_at ASC
LIMIT :limit
"""
)


async def load_session_history(
    connection: AsyncConnection,
    *,
    session_id: UUID,
    user_id: UUID,
    limit: int = 5,
) -> list[dict]:
    """Load previous turns for this session, oldest first.

    Returns a list of {question, rewritten_question, answer} dicts. Older turns are
    pruned beyond `limit` so token usage stays bounded.
    """
    result = await connection.execute(
        HISTORY_SQL,
        {"session_id": str(session_id), "user_id": str(user_id), "limit": limit},
    )
    return [
        {
            "question": row.question,
            "rewritten_question": row.rewritten_question,
            "answer": row.answer,
        }
        for row in result.fetchall()
    ]


async def condense_question(
    *,
    llm: ILlmProvider,
    history: list[dict],
    new_question: str,
    locale: str,
    condenser_model: str,
) -> str:
    """Rewrite `new_question` as a standalone query using prior turns.

    Returns the rewritten question. If history is empty or rewrite fails, returns the
    original question unchanged (caller should treat both equivalently).
    """
    if not history:
        return new_question

    prompt = _load_condenser_prompt(locale)
    history_text = "\n\n".join(
        f"User: {turn['question']}\nAssistant: {turn['answer']}"
        for turn in history
    )

    messages = [
        ChatMessage(role="system", content=prompt),
        ChatMessage(
            role="user",
            content=f"<history>\n{history_text}\n</history>\n\n<new_question>\n{new_question}\n</new_question>",
        ),
    ]
    req = ChatCompletionRequest(
        messages=messages,
        model=condenser_model,
        temperature=0.0,
        max_tokens=200,
    )
    try:
        content, _usage = await llm.chat_complete(req)
        rewritten = content.strip()
        if not rewritten:
            return new_question
        return rewritten
    except Exception:
        # Condensation is best-effort; on any failure fall back to the original.
        return new_question
