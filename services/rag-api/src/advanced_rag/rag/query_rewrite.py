"""Optional query rewrite / expansion using a cheap LLM.

See docs/v2/03-phases.md Phase 5.3. Default tenant_config has this disabled
(`enable_query_rewrite=false`); enable to see if it helps retrieval recall on the
golden set before turning it on by default.
"""

from __future__ import annotations

from pathlib import Path

from advanced_rag.providers.base import (
    ChatCompletionRequest,
    ChatMessage,
    ILlmProvider,
)


REWRITER_PROMPT_DIR = Path(__file__).parent / "prompts"


def _load_rewriter_prompt(locale: str) -> str:
    for candidate in (locale, "en-US"):
        path = REWRITER_PROMPT_DIR / f"rewriter_{candidate}.md"
        if path.exists():
            return path.read_text(encoding="utf-8")
    raise FileNotFoundError(
        f"No rewriter prompt found for locale '{locale}' nor for 'en-US' fallback"
    )


async def rewrite_query(
    *,
    llm: ILlmProvider,
    question: str,
    locale: str,
    rewriter_model: str,
    max_rewrites: int = 3,
) -> list[str]:
    """Generate up to `max_rewrites` reformulations of the question.

    The reformulations are added to the retrieval candidate pool (vector + BM25) so the
    final RRF combines results across multiple paraphrases.

    Returns a list whose first element is the original question and subsequent elements
    are reformulations. Always returns at least the original question.
    """
    if not question.strip():
        return [question]

    prompt = _load_rewriter_prompt(locale).replace("{{N}}", str(max_rewrites))

    messages = [
        ChatMessage(role="system", content=prompt),
        ChatMessage(role="user", content=question),
    ]
    req = ChatCompletionRequest(
        messages=messages,
        model=rewriter_model,
        temperature=0.4,
        max_tokens=200,
    )
    try:
        content, _ = await llm.chat_complete(req)
    except Exception:
        return [question]

    rewrites = [
        line.strip().lstrip("0123456789.-) ")
        for line in content.splitlines()
        if line.strip()
    ]
    rewrites = [r for r in rewrites if r and r != question][:max_rewrites]
    return [question, *rewrites]
