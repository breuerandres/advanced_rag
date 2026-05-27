from advanced_rag.rag.answer_generator import load_system_prompt
from advanced_rag.rag.chat_service import _no_results_message
from advanced_rag.rag.conversation_memory import _load_condenser_prompt
from advanced_rag.rag.query_rewrite import _load_rewriter_prompt


def test_portuguese_locale_falls_back_to_english_prompts() -> None:
    assert load_system_prompt("pt-BR") == load_system_prompt("en-US")
    assert _load_condenser_prompt("pt-BR") == _load_condenser_prompt("en-US")
    assert _load_rewriter_prompt("pt-BR") == _load_rewriter_prompt("en-US")


def test_no_results_message_supports_spanish_and_english_only() -> None:
    assert _no_results_message("es-AR").startswith("No encontr")
    assert _no_results_message("pt-BR") == _no_results_message("en-US")
