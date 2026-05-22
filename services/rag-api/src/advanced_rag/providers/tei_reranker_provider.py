"""TEI reranker provider (BGE-reranker-v2-m3 by default).

TEI exposes a `/rerank` endpoint. Same sidecar can serve embedding and reranker if loaded
with both models, but in production a separate sidecar per model is recommended.

See docs/adr/0001-multi-provider-llm.md and docs/adr/0002-hybrid-retrieval.md.
"""

from __future__ import annotations

import httpx

from advanced_rag.providers._retry import is_transient_httpx_error, retry_async
from advanced_rag.providers.base import IRerankerProvider, RerankerResult


class TeiRerankerProvider(IRerankerProvider):
    name = "tei-bge"

    def __init__(
        self,
        *,
        base_url: str,
        model: str = "BAAI/bge-reranker-v2-m3",
        timeout_seconds: float = 15.0,
    ) -> None:
        self._base_url = base_url.rstrip("/")
        self.model = model
        self._client = httpx.AsyncClient(timeout=timeout_seconds)

    async def rerank(
        self, query: str, documents: list[str], top_k: int
    ) -> list[RerankerResult]:
        if not documents:
            return []

        async def _call() -> httpx.Response:
            response = await self._client.post(
                f"{self._base_url}/rerank",
                json={
                    "query": query,
                    "texts": documents,
                    "return_documents": False,
                    "raw_scores": False,
                },
            )
            response.raise_for_status()
            return response

        response = await retry_async(_call, retry_on=is_transient_httpx_error)
        # TEI returns: [{"index": int, "score": float}, ...] sorted by descending score.
        data = response.json()
        results = [
            RerankerResult(original_index=item["index"], score=item["score"])
            for item in data
        ]
        return results[:top_k]

    async def aclose(self) -> None:
        await self._client.aclose()
