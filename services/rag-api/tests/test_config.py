from advanced_rag.core.config import Settings


def test_settings_use_mvp_model_defaults() -> None:
    settings = Settings()

    assert settings.openai_chat_model == "gpt-4.1-nano"
    assert settings.openai_embedding_model == "text-embedding-3-small"
    assert settings.openai_embedding_dimensions == 1536


def test_secret_file_reader_ignores_utf8_bom(tmp_path) -> None:
    secret_path = tmp_path / "internal_service_token.txt"
    secret_path.write_text("\ufefflocal-token", encoding="utf-8")

    settings = Settings(internal_service_token_file=str(secret_path))

    assert settings.resolved_internal_service_token == "local-token"
