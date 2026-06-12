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
from typing import Any

from openai import AsyncOpenAI

from advanced_rag.providers._retry import is_transient_openai_error, retry_async
from advanced_rag.providers.base import (
    ChatCompletionDelta,
    ChatCompletionRequest,
    ChatUsage,
    IEmbeddingProvider,
    ILlmProvider,
    ImageInput,
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
                "stream_options": {"include_usage": True},
            }
            if req.response_format is not None:
                kwargs["response_format"] = req.response_format
            return await client.chat.completions.create(**kwargs)

        stream = await retry_async(_call, retry_on=is_transient_openai_error)

        async for chunk in stream:
            usage = getattr(chunk, "usage", None)
            delta = chunk.choices[0].delta if chunk.choices else None
            finish = chunk.choices[0].finish_reason if chunk.choices else None
            if delta is None and finish is None and usage is None:
                continue
            yield ChatCompletionDelta(
                content=getattr(delta, "content", None) if delta else None,
                finish_reason=finish,
                usage=(
                    ChatUsage(
                        input_tokens=getattr(usage, "prompt_tokens", 0) or 0,
                        output_tokens=getattr(usage, "completion_tokens", 0) or 0,
                        cached_input_tokens=getattr(usage, "prompt_tokens_cached", 0) or 0,
                    )
                    if usage is not None
                    else None
                ),
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

    async def multimodal_complete(
        self, req: ChatCompletionRequest, images: list[ImageInput]
    ) -> tuple[str, ChatUsage]:
        client = self._ensure_client()

        instructions = "\n\n".join(m.content for m in req.messages if m.role == "system")
        user_text = "\n\n".join(m.content for m in req.messages if m.role != "system")
        content_parts: list[dict] = [{"type": "input_text", "text": user_text}]
        for image in images:
            content_parts.append(
                {
                    "type": "input_image",
                    "image_url": f"data:{image.media_type};base64,{image.data_base64}",
                    "detail": image.detail,
                }
            )

        async def _call():  # type: ignore[no-untyped-def]
            kwargs: dict = {
                "model": req.model,
                "input": [{"role": "user", "content": content_parts}],
                "store": False,
                "temperature": req.temperature,
                "max_output_tokens": req.max_tokens,
            }
            if instructions:
                kwargs["instructions"] = instructions
            if req.response_format is not None:
                kwargs["text"] = {"format": req.response_format}
            return await client.responses.create(**kwargs)

        response = await retry_async(_call, retry_on=is_transient_openai_error)

        content = getattr(response, "output_text", None) or _extract_output_text(response)
        usage = getattr(response, "usage", None)
        cached = 0
        details = getattr(usage, "input_tokens_details", None) if usage else None
        if details is not None:
            cached = getattr(details, "cached_tokens", 0) or 0
        return content, ChatUsage(
            input_tokens=getattr(usage, "input_tokens", 0) if usage else 0,
            output_tokens=getattr(usage, "output_tokens", 0) if usage else 0,
            cached_input_tokens=cached,
        )


def _extract_output_text(response: Any) -> str:
    """Walk a Responses API result for message text when `output_text` is absent."""
    parts: list[str] = []
    for item in getattr(response, "output", None) or []:
        for part in getattr(item, "content", None) or []:
            text = getattr(part, "text", None)
            if text:
                parts.append(text)
    return "".join(parts)


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
