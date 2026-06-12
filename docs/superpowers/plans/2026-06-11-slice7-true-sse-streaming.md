# Slice 7: True SSE Streaming Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stream answer tokens to the browser as the model produces them, instead of generating the full answer first and replaying it — time-to-first-token drops from full generation latency to provider first-token latency.

**Architecture:** `generate_answer` gains a streaming sibling that consumes `ILlmProvider.chat_stream` and pushes character deltas through an incremental JSON-field parser (the model still returns `{"answer": ..., "cited_chunk_ids": [...]}`; we surface `answer` characters as they arrive and parse citations from the completed payload). `ChatService` gains `answer_stream(...)` — an async generator yielding typed events — and the router builds the `StreamingResponse` directly from it. Cache hits, no-results answers, and multimodal generation (single-shot Responses call, Slice 3) keep emitting one whole-answer event. The audit row is written after the stream completes, preserving exact token usage and end-to-end latency.

**Tech Stack:** FastAPI StreamingResponse, async generators, `openai` SDK streaming, pytest.

**Depends on:** Slice 3 (multimodal call sites exist) and Slice 5 (final `chat_service.py` shape). The SSE event contract in `rag-spec.md` (`request-id`, `cache-hit`, `answer-token`, `citations`, `usage`, `done`, `error`) does not change — frontends need no changes.

---

## Background For A Zero-Context Engineer

- Today `POST /api/chat` (`services/rag-api/src/advanced_rag/api/routers/chat.py`) runs `await service.answer(...)` to completion and then `_stream_answer` replays the finished `ChatAnswer` as SSE. The provider call inside `generate_answer` uses `chat_complete` (non-streaming).
- `ILlmProvider.chat_stream(req) -> AsyncIterator[ChatCompletionDelta]` already exists and is implemented by `openai_provider.py` (verified) — it is just unused on this path.
- `rag-spec.md` (Generation section) already specifies the incremental approach: watch the streamed content for the `"answer":"` field and emit its character deltas, then parse citations at the end. The model is instructed (see `answer_generator.JSON_INSTRUCTION`) to emit `{"answer": "...", "cited_chunk_ids": [...]}` with `response_format={"type": "json_object"}`.
- Provider deltas can split anywhere — including inside `é`-style escapes — so the parser must be a character state machine, not regex over the buffer.

## File Map

- Create: `services/rag-api/src/advanced_rag/rag/answer_stream_parser.py`
- Modify: `services/rag-api/src/advanced_rag/rag/answer_generator.py` (add `generate_answer_stream`)
- Modify: `services/rag-api/src/advanced_rag/rag/chat_service.py` (add `answer_stream`)
- Modify: `services/rag-api/src/advanced_rag/api/routers/chat.py` (stream from the generator)
- Create: `services/rag-api/tests/test_answer_stream_parser.py`
- Modify: `services/rag-api/tests/test_chat_rag.py` (streaming integration test with a fake streaming provider)

---

## Task 1: Incremental Answer-Field Parser (pure, fully unit-testable)

**Files:**
- Create: `services/rag-api/src/advanced_rag/rag/answer_stream_parser.py`
- Create: `services/rag-api/tests/test_answer_stream_parser.py`

- [ ] **Step 1: Write the failing tests**

```python
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
```

- [ ] **Step 2: Run to verify failure**

Run: `uv run pytest tests/test_answer_stream_parser.py -q` → FAIL (module missing).

- [ ] **Step 3: Implement the parser**

