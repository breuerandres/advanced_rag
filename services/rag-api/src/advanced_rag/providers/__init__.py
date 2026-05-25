"""LLM / Embedding / Reranker provider abstraction.

See docs/adr/0001-multi-provider-llm.md for the design.

This package replaces direct imports of `openai.AsyncOpenAI` from business logic. All
paid LLM/embedding/reranker calls must go through these protocols.

Usage::

    from advanced_rag.providers import (
        ILlmProvider, IEmbeddingProvider, IRerankerProvider, ProviderFactory,
    )

    factory = ProviderFactory(tenant_config)
    llm = await factory.make_llm()
    embed = await factory.make_embedding()
    reranker = await factory.make_reranker()  # may be None if disabled
"""

from advanced_rag.providers.base import (
    ChatCompletionDelta,
    ChatCompletionRequest,
    ChatMessage,
    ChatUsage,
    IEmbeddingProvider,
    ILlmProvider,
    IRerankerProvider,
    RerankerResult,
)
from advanced_rag.providers.factory import ProviderFactory

__all__ = [
    "ChatCompletionDelta",
    "ChatCompletionRequest",
    "ChatMessage",
    "ChatUsage",
    "IEmbeddingProvider",
    "ILlmProvider",
    "IRerankerProvider",
    "RerankerResult",
    "ProviderFactory",
]
