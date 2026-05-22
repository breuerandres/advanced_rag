"""Cohere reranker provider (rerank-multilingual-v3.0 by default).

Uses the official Cohere Python SDK. Requires a Cohere API key.

See docs/adr/0001-multi-provider-llm.md.

Note: this provider depends on the `cohere` Python SDK. Add to optional dependencies
group `providers-cohere`.
"""

from __future__ import annotations

from advanced_rag.providers._retry import is_transient_httpx_error, retry_async
from advanced_rag.providers.base import IRerankerProvider, RerankerResult


class CohereRerankerProvider(IRerankerProvider):
    name = "cohere"

    def __init__(
        self,
        *,
        api_key: str,
        model: str = "rerank-multilingual-v3.0",
    ) -> None:
        # Lazy import; the dep is optional.
        from cohere import AsyncClient  # type: ignore

        self._client = AsyncClient(api_key=api_key)
        self.model = model

    async def rerank(
        self, query: str, documents: list[str], top_k: int
    ) -> list[RerankerResult]:
        if not documents:
            return []

        async def _call():  # type: ignore[no-untyped-def]
            return await self._client.rerank(
                model=self.model,
                query=query,
                documents=documents,
                top_n=top_k,
            )

        response = await retry_async(_call, retry_on=is_transient_httpx_error)
        return [
            RerankerResult(original_index=item.index, score=item.relevance_score)
            for item in response.results
        ]
