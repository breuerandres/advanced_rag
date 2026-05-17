from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(extra="ignore")

    openai_chat_model: str = "gpt-4.1-mini"
    openai_embedding_model: str = "text-embedding-3-large"
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
        return secret_file.read().strip()
