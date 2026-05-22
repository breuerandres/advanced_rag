from advanced_rag.core.config import Settings


def test_settings_use_v2_model_defaults() -> None:
    """v2 defaults: text-embedding-3-large @ 1024d.

    The schema migration changes `rag.document_chunks.embedding` to `vector(1024)`,
    so the embedding dimensions default must match. Deployments upgrading from the
    MVP need to set `OPENAI_EMBEDDING_DIMENSIONS=1024` (and reindex) before serving
    traffic on the new schema.
    """
    settings = Settings()

    assert settings.openai_chat_model == "gpt-4.1-nano"
    assert settings.openai_embedding_model == "text-embedding-3-large"
    assert settings.openai_embedding_dimensions == 1024
    assert settings.resolved_chat_model == "gpt-4.1-nano"
    assert settings.resolved_embedding_model == "text-embedding-3-large"
    assert settings.resolved_embedding_dimensions == 1024


def test_secret_file_reader_ignores_utf8_bom(tmp_path) -> None:
    secret_path = tmp_path / "internal_service_token.txt"
    secret_path.write_text("﻿local-token", encoding="utf-8")

    settings = Settings(internal_service_token_file=str(secret_path))

    assert settings.resolved_internal_service_token == "local-token"
