"""Provider protocol definitions.

These are the only types business logic should reference. Implementations live in
sibling modules (`openai_provider.py`, `anthropic_provider.py`, ...). The factory
in `factory.py` chooses the implementation based on `app.tenant_config` values.

See docs/adr/0001-multi-provider-llm.md.
"""

from __future__ import annotations

from collections.abc import AsyncIterator
from typing import Literal, Protocol

from pydantic import BaseModel, ConfigDict, Field


# ---------------------------------------------------------------------------
# Chat completion
# ---------------------------------------------------------------------------


class ChatMessage(BaseModel):
    """A single message in a chat completion request."""

    model_config = ConfigDict(frozen=True)

    role: Literal["system", "user", "assistant"]
    content: str


class ChatCompletionRequest(BaseModel):
    """Provider-agnostic chat completion request."""

    model_config = ConfigDict(frozen=True)

    messages: list[ChatMessage]
    model: str
    temperature: float = 0.1
    max_tokens: int = 900
    response_format: dict | None = None
    """If set, the provider must return content that conforms to the JSON schema.

    Schema shape varies per provider (OpenAI uses `response_format`, Anthropic uses
    tool-use, etc.). Each provider translates this to its native equivalent."""


class ChatUsage(BaseModel):
    """Token usage reported by the provider."""

    model_config = ConfigDict(frozen=True)

    input_tokens: int = 0
    cached_input_tokens: int = 0
    output_tokens: int = 0


class ChatCompletionDelta(BaseModel):
    """One streaming chunk from a chat completion."""

    model_config = ConfigDict(frozen=True)

    content: str | None = None
    finish_reason: str | None = None
    usage: ChatUsage | None = None
    """Token usage, reported once at the end of the stream when the provider supports it
    (OpenAI sends it in a final usage-only chunk under `stream_options.include_usage`)."""


class ILlmProvider(Protocol):
    """Protocol for chat / completion providers."""

    name: str
    """Stable identifier, e.g. 'openai', 'anthropic', 'azure-openai', 'ollama'."""

    def chat_stream(self, req: ChatCompletionRequest) -> AsyncIterator[ChatCompletionDelta]:
        """Stream completion tokens as they arrive.

        Implementations should yield `ChatCompletionDelta(content="...")` for each
        token chunk and a final delta with `finish_reason` set to indicate stop.
        """
        ...

    async def chat_complete(
        self, req: ChatCompletionRequest
    ) -> tuple[str, ChatUsage]:
        """Non-streaming completion. Returns the full content and usage."""
        ...


# ---------------------------------------------------------------------------
# Multimodal completion (query-time multimodal RAG)
# ---------------------------------------------------------------------------


class ImageInput(BaseModel):
    """One image attached to a multimodal completion request."""

    model_config = ConfigDict(frozen=True)

    data_base64: str
    """Raw base64-encoded image bytes (no `data:` prefix)."""

    media_type: str
    """MIME type, e.g. 'image/png'."""

    detail: str = "low"
    """Provider image-detail hint; 'low' keeps token cost bounded."""


class IMultimodalLlmProvider(Protocol):
    """Optional capability for providers that accept image inputs.

    Implemented separately from `ILlmProvider` so text-only providers (ollama,
    anthropic, azure) need no changes. The chat service probes for
    `multimodal_stream` and falls back to the text path when it is absent.
    """

    name: str

    def multimodal_stream(
        self, req: ChatCompletionRequest, images: list[ImageInput]
    ) -> AsyncIterator[ChatCompletionDelta]:
        """Stream a multimodal completion token by token.

        Mirrors `ILlmProvider.chat_stream` but attaches `images` to the request:
        yields `ChatCompletionDelta(content=...)` per chunk and a final delta
        carrying usage. Implemented only by providers whose model accepts images.
        """
        ...


# ---------------------------------------------------------------------------
# Embeddings
# ---------------------------------------------------------------------------


class IEmbeddingProvider(Protocol):
    """Protocol for embedding providers."""

    name: str
    """Stable identifier, e.g. 'openai', 'tei', 'voyage'."""

    model: str
    """Model identifier the provider was configured with."""

    dimensions: int
    """Embedding vector dimensions. Must match the rag.document_chunks.embedding column.

    For v2 this is fixed at 1024. See docs/adr/0003-multilingual-embeddings.md.
    """

    async def embed(self, texts: list[str]) -> tuple[list[list[float]], ChatUsage]:
        """Embed a batch of texts.

        Returns a list of vectors (one per input) and a usage record. Implementations
        should batch internally according to the provider's per-request limits.
        """
        ...


# ---------------------------------------------------------------------------
# Reranker
# ---------------------------------------------------------------------------


class RerankerResult(BaseModel):
    """One reranked document with its position in the original list and the score."""

    model_config = ConfigDict(frozen=True)

    original_index: int = Field(..., ge=0)
    score: float


class IRerankerProvider(Protocol):
    """Protocol for cross-encoder reranker providers.

    Note: rerankers are optional. If the tenant_config has `enable_reranker=false` the
    factory returns `None` and the retrieval pipeline skips this step.
    """

    name: str
    """Stable identifier, e.g. 'tei-bge', 'cohere', 'voyage'."""

    model: str
    """Model identifier."""

    async def rerank(
        self, query: str, documents: list[str], top_k: int
    ) -> list[RerankerResult]:
        """Rerank a list of documents against a query.

        Returns up to `top_k` results sorted by descending score. Implementations
        must not mutate the input list and must return `original_index` values
        valid for indexing back into the input.
        """
        ...