```python
"""Incremental extractor for the streamed `answer` field of the model's JSON payload.

The chat model returns `{"answer": "...", "cited_chunk_ids": [...]}` (json_object
mode). Deltas can split anywhere, including inside escape sequences, so this is a
character state machine: it scans for the `"answer"` key, then enters the string
value and emits unescaped characters as they complete. Everything fed is also kept
in `raw` so the caller can json-parse the full payload at the end (citations) or
fall back to raw text when the payload was never JSON.
"""

from __future__ import annotations


class AnswerStreamParser:
    _SEEK_KEY = 0      # scanning for "answer" key
    _SEEK_COLON = 1    # key found, scanning for the value start quote
    _IN_VALUE = 2      # inside the answer string value
    _DONE = 3          # value closed; ignore the rest

    def __init__(self) -> None:
        self.raw: str = ""
        self._state = self._SEEK_KEY
        self._escape = False
        self._unicode_buffer: str | None = None  # collects 4 hex digits after \u
        self._key_window = ""

    def feed(self, delta: str) -> str:
        """Consume a provider delta; return the answer characters it completes."""
        self.raw += delta
        out: list[str] = []
        for ch in delta:
            if self._state == self._SEEK_KEY:
                self._key_window = (self._key_window + ch)[-12:]
                if self._key_window.endswith('"answer"'):
                    self._state = self._SEEK_COLON
            elif self._state == self._SEEK_COLON:
                if ch == '"':
                    self._state = self._IN_VALUE
                # ':' and whitespace are skipped silently
            elif self._state == self._IN_VALUE:
                if self._unicode_buffer is not None:
                    self._unicode_buffer += ch
                    if len(self._unicode_buffer) == 4:
                        out.append(chr(int(self._unicode_buffer, 16)))
                        self._unicode_buffer = None
                elif self._escape:
                    self._escape = False
                    if ch == "u":
                        self._unicode_buffer = ""
                    else:
                        out.append(_UNESCAPE.get(ch, ch))
                elif ch == "\\":
                    self._escape = True
                elif ch == '"':
                    self._state = self._DONE
                else:
                    out.append(ch)
        return "".join(out)

    def finalize_raw(self) -> str:
        """Full raw content; used for citation parsing or non-JSON fallback."""
        return self.raw


_UNESCAPE = {"n": "\n", "t": "\t", "r": "\r", "b": "\b", "f": "\f", '"': '"', "\\": "\\", "/": "/"}
```

- [ ] **Step 4: Run the tests** → PASS. **Step 5: Commit:**

```bash
git add services/rag-api/src/advanced_rag/rag/answer_stream_parser.py services/rag-api/tests/test_answer_stream_parser.py
git commit -m "feat(rag): incremental json answer-field stream parser"
```

## Task 2: Streaming Answer Generator

**Files:**
- Modify: `services/rag-api/src/advanced_rag/rag/answer_generator.py`

- [ ] **Step 1: Add the streaming generator**

```python
from collections.abc import AsyncIterator

from advanced_rag.rag.answer_stream_parser import AnswerStreamParser


async def generate_answer_stream(
    *,
    llm: ILlmProvider,
    question: str,
    chunks: list[Any],
    locale: str,
    model: str,
    temperature: float = 0.1,
    max_tokens: int = 900,
) -> AsyncIterator[str | AnswerGeneration]:
    """Yield answer-character strings as they stream, then one final AnswerGeneration.

    The final item is always an `AnswerGeneration` carrying the full answer, cited
    chunk ids, and usage; every earlier item is a `str` delta for SSE forwarding.
    """
    system = load_system_prompt(locale)
    user_content = (
        f"Context:\n{_format_context(chunks)}\n\n"
        f"Question:\n{question}\n\n"
        f"{JSON_INSTRUCTION}"
    )
    req = ChatCompletionRequest(
        messages=[
            ChatMessage(role="system", content=system),
            ChatMessage(role="user", content=user_content),
        ],
        model=model,
        temperature=temperature,
        max_tokens=max_tokens,
        response_format={"type": "json_object"},
    )
    parser = AnswerStreamParser()
    usage = ChatUsage()
    async for delta in llm.chat_stream(req):
        if delta.content:
            emitted = parser.feed(delta.content)
            if emitted:
                yield emitted
        if delta.finish_reason is not None and delta.usage is not None:
            usage = delta.usage
    answer, cited = _parse_answer(parser.finalize_raw())
    yield AnswerGeneration(answer=answer, cited_chunk_ids=cited, usage=usage)
```

Check `ChatCompletionDelta` in `providers/base.py`: if it has no `usage` field, add `usage: ChatUsage | None = None` and populate it in `openai_provider.chat_stream`'s final delta (the OpenAI SDK exposes usage on the last chunk when `stream_options={"include_usage": True}` — set that option in the provider). If usage is unavailable from a provider, fall back to `_estimate_tokens` at the call site (chat_service already imports an estimator).

- [ ] **Step 2: Quick unit test with a fake provider** (in `test_chat_rag.py` or a small new module): a fake `chat_stream` yielding three deltas forming a valid payload; assert the generator yields the expected character strings and a final `AnswerGeneration` with the right `cited_chunk_ids`. Run it → PASS.

- [ ] **Step 3: Commit:**

```bash
git add services/rag-api
git commit -m "feat(rag): streaming answer generation over provider chat_stream"
```

## Task 3: `ChatService.answer_stream` And Router Integration

**Files:**
- Modify: `services/rag-api/src/advanced_rag/rag/chat_service.py`
- Modify: `services/rag-api/src/advanced_rag/api/routers/chat.py`
- Modify: `services/rag-api/tests/test_chat_rag.py`

