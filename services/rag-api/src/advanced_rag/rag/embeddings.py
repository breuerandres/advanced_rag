from __future__ import annotations

from typing import Protocol

from openai import AsyncOpenAI


class EmbeddingProvider(Protocol):
    async def embed_texts(
        self,
        texts: list[str],
        *,
        model: str,
        dimensions: int,
    ) -> list[list[float]]:
        pass


class OpenAIEmbeddingProvider:
    def __init__(self, api_key: str = "") -> None:
        self._api_key = api_key or None
        self._client: AsyncOpenAI | None = None

    async def embed_texts(
        self,
        texts: list[str],
        *,
        model: str,
        dimensions: int,
    ) -> list[list[float]]:
        if self._client is None:
            self._client = AsyncOpenAI(api_key=self._api_key)
        response = await self._client.embeddings.create(
            input=texts,
            model=model,
            dimensions=dimensions,
        )
        return [item.embedding for item in response.data]
