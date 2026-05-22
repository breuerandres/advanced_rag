"""OpenAI implementation of `ILlmProvider` and `IEmbeddingProvider`.

See docs/adr/0001-multi-provider-llm.md.

Note: the embedding provider truncates to the configured `dimensions` natively
(OpenAI's text-embedding-3-large supports `dimensions` parameter as of 2024-01).

Construction of the underlying `AsyncOpenAI` client is deferred to first use so a
process can hold an instance of this provider without an API key (e.g. during test
app startup where a fake provider is injected before any network call would occur).
"""

from __future__ import annotations

from collections.abc import AsyncIterator

from openai import AsyncOpenAI

from advanced_rag.providers._retry import is_transient_openai_error, retry_async
from advanced_rag.providers.base import (
    ChatCompletionDelta,
    ChatCompletionRequest,
    ChatUsage,
    IEmbeddingProvider,
    ILlmProvider,
)


class OpenAILlmProvider(ILlmProvider):
    name = "openai"

    def __init__(self, *, api_key: str, base_url: str | None = None) -> None:
        self._api_key = api_key
        self._base_url = base_url
        self._client: AsyncOpenAI | None = None

    def _ensure_client(self) -> AsyncOpenAI:
        if self._client is None:
            self._client = AsyncOpenAI(api_key=self._api_key or None, base_url=self._base_url)
        return self._client

    async def chat_stream(
        self, req: ChatCompletionRequest
    ) -> AsyncIterator[ChatCompletionDelta]:
        client = self._ensure_client()

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
            return await client.chat.completions.create(**kwargs)

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
        client = self._ensure_client()

        async def _call():  # type: ignore[no-untyped-def]
            kwargs: dict = {
                "model": req.model,
                "messages": [m.model_dump() for m in req.messages],
                "temperature": req.temperature,
                "max_tokens": req.max_tokens,
            }
            if req.response_format is not None:
                kwargs["response_format"] = req.response_format
            return await client.chat.completions.create(**kwargs)

        response = await retry_async(_call, retry_on=is_transient_openai_error)

        content = response.choices[0].message.content or ""
        usage = response.usage
        return content, ChatUsage(
            input_tokens=usage.prompt_tokens if usage else 0,
            output_tokens=usage.completion_tokens if usage else 0,
            cached_input_tokens=getattr(usage, "prompt_tokens_cached", 0) if usage else 0,
        )


class OpenAIEmbeddingProvider(IEmbeddingProvider):
    name = "openai"

    def __init__(
        self,
        *,
        api_key: str,
        model: str = "text-embedding-3-large",
        dimensions: int = 1024,
        base_url: str | None = None,
    ) -> None:
        self._api_key = api_key
        self._base_url = base_url
        self._client: AsyncOpenAI | None = None
        self.model = model
        self.dimensions = dimensions

    def _ensure_client(self) -> AsyncOpenAI:
        if self._client is None:
            self._client = AsyncOpenAI(api_key=self._api_key or None, base_url=self._base_url)
        return self._client

    async def embed(
        self, texts: list[str]
    ) -> tuple[list[list[float]], ChatUsage]:
        if not texts:
            return [], ChatUsage()

        client = self._ensure_client()

        async def _call():  # type: ignore[no-untyped-def]
            return await client.embeddings.create(
                input=texts,
                model=self.model,
                dimensions=self.dimensions,
            )

        response = await retry_async(_call, retry_on=is_transient_openai_error)
        vectors = [item.embedding for item in response.data]
        usage = response.usage
        return vectors, ChatUsage(
            input_tokens=usage.prompt_tokens if usage else 0,
        )