- [ ] **Step 1: Failing integration test**

With a fake streaming provider wired into the service, POST `/api/chat` (existing router test fixture) and assert the SSE body contains, in order: `event: request-id`, **multiple** `event: answer-token` lines (more than one — proves true streaming), one `event: citations`, one `event: usage`, `event: done`. Also assert a `rag.query_audit_events` row exists with the full answer.

- [ ] **Step 2: Implement `answer_stream`**

Shape (mirrors `answer`, which stays for internal callers/tests):

```python
class ChatStreamEvent(BaseModel):
    model_config = ConfigDict(frozen=True)

    event: str          # "cache-hit" | "answer-token" | "citations" | "usage"
    payload: dict[str, Any]
```

`async def answer_stream(...) -> AsyncIterator[ChatStreamEvent]` with the same parameters as `answer`. Implementation order inside one session/transaction, copied from `answer`:

1. Validation, corpus resolution, pricing/budget checks, history condensation, question embedding — identical code (extract shared private helpers where copying would duplicate more than a few lines; `_prepare_request(...)` returning a small frozen model is the natural seam).
2. Cache lookup: on hit, yield `cache-hit`, one `answer-token` with the full cached answer, `citations`, zero-cost `usage`; write the cache-hit audit row; return.
3. Retrieval (+ rerank, + multimodal selection from Slice 3). No chunks → yield one `answer-token` with the no-results message, empty `citations`, estimated `usage`; audit; return.
4. Multimodal path (images selected): call the single-shot multimodal completion, then yield one `answer-token` with the whole answer (spec allows non-streaming multimodal), `citations`, `usage`; audit; no cache write.
5. Text path: iterate `generate_answer_stream`; for each `str` item yield `answer-token` `{"delta": item}`; when the final `AnswerGeneration` arrives, build citations exactly like `answer` does, yield `citations` and `usage`, write audit + cache (`if citations and scope_document_id is None and not multimodal_used`), commit.

Mid-stream provider failure: catch around the iteration, log with request id, and re-raise a `ApiException("RAG_PROVIDER_UNAVAILABLE", 502, ...)`; the router converts it to a final `event: error` (see Step 3). No audit row is written for a failed generation (matches current behavior where the exception aborts before audit).

6. `latency_ms` for the audit row is computed right before the audit insert — after the last content event — preserving the "end-to-end to last SSE event" semantics.

- [ ] **Step 3: Rewrite the router**

```python
@router.post("/api/chat")
async def post_chat(body: ChatRequest, request: Request) -> StreamingResponse:
    # validation, CSRF, session, rate limit: unchanged
    ...
    async def event_stream():
        yield _event("request-id", {"request_id": request_id})
        try:
            async for item in service.answer_stream(
                question=body.question,
                claims=claims,
                request_id=request_id,
                filters=filters,
                session_id=body.session_id,
                locale=body.locale,
                scope_document_id=body.document_id,
            ):
                yield _event(item.event, item.payload)
        except ApiException as exc:
            yield _event("error", {"error": {"code": exc.code, "message": exc.message, "request_id": request_id}})
            return
        yield _event("done", {})

    return StreamingResponse(event_stream(), media_type="text/event-stream", headers={"Cache-Control": "no-cache"})
```

Keep `_event` as-is. Pre-stream failures (validation, CSRF, budget, rate limit) still raise before the `StreamingResponse` is constructed and keep returning the regular JSON error envelope — only failures after streaming starts use the `error` SSE event. Delete `_stream_answer` once nothing references it; keep `ChatService.answer` (used by tests and as the non-streaming core for comparison) unless it becomes fully dead, in which case fold and delete.

- [ ] **Step 4: Run the full suite**

Run: `uv run pytest -q && uv run ruff check . && uv run mypy src tests`
Expected: green; the integration test from Step 1 proves multiple `answer-token` events.

- [ ] **Step 5: Spec + graph + commit**

`context/rag-spec.md` Generation section: replace the "custom server-side parser" future-tense sentence with present tense (implemented; multimodal and cache-hit responses emit a single whole-answer token event). Run `graphify update .`.

```bash
git add services/rag-api context/rag-spec.md graphify-out
git commit -m "feat(rag): true token streaming for chat SSE"
```

- [ ] **Step 6 (USER-OWNED): Compose acceptance**

Ask the user to run the stack and verify in chat-web that long answers render progressively (tokens appear before the answer completes) and that citations/usage still arrive at the end. Report back.
