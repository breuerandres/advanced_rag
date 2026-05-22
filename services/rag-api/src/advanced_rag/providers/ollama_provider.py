"""Ollama implementation of `ILlmProvider`.

Ollama exposes an OpenAI-compatible API at `/v1`, so we reuse `AsyncOpenAI` with a
custom `base_url`. The default `base_url` is `http://localhost:11434/v1`; in Docker
Compose the operator points this at an Ollama service.

See docs/adr/0001-multi-provider-llm.md.

Note: Ollama JSON mode support varies by model. The provider passes `response_format`
through verbatim; some local models may not respect it without prompting tweaks.
"""

from __future__ import annotations

from collections.abc import AsyncIterator

from openai import AsyncOpenAI

from advanced_rag.providers._retry import is_transient_openai_error, retry_async
from advanced_rag.providers.base import (
    ChatCompletionDelta,
    ChatCompletionRequest,
    ChatUsage,
    ILlmProvider,
)


class OllamaLlmProvider(ILlmProvider):
    name = "ollama"

    def __init__(
        self,
        *,
        base_url: str = "http://localhost:11434/v1",
        api_key: str = "ollama",
    ) -> None:
        # Ollama ignores the API key, but the OpenAI client requires one.
        self._client = AsyncOpenAI(api_key=api_key, base_url=base_url)

    async def chat_stream(
        self, req: ChatCompletionRequest
    ) -> AsyncIterator[ChatCompletionDelta]:
        async def _call():  # type: ignore[no-untyped-def]
            kwargs: dict = {
                "model": req.model,
                "messages": [m.model_dump() for m in req.messages],
                "temperature": req.temperature,
                "max_tokens": req.max_tokens,
                "stream": True,
            }
            if req.response_format is not None:
                kwargs["response_format"] = req.response_format
            return await self._client.chat.completions.create(**kwargs)

        stream = await retry_async(_call, retry_on=is_transient_openai_error)
        async for chunk in stream:
            delta = chunk.choices[0].delta if chunk.choices else None
            finish = chunk.choices[0].finish_reason if chunk.choices else None
            if delta is None and finish is None:
                continue
            yield ChatCompletionDelta(
                content=getattr(delta, "content", None) if delta else None,
                finish_reason=finish,
            )

    async def chat_complete(
        self, req: ChatCompletionRequest
    ) -> tuple[str, ChatUsage]:
        async def _call():  # type: ignore[no-untyped-def]
            kwargs: dict = {
                "model": req.model,
                "messages": [m.model_dump() for m in req.messages],
                "temperature": req.temperature,
                "max_tokens": req.max_tokens,
            }
            if req.response_format is not None:
                kwargs["response_format"] = req.response_format
            return await self._client.chat.completions.create(**kwargs)

        response = await retry_async(_call, retry_on=is_transient_openai_error)
        content = response.choices[0].message.content or ""
        usage = response.usage
        return content, ChatUsage(
            input_tokens=usage.prompt_tokens if usage else 0,
            output_tokens=usage.completion_tokens if usage else 0,
        )
