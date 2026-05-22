"""Generate the chat answer with structured citations from retrieved chunks.

Bridges the provider-agnostic `ILlmProvider.chat_complete` to the domain need of
returning `{answer, cited_chunk_ids}` JSON. The system prompt is loaded per locale
from `rag/prompts/system_<locale>.md` and the user message embeds the context plus
explicit instructions for the JSON shape.

See docs/adr/0001-multi-provider-llm.md.
"""

from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path
from typing import Any
from uuid import UUID

from advanced_rag.providers.base import (
    ChatCompletionRequest,
    ChatMessage,
    ChatUsage,
    ILlmProvider,
)


SYSTEM_PROMPT_DIR = Path(__file__).parent / "prompts"


def load_system_prompt(locale: str) -> str:
    """Load the system prompt for the given locale, falling back to en-US.

    Raises FileNotFoundError if neither the requested locale nor en-US is available.
    """
    for candidate in (locale, "en-US"):
        path = SYSTEM_PROMPT_DIR / f"system_{candidate}.md"
        if path.exists():
            return path.read_text(encoding="utf-8")
    raise FileNotFoundError(
        f"No system prompt found for locale {locale!r} nor for 'en-US' fallback "
        f"in {SYSTEM_PROMPT_DIR}"
    )


@dataclass(frozen=True)
class AnswerGeneration:
    """Result of a single answer generation call.

    `cited_chunk_ids` is a sub-set of the IDs passed in via `chunks`; the chat_service
    re-hydrates these into full `Citation` records using the chunk metadata it already
    holds.
    """

    answer: str
    cited_chunk_ids: list[UUID]
    usage: ChatUsage


JSON_INSTRUCTION = (
    "Respond strictly with a JSON object of this shape:\n"
    '{"answer": "<text answer>", "cited_chunk_ids": ["<chunk_id_1>", "<chunk_id_2>"]}\n'
    "Each chunk_id MUST be one of the IDs listed in the context. Include only the IDs "
    "you actually used; if none, return an empty list."
)


def _format_context(chunks: list[Any]) -> str:
    parts: list[str] = []
    for chunk in chunks:
        heading_path = list(getattr(chunk, "heading_path", []) or [])
        heading = " / ".join(heading_path) if heading_path else "(untitled)"
        parts.append(
            f"[chunk_id={chunk.id} document_id={chunk.document_id} heading={heading}]\n"
            f"{chunk.content}"
        )
    return "\n\n".join(parts)


async def generate_answer(
    *,
    llm: ILlmProvider,
    question: str,
    chunks: list[Any],
    locale: str,
    model: str,
    temperature: float = 0.1,
    max_tokens: int = 900,
) -> AnswerGeneration:
    """Ask the configured LLM for an answer constrained to the retrieved context.

    `chunks` items must expose `id`, `document_id`, `content`, and `heading_path` (the
    `chat_service.RetrievedChunk` model satisfies this). On JSON parse failure the
    raw content is returned as the answer with no citations — the caller can decide
    whether to retry or surface it as-is.
    """
    system = load_system_prompt(locale)
    user_content = (
        f"Context:\n{_format_context(chunks)}\n\n"
        f"Question:\n{question}\n\n"
        f"{JSON_INSTRUCTION}"
    )
    req = ChatCompletionRequest(
        messages=[
            ChatMessage(role="system", content=system),
            ChatMessage(role="user", content=user_content),
        ],
        model=model,
        temperature=temperature,
        max_tokens=max_tokens,
        response_format={"type": "json_object"},
    )
    content, usage = await llm.chat_complete(req)
    answer, cited = _parse_answer(content)
    return AnswerGeneration(answer=answer, cited_chunk_ids=cited, usage=usage)


def _parse_answer(content: str) -> tuple[str, list[UUID]]:
    if not content:
        return ("", [])
    try:
        parsed = json.loads(content)
    except json.JSONDecodeError:
        return (content, [])
    answer = str(parsed.get("answer", ""))
    raw_ids = parsed.get("cited_chunk_ids", []) or []
    cited: list[UUID] = []
    for item in raw_ids:
        try:
            cited.append(UUID(str(item)))
        except (ValueError, TypeError):
            continue
    return (answer, cited)
