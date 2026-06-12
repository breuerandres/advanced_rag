from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(extra="ignore")

    # ----- Provider-agnostic tenant configuration ------------------------
    # These values seed the in-process `ProviderFactory` until the .NET API exposes
    # tenant_config via /api/v1/config (Phase 1.1). New deployments should populate
    # these fields; the historical `openai_*` fields below remain as legacy aliases
    # so existing .env files continue to work.
    llm_provider: str = "openai"
    llm_model: str = ""
    llm_api_key: str = ""
    llm_api_key_file: str = ""
    llm_base_url: str = ""

    embedding_provider: str = "openai"
    embedding_model: str = ""
    embedding_dimensions: int = 0
    embedding_api_key: str = ""
    embedding_api_key_file: str = ""
    embedding_base_url: str = ""

    enable_reranker: bool = True
    reranker_provider: str = "tei-bge"
    reranker_model: str = "BAAI/bge-reranker-v2-m3"
    reranker_base_url: str = ""
    reranker_api_key: str = ""

    azure_endpoint: str = ""
    azure_api_version: str = "2024-08-01-preview"
    azure_chat_deployment: str = ""
    azure_embedding_deployment: str = ""

    # Hybrid retrieval knobs (Phase 2.4)
    rag_vector_top_k: int = 20
    rag_bm25_top_k: int = 20
    rag_rrf_k: int = 60
    rag_hybrid_top_k: int = 30
    rag_final_top_k: int = 8
    conversation_history_turns: int = 5

    default_locale: str = "es-AR"

    # ----- Legacy MVP fields ---------------------------------------------
    # These are the source of truth when set; new fields above act as defaults
    # when the legacy fields are empty. The MVP shipped with these names and
    # the chat schema was `vector(1536)` — but the v2 migration changes it to
    # `vector(1024)`, so dimensions must be updated to 1024 in any deployment
    # that upgrades.
    openai_chat_model: str = "gpt-4.1-nano"
    openai_embedding_model: str = "text-embedding-3-large"
    openai_embedding_dimensions: int = 1024
    openai_api_key: str = ""
    openai_api_key_file: str = ""

    rag_database_url: str = ""
    rag_database_host: str = "postgres"
    rag_database_port: int = 5432
    rag_database_name: str = "advanced_rag"
    rag_database_user: str = "rag_owner"
    rag_database_password: str = ""
    rag_database_password_file: str = ""
    internal_service_token: str = ""
    internal_service_token_file: str = ""
    csrf_signing_key: str = ""
    csrf_signing_key_file: str = ""
    rag_semantic_cache_ttl_hours: int = 24
    rag_semantic_cache_similarity_threshold: float = 0.90
    customer_timezone: str = "UTC"
    default_monthly_ai_budget_usd: float = 5.00
    openai_chat_temperature: float = 0.1
    openai_chat_max_tokens: int = 900
    chat_token_issuer: str = "advanced-rag-dotnet-api"
    chat_token_audience: str = "advanced-rag-chat"
    chat_token_public_keys_by_kid: dict[str, str] = {}
    dotnet_jwks_url: str = ""
    dotnet_session_validate_url: str = "http://dotnet-api:8080/internal/session/validate"
    multimodal_enabled: bool = True
    multimodal_max_images: int = 3
    multimodal_max_total_image_bytes: int = 5242880  # 5 MB
    multimodal_image_detail: str = "low"
    dotnet_internal_base_url: str = "http://dotnet-api:8080"
    session_cookie_name: str = "__Host-session"
    session_validation_cache_seconds: int = 60
    log_directory: str = "/var/log/rag-api"

    @property
    def resolved_rag_database_url(self) -> str:
        if self.rag_database_url:
            return self.rag_database_url

        password = self.rag_database_password or _read_secret_file(self.rag_database_password_file)
        return (
            "postgresql+asyncpg://"
            f"{self.rag_database_user}:{password}"
            f"@{self.rag_database_host}:{self.rag_database_port}/{self.rag_database_name}"
        )

    @property
    def resolved_internal_service_token(self) -> str:
        return self.internal_service_token or _read_secret_file(self.internal_service_token_file)

    @property
    def resolved_csrf_signing_key(self) -> str:
        return self.csrf_signing_key or _read_secret_file(self.csrf_signing_key_file)

    @property
    def resolved_openai_api_key(self) -> str:
        return self.openai_api_key or _read_secret_file(self.openai_api_key_file)

    @property
    def resolved_llm_api_key(self) -> str:
        explicit = self.llm_api_key or _read_secret_file(self.llm_api_key_file)
        return explicit or self.resolved_openai_api_key

    @property
    def resolved_embedding_api_key(self) -> str:
        explicit = self.embedding_api_key or _read_secret_file(self.embedding_api_key_file)
        return explicit or self.resolved_llm_api_key

    @property
    def resolved_chat_model(self) -> str:
        return self.openai_chat_model or self.llm_model

    @property
    def resolved_embedding_model(self) -> str:
        return self.openai_embedding_model or self.embedding_model

    @property
    def resolved_embedding_dimensions(self) -> int:
        return self.openai_embedding_dimensions or self.embedding_dimensions


def _read_secret_file(path: str) -> str:
    if not path:
        return ""
    with open(path, encoding="utf-8") as secret_file:
        return secret_file.read().lstrip("﻿").strip()
