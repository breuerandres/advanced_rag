from advanced_rag.core.config import Settings


def test_settings_use_mvp_model_defaults() -> None:
    settings = Settings()

    assert settings.openai_chat_model == "gpt-4.1-nano"
    assert settings.openai_embedding_model == "text-embedding-3-small"
    assert settings.openai_embedding_dimensions == 1536
