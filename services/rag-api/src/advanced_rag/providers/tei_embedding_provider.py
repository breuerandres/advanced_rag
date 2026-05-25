"""Text Embeddings Inference (TEI) embedding provider.

Targets HuggingFace's `text-embeddings-inference` server (https://github.com/huggingface/text-embeddings-inference).
Default model: BAAI/bge-m3 (1024-dim, multilingual).

The TEI server is started by Docker Compose (`infra/compose/compose.yaml` adds a sidecar)
with a model preloaded. The provider points at its HTTP endpoint.

See docs/adr/0001-multi-provider-llm.md and docs/adr/0003-multilingual-embeddings.md.
"""

from __future__ import annotations

import httpx

from advanced_rag.providers._retry import is_transient_httpx_error, retry_async
from advanced_rag.providers.base import ChatUsage, IEmbeddingProvider


class TeiEmbeddingProvider(IEmbeddingProvider):
    name = "tei"

    def __init__(
        self,
        *,
        base_url: str,
        model: str = "BAAI/bge-m3",
        dimensions: int = 1024,
        timeout_seconds: float = 30.0,
    ) -> None:
        self._base_url = base_url.rstrip("/")
        self.model = model
        self.dimensions = dimensions
        self._client = httpx.AsyncClient(timeout=timeout_seconds)

    async def embed(self, texts: list[str]) -> tuple[list[list[float]], ChatUsage]:
        if not texts:
            return [], ChatUsage()

        async def _call() -> httpx.Response:
            response = await self._client.post(
                f"{self._base_url}/embed",
                json={"inputs": texts, "normalize": True},
            )
            response.raise_for_status()
            return response

        response = await retry_async(_call, retry_on=is_transient_httpx_error)
        # TEI returns either {"embeddings": [...]} or just [[...], [...]] depending on
        # the route. The /embed endpoint returns the latter; /embed_all returns the
        # former. We handle both shapes defensively.
        data = response.json()
        if isinstance(data, dict) and "embeddings" in data:
            vectors = data["embeddings"]
        else:
            vectors = data
        if not all(len(v) == self.dimensions for v in vectors):
            raise ValueError(
                f"TEI returned vectors of unexpected dimensions; expected {self.dimensions}, "
                f"got {[len(v) for v in vectors]}"
            )

        # TEI does not report token usage. We approximate as word count for cost tracking
        # (BGE-M3 is self-hosted, so cost is operational not metered).
        approx_input_tokens = sum(len(t.split()) for t in texts)
        return vectors, ChatUsage(input_tokens=approx_input_tokens)

    async def aclose(self) -> None:
        await self._client.aclose()
