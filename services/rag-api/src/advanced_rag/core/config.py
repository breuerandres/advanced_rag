from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(extra="ignore")

    openai_chat_model: str = "gpt-4.1-nano"
    openai_embedding_model: str = "text-embedding-3-small"
    openai_embedding_dimensions: int = 1536
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
    def resolved_openai_api_key(self) -> str:
        return self.openai_api_key or _read_secret_file(self.openai_api_key_file)


def _read_secret_file(path: str) -> str:
    if not path:
        return ""
    with open(path, encoding="utf-8") as secret_file:
        return secret_file.read().lstrip("\ufeff").strip()
