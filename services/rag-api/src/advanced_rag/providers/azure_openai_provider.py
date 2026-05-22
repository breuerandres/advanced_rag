"""Azure OpenAI implementation of `ILlmProvider` and `IEmbeddingProvider`.

The OpenAI SDK natively supports Azure when constructed with `AsyncAzureOpenAI` instead
of `AsyncOpenAI`. The deployment name acts as the model identifier in Azure.

See docs/adr/0001-multi-provider-llm.md.
"""

from __future__ import annotations

from collections.abc import AsyncIterator

from openai import AsyncAzureOpenAI

from advanced_rag.providers._retry import is_transient_openai_error, retry_async
from advanced_rag.providers.base import (
    ChatCompletionDelta,
    ChatCompletionRequest,
    ChatUsage,
    IEmbeddingProvider,
    ILlmProvider,
)


class AzureOpenAILlmProvider(ILlmProvider):
    name = "azure-openai"

    def __init__(
        self,
        *,
        api_key: str,
        endpoint: str,
        api_version: str = "2024-08-01-preview",
        deployment: str | None = None,
    ) -> None:
        self._client = AsyncAzureOpenAI(
            api_key=api_key,
            azure_endpoint=endpoint,
            api_version=api_version,
        )
        self._deployment = deployment

    def _resolve_model(self, requested: str) -> str:
        # If a deployment was provided to the provider constructor, prefer it; otherwise
        # treat the model name as the deployment name (operator's responsibility).
        return self._deployment or requested

    async def chat_stream(
        self, req: ChatCompletionRequest
    ) -> AsyncIterator[ChatCompletionDelta]:
        async def _call():  # type: ignore[no-untyped-def]
            kwargs: dict = {
                "model": self._resolve_model(req.model),
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
                "model": self._resolve_model(req.model),
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
            cached_input_tokens=getattr(usage, "prompt_tokens_cached", 0) if usage else 0,
        )


class AzureOpenAIEmbeddingProvider(IEmbeddingProvider):
    name = "azure-openai"

    def __init__(
        self,
        *,
        api_key: str,
        endpoint: str,
        api_version: str = "2024-08-01-preview",
        model: str = "text-embedding-3-large",
        dimensions: int = 1024,
        deployment: str | None = None,
    ) -> None:
        self._client = AsyncAzureOpenAI(
            api_key=api_key,
            azure_endpoint=endpoint,
            api_version=api_version,
        )
        self.model = deployment or model
        self.dimensions = dimensions

    async def embed(self, texts: list[str]) -> tuple[list[list[float]], ChatUsage]:
        if not texts:
            return [], ChatUsage()

        async def _call():  # type: ignore[no-untyped-def]
            return await self._client.embeddings.create(
                input=texts,
                model=self.model,
                dimensions=self.dimensions,
            )

        response = await retry_async(_call, retry_on=is_transient_openai_error)
        vectors = [item.embedding for item in response.data]
        usage = response.usage
        return vectors, ChatUsage(input_tokens=usage.prompt_tokens if usage else 0)
