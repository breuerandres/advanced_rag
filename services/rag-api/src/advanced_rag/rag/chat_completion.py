from __future__ import annotations

import json
from typing import Any, Protocol
from uuid import UUID

from openai import AsyncOpenAI
from pydantic import BaseModel, ConfigDict


class ChatCompletionResult(BaseModel):
    model_config = ConfigDict(frozen=True)

    answer: str
    cited_chunk_ids: list[UUID]
    input_tokens: int
    output_tokens: int


class ChatCompletionProvider(Protocol):
    async def complete(
        self,
        *,
        question: str,
        context_chunks: list[Any],
        model: str,
    ) -> ChatCompletionResult:
        pass


class OpenAIChatCompletionProvider:
    def __init__(self, api_key: str = "") -> None:
        self._api_key = api_key or None
        self._client: AsyncOpenAI | None = None

    async def complete(
        self,
        *,
        question: str,
        context_chunks: list[Any],
        model: str,
    ) -> ChatCompletionResult:
        if self._client is None:
            self._client = AsyncOpenAI(api_key=self._api_key)

        context = "\n\n".join(
            f"[chunk_id={chunk.id} document_id={chunk.document_id}]\n{chunk.content}"
            for chunk in context_chunks
        )
        response = await self._client.chat.completions.create(
            model=model,
            messages=[
                {
                    "role": "system",
                    "content": (
                        "You are an internal assistant. Answer in Spanish (es-AR). "
                        "Use only the retrieved context and return JSON with answer and cited_chunk_ids."
                    ),
                },
                {"role": "user", "content": f"Context:\n{context}\n\nQuestion:\n{question}"},
            ],
            temperature=0.1,
            max_tokens=900,
            response_format={"type": "json_object"},
        )
        content = response.choices[0].message.content or "{}"
        parsed = json.loads(content)
        cited_chunk_ids = [UUID(value) for value in parsed.get("cited_chunk_ids", [])]
        usage = response.usage
        return ChatCompletionResult(
            answer=str(parsed.get("answer", "")),
            cited_chunk_ids=cited_chunk_ids,
            input_tokens=usage.prompt_tokens if usage else _estimate_tokens(context + question),
            output_tokens=usage.completion_tokens if usage else _estimate_tokens(content),
        )


def _estimate_tokens(text: str) -> int:
    return max(1, len(text.split()))
