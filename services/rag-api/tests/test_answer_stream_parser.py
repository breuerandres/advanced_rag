from __future__ import annotations

from advanced_rag.rag.answer_stream_parser import AnswerStreamParser


def _feed(parser: AnswerStreamParser, chunks: list[str]) -> str:
    out = []
    for chunk in chunks:
        out.append(parser.feed(chunk))
    return "".join(out)


def test_emits_answer_characters_across_split_deltas() -> None:
    parser = AnswerStreamParser()
    emitted = _feed(parser, ['{"ans', 'wer": "Hol', 'a mundo", "cited_chunk_ids": []}'])
    assert emitted == "Hola mundo"


def test_unescapes_json_escapes_even_when_split() -> None:
    parser = AnswerStreamParser()
    emitted = _feed(parser, ['{"answer": "a\\', 'nb \\u00e9', 'c", "cited_chunk_ids": []}'])
    assert emitted == "a\nb éc"


def test_ignores_other_fields_and_handles_answer_not_first() -> None:
    parser = AnswerStreamParser()
    emitted = _feed(parser, ['{"cited_chunk_ids": ["x"], "answer": "ok"}'])
    assert emitted == "ok"


def test_raw_text_fallback_when_not_json() -> None:
    # Some providers may ignore json mode; raw content must still surface once
    # the parser concludes the payload is not a JSON object.
    parser = AnswerStreamParser()
    emitted = _feed(parser, ["plain ", "text answer"])
    assert parser.finalize_raw() == "plain text answer"
    assert emitted == ""
