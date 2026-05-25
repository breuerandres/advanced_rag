"""Provider factory.

Reads the tenant configuration (loaded from `app.tenant_config` at startup or per
request as appropriate) and returns the right provider implementations.

See docs/adr/0001-multi-provider-llm.md.
"""

from __future__ import annotations

from typing import Any

from pydantic import BaseModel, ConfigDict

from advanced_rag.providers.base import (
    IEmbeddingProvider,
    ILlmProvider,
    IRerankerProvider,
)


class TenantProviderConfig(BaseModel):
    """Subset of tenant_config relevant for provider construction.

    The .NET API exposes the full tenant_config via `GET /api/v1/config`. FastAPI caches
    these values at process start and refreshes on a periodic interval or when an admin
    triggers an explicit reload.
    """

    model_config = ConfigDict(frozen=True)

    # LLM
    llm_provider: str = "openai"
    llm_model: str = "gpt-4o-mini"
    llm_base_url: str | None = None
    llm_api_key: str = ""

    # Embeddings
    embedding_provider: str = "openai"
    embedding_model: str = "text-embedding-3-large"
    embedding_dimensions: int = 1024
    embedding_base_url: str | None = None
    embedding_api_key: str = ""

    # Reranker
    enable_reranker: bool = True
    reranker_provider: str = "tei-bge"
    reranker_model: str = "BAAI/bge-reranker-v2-m3"
    reranker_base_url: str | None = None
    reranker_api_key: str = ""

    # Azure-only extras
    azure_endpoint: str | None = None
    azure_api_version: str = "2024-08-01-preview"
    azure_chat_deployment: str | None = None
    azure_embedding_deployment: str | None = None


class ProviderFactory:
    """Builds provider implementations from tenant config.

    Construct once per process; calls to `make_*` are cheap and the underlying provider
    instances are stateless aside from HTTP client connections, which are pooled.
    """

    def __init__(self, config: TenantProviderConfig) -> None:
        self._config = config
        self._llm: ILlmProvider | None = None
        self._embedding: IEmbeddingProvider | None = None
        self._reranker: IRerankerProvider | None = None

    @property
    def config(self) -> TenantProviderConfig:
        return self._config

    def make_llm(self) -> ILlmProvider:
        if self._llm is None:
            self._llm = self._build_llm()
        return self._llm

    def make_embedding(self) -> IEmbeddingProvider:
        if self._embedding is None:
            self._embedding = self._build_embedding()
        return self._embedding

    def make_reranker(self) -> IRerankerProvider | None:
        if not self._config.enable_reranker:
            return None
        if self._reranker is None:
            self._reranker = self._build_reranker()
        return self._reranker

    # ----- builders -------------------------------------------------------

    def _build_llm(self) -> ILlmProvider:
        cfg = self._config
        provider = cfg.llm_provider.lower()

        if provider == "openai":
            from advanced_rag.providers.openai_provider import OpenAILlmProvider

            return OpenAILlmProvider(api_key=cfg.llm_api_key, base_url=cfg.llm_base_url)

        if provider == "anthropic":
            from advanced_rag.providers.anthropic_provider import AnthropicLlmProvider

            return AnthropicLlmProvider(api_key=cfg.llm_api_key, base_url=cfg.llm_base_url)

        if provider in ("azure-openai", "azure"):
            from advanced_rag.providers.azure_openai_provider import AzureOpenAILlmProvider

            if not cfg.azure_endpoint:
                raise ValueError("azure_endpoint must be set when llm_provider is 'azure-openai'")
            return AzureOpenAILlmProvider(
                api_key=cfg.llm_api_key,
                endpoint=cfg.azure_endpoint,
                api_version=cfg.azure_api_version,
                deployment=cfg.azure_chat_deployment,
            )

        if provider == "ollama":
            from advanced_rag.providers.ollama_provider import OllamaLlmProvider

            base_url = cfg.llm_base_url or "http://localhost:11434/v1"
            return OllamaLlmProvider(base_url=base_url)

        raise ValueError(f"Unknown llm_provider: {cfg.llm_provider!r}")

    def _build_embedding(self) -> IEmbeddingProvider:
        cfg = self._config
        provider = cfg.embedding_provider.lower()

        if provider == "openai":
            from advanced_rag.providers.openai_provider import OpenAIEmbeddingProvider

            return OpenAIEmbeddingProvider(
                api_key=cfg.embedding_api_key or cfg.llm_api_key,
                model=cfg.embedding_model,
                dimensions=cfg.embedding_dimensions,
                base_url=cfg.embedding_base_url,
            )

        if provider in ("azure-openai", "azure"):
            from advanced_rag.providers.azure_openai_provider import (
                AzureOpenAIEmbeddingProvider,
            )

            if not cfg.azure_endpoint:
                raise ValueError("azure_endpoint must be set for azure embedding provider")
            return AzureOpenAIEmbeddingProvider(
                api_key=cfg.embedding_api_key or cfg.llm_api_key,
                endpoint=cfg.azure_endpoint,
                api_version=cfg.azure_api_version,
                model=cfg.embedding_model,
                dimensions=cfg.embedding_dimensions,
                deployment=cfg.azure_embedding_deployment,
            )

        if provider == "tei":
            from advanced_rag.providers.tei_embedding_provider import TeiEmbeddingProvider

            if not cfg.embedding_base_url:
                raise ValueError("embedding_base_url must be set when embedding_provider is 'tei'")
            return TeiEmbeddingProvider(
                base_url=cfg.embedding_base_url,
                model=cfg.embedding_model,
                dimensions=cfg.embedding_dimensions,
            )

        raise ValueError(f"Unknown embedding_provider: {cfg.embedding_provider!r}")

    def _build_reranker(self) -> IRerankerProvider:
        cfg = self._config
        provider = cfg.reranker_provider.lower()

        if provider in ("tei", "tei-bge", "bge"):
            from advanced_rag.providers.tei_reranker_provider import TeiRerankerProvider

            if not cfg.reranker_base_url:
                raise ValueError(
                    "reranker_base_url must be set when reranker_provider is 'tei-bge'"
                )
            return TeiRerankerProvider(
                base_url=cfg.reranker_base_url,
                model=cfg.reranker_model,
            )

        if provider == "cohere":
            from advanced_rag.providers.cohere_reranker import CohereRerankerProvider

            return CohereRerankerProvider(
                api_key=cfg.reranker_api_key,
                model=cfg.reranker_model,
            )

        raise ValueError(f"Unknown reranker_provider: {cfg.reranker_provider!r}")


def make_factory_from_dict(d: dict[str, Any]) -> ProviderFactory:
    """Convenience for building a factory from a plain dict (e.g. from .env or a test)."""
    return ProviderFactory(TenantProviderConfig.model_validate(d))
