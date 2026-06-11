# docs-web Redesign + Document-Scoped Mini Chat Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restyle the docs-web portal/viewer into a warm help center, add back-to-library navigation, and add an ephemeral document-scoped mini chat streamed from FastAPI through new Caddy routing on the docs host.

**Architecture:** FastAPI's existing `POST /api/chat` gains an optional `documentId` that forces the `published` corpus, filters retrieval to one document inside the existing branch-aware SQL, bypasses the semantic cache, and audits the scope. Caddy exposes `/api/chat` + `/api/feedback*` on `docs.{$PUBLIC_DOMAIN}`. docs-web is split from one 444-line `App.tsx` into `features/` modules with a local SSE client copied from chat-web.

**Tech Stack:** FastAPI + SQLAlchemy + Alembic + Testcontainers (rag-api), Caddy, React 18 + TypeScript + Vite + Vitest + Testing Library + i18next + lucide-react + `@helpcenter/shared-ui` (docs-web). No new libraries.

**Authoritative spec:** `docs/superpowers/specs/2026-06-10-docs-web-redesign-and-doc-chat-design.md`. If this plan and the spec conflict, the spec wins.

**Before starting:**
- Branch off `roles-refactor` (the scoped retrieval depends on the hierarchical-access schema): `git checkout roles-refactor; git checkout -b docs-redesign`
- Docker Desktop must be running (rag-api tests use Testcontainers).
- Backend commands run from `services\rag-api` (`Set-Location services\rag-api`). Frontend commands run from the repo root with `pnpm.cmd --dir apps\docs-web ...`.
- Run `graphify update .` once at the very end (Task 11), not per task.

---

## Task 1: Alembic migration — `scope_document_id` on `rag.query_audit_events`

**Files:**
- Create: `services/rag-api/alembic/versions/20260610_120000_add_scope_document_id_to_query_audit.py`
- Modify: `services/rag-api/tests/test_chat_rag.py` (no change needed for the column itself — `read_audit_state` uses `SELECT *`)

- [ ] **Step 1: Confirm the current Alembic head**

Run (from `services\rag-api`): `uv run alembic heads`
Expected: a single head, `20260522_134100`. If different, use the actual head as `down_revision` below.

- [ ] **Step 2: Create the migration file**

Create `services/rag-api/alembic/versions/20260610_120000_add_scope_document_id_to_query_audit.py`:

```python
"""Add scope_document_id to rag.query_audit_events.

Document-scoped mini-chat requests (docs-web) audit which document the retrieval
was restricted to. NULL means a normal corpus-wide chat request. See
docs/superpowers/specs/2026-06-10-docs-web-redesign-and-doc-chat-design.md.
"""

from collections.abc import Sequence

from alembic import op

revision: str = "20260610_120000"
down_revision: str | None = "20260522_134100"
branch_labels: str | Sequence[str] | None = None
depends_on: str | Sequence[str] | None = None


def upgrade() -> None:
    op.execute("ALTER TABLE rag.query_audit_events ADD COLUMN scope_document_id uuid;")


def downgrade() -> None:
    op.execute("ALTER TABLE rag.query_audit_events DROP COLUMN IF EXISTS scope_document_id;")
```

- [ ] **Step 3: Verify migrations still apply cleanly**

Run: `uv run pytest tests/test_migrations.py -q`
Expected: PASS (the suite applies all revisions against a fresh Testcontainers Postgres).

- [ ] **Step 4: Commit**

```powershell
git add services/rag-api/alembic/versions/20260610_120000_add_scope_document_id_to_query_audit.py
git commit -m "feat(rag-api): add scope_document_id audit column for doc-scoped chat"
```

---

## Task 2: Retrieval — `scope_document_id` filter in hybrid retrieval

**Files:**
- Modify: `services/rag-api/src/advanced_rag/rag/hybrid_retrieval.py`
- Test: `services/rag-api/tests/test_chat_rag.py`

- [ ] **Step 1: Write the failing test**

Add at the end of the hierarchical test block in `services/rag-api/tests/test_chat_rag.py` (right after `test_admin_global_scope_bypasses_rule_filter`):

```python
def test_scope_document_filter_limits_retrieval_to_one_accessible_document() -> None:
    with _postgres() as database:
        seeded = asyncio.run(database.seed_hierarchical_corpus())
        scoped = asyncio.run(
            _retrieve_document_ids(
                database.async_url,
                HybridRetrievalParams(
                    corpus="published",
                    user_groups=[],
                    user_organizational_unit_id=seeded["marketing"],
                    root_organizational_unit_id=seeded["empresa"],
                    scope_document_id=seeded["comunicacion_doc"],
                ),
            )
        )
        out_of_scope = asyncio.run(
            _retrieve_document_ids(
                database.async_url,
                HybridRetrievalParams(
                    corpus="published",
                    user_groups=[],
                    user_organizational_unit_id=seeded["marketing"],
                    root_organizational_unit_id=seeded["empresa"],
                    scope_document_id=seeded["sistemas_doc"],
                ),
            )
        )

    # The scope narrows retrieval to exactly one accessible document; scoping to a
    # document outside the user's branch yields nothing (access predicate still applies).
    assert scoped == {seeded["comunicacion_doc"]}
    assert out_of_scope == set()
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `uv run pytest tests/test_chat_rag.py::test_scope_document_filter_limits_retrieval_to_one_accessible_document -q`
Expected: FAIL with a Pydantic validation error (`scope_document_id` is not a known field).

- [ ] **Step 3: Implement the param and SQL filter**

In `services/rag-api/src/advanced_rag/rag/hybrid_retrieval.py`:

(a) Add the field to `HybridRetrievalParams` (after `dimension_value_filter`):

```python
    dimension_value_filter: list[UUID] | None = None
    scope_document_id: UUID | None = None
```

(b) Define the scope predicate next to `_ACCESS_RULE_PREDICATE` (module level):

```python
# Document-scope predicate for the docs-web mini chat: when a scope document is set,
# both candidate CTEs only consider that document's chunks. The access-rule predicate
# above still applies, so scoping to an inaccessible document retrieves nothing.
_SCOPE_DOCUMENT_PREDICATE = """
      AND (
          CAST(:scope_document_id AS uuid) IS NULL
          OR chunk.document_id = CAST(:scope_document_id AS uuid)
      )
"""
```

(c) Interpolate it into **both** CTEs of `HYBRID_RETRIEVAL_SQL`, immediately after each `{_ACCESS_RULE_PREDICATE}` line:

```python
      {_ACCESS_RULE_PREDICATE}
      {_SCOPE_DOCUMENT_PREDICATE}
```

(There are exactly two `{_ACCESS_RULE_PREDICATE}` occurrences: one in `vector_candidates`, one in `bm25_candidates`.)

(d) Bind the parameter in `hybrid_retrieve` (inside the `connection.execute(...)` dict, after `dimension_value_filter`):

```python
            "scope_document_id": (
                str(params.scope_document_id) if params.scope_document_id is not None else None
            ),
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `uv run pytest tests/test_chat_rag.py::test_scope_document_filter_limits_retrieval_to_one_accessible_document -q`
Expected: PASS.

- [ ] **Step 5: Run the neighboring retrieval tests for regressions**

Run: `uv run pytest tests/test_chat_rag.py -q -k "hierarchical or group_rule or admin_global or scope_document"`
Expected: all PASS.

- [ ] **Step 6: Commit**

```powershell
git add services/rag-api/src/advanced_rag/rag/hybrid_retrieval.py services/rag-api/tests/test_chat_rag.py
git commit -m "feat(rag-api): filter hybrid retrieval by scope document id"
```

---

## Task 3: ChatService doc-scoped behavior + `documentId` on the API

**Files:**
- Modify: `services/rag-api/src/advanced_rag/schemas/chat.py`
- Modify: `services/rag-api/src/advanced_rag/api/routers/chat.py`
- Modify: `services/rag-api/src/advanced_rag/rag/chat_service.py`
- Test: `services/rag-api/tests/test_chat_rag.py`

- [ ] **Step 1: Write the failing tests**

Add to `services/rag-api/tests/test_chat_rag.py` (after `test_budget_exhaustion_blocks_before_paid_provider_calls`). They reuse `seed_chat_corpus`, whose allowed chunk content is `"Wear visible credentials."`:

```python
def test_doc_scoped_chat_answers_from_scoped_document_and_bypasses_cache() -> None:
    with _postgres() as database:
        allowed_document_id = uuid4()
        denied_document_id = uuid4()
        preview_document_id = uuid4()
        asyncio.run(
            database.seed_chat_corpus(
                allowed_document_id=allowed_document_id,
                denied_document_id=denied_document_id,
                preview_document_id=preview_document_id,
                monthly_budget=Decimal("5.0000"),
            )
        )
        app = create_app(
            Settings(
                rag_database_url=database.async_url,
                openai_chat_model=CHAT_MODEL,
                openai_embedding_model=EMBEDDING_MODEL,
                openai_embedding_dimensions=EMBEDDING_DIMENSIONS,
                customer_timezone="UTC",
                enable_reranker=False,
                csrf_signing_key=TEST_CSRF_SIGNING_KEY,
            ),
            embedding_provider=FakeEmbeddingProvider(),
            llm_provider=FakeLlmProvider(),
            session_validator=FakeSessionValidator(
                ChatTokenClaims(
                    user_id=str(USER_ID),
                    role="Viewer",
                    groups=[str(ALLOWED_GROUP_ID)],
                    access_scope_hash="scope-allowed",
                    corpus="published",
                )
            ),
        )
        client = TestClient(app)
        client.cookies.set("__Host-session", "valid")
        set_csrf(client)

        request_body = {
            "question": "What credential rule applies?",
            "documentId": str(allowed_document_id),
            "sessionId": str(uuid4()),
        }
        with client.stream(
            "POST", "/api/chat", json=request_body, headers={"X-Request-ID": "req-doc-1"}
        ) as first:
            first_body = "".join(first.iter_text())
        with client.stream(
            "POST", "/api/chat", json=request_body, headers={"X-Request-ID": "req-doc-2"}
        ) as second:
            second_body = "".join(second.iter_text())

        cache_entries = asyncio.run(
            database.count_cache_entries_for_document(allowed_document_id)
        )
        state = asyncio.run(database.read_audit_state())

    assert first.status_code == 200
    assert "Wear visible credentials." in first_body
    # Identical repeated question: a corpus-wide chat would hit the semantic cache here.
    assert "event: cache-hit" not in second_body
    assert cache_entries == 0
    assert state["audit"]["cache_hit"] is False
    assert state["audit"]["scope_document_id"] == allowed_document_id
    assert state["audit"]["corpus"] == "published"


def test_doc_scoped_chat_forces_published_corpus_for_management_roles() -> None:
    with _postgres() as database:
        allowed_document_id = uuid4()
        denied_document_id = uuid4()
        preview_document_id = uuid4()
        asyncio.run(
            database.seed_chat_corpus(
                allowed_document_id=allowed_document_id,
                denied_document_id=denied_document_id,
                preview_document_id=preview_document_id,
                monthly_budget=Decimal("5.0000"),
            )
        )
        app = create_app(
            Settings(
                rag_database_url=database.async_url,
                openai_chat_model=CHAT_MODEL,
                openai_embedding_model=EMBEDDING_MODEL,
                openai_embedding_dimensions=EMBEDDING_DIMENSIONS,
                customer_timezone="UTC",
                enable_reranker=False,
                csrf_signing_key=TEST_CSRF_SIGNING_KEY,
            ),
            embedding_provider=FakeEmbeddingProvider(),
            llm_provider=FakeLlmProvider(),
            session_validator=FakeSessionValidator(
                ChatTokenClaims(
                    user_id=str(USER_ID),
                    role="DocumentEditor",
                    is_global_admin=False,
                    groups=[str(ALLOWED_GROUP_ID)],
                    access_scope_hash="scope-editor",
                    corpus="management",
                )
            ),
        )
        client = TestClient(app)
        client.cookies.set("__Host-session", "valid")
        set_csrf(client)

        with client.stream(
            "POST",
            "/api/chat",
            json={
                "question": "What credential rule applies?",
                "documentId": str(allowed_document_id),
            },
            headers={"X-Request-ID": "req-doc-3"},
        ) as response:
            body = "".join(response.iter_text())

        state = asyncio.run(database.read_audit_state())

    assert response.status_code == 200
    # The published chunk is found even though the claims corpus is "management",
    # proving the doc-scoped path forces the published corpus.
    assert "Wear visible credentials." in body
    assert state["audit"]["corpus"] == "published"


def test_doc_scoped_chat_outside_access_returns_scoped_no_results_message() -> None:
    with _postgres() as database:
        allowed_document_id = uuid4()
        denied_document_id = uuid4()
        preview_document_id = uuid4()
        asyncio.run(
            database.seed_chat_corpus(
                allowed_document_id=allowed_document_id,
                denied_document_id=denied_document_id,
                preview_document_id=preview_document_id,
                monthly_budget=Decimal("5.0000"),
            )
        )
        app = create_app(
            Settings(
                rag_database_url=database.async_url,
                openai_chat_model=CHAT_MODEL,
                openai_embedding_model=EMBEDDING_MODEL,
                openai_embedding_dimensions=EMBEDDING_DIMENSIONS,
                customer_timezone="UTC",
                enable_reranker=False,
                csrf_signing_key=TEST_CSRF_SIGNING_KEY,
            ),
            embedding_provider=FakeEmbeddingProvider(),
            llm_provider=FakeLlmProvider(),
            session_validator=FakeSessionValidator(
                ChatTokenClaims(
                    user_id=str(USER_ID),
                    role="Viewer",
                    groups=[str(ALLOWED_GROUP_ID)],
                    access_scope_hash="scope-allowed",
                    corpus="published",
                )
            ),
        )
        client = TestClient(app)
        client.cookies.set("__Host-session", "valid")
        set_csrf(client)

        with client.stream(
            "POST",
            "/api/chat",
            json={
                "question": "What credential rule applies?",
                # The user is NOT in the denied document's group.
                "documentId": str(denied_document_id),
                "locale": "en-US",
            },
            headers={"X-Request-ID": "req-doc-4"},
        ) as response:
            body = "".join(response.iter_text())

        state = asyncio.run(database.read_audit_state())

    assert response.status_code == 200
    # ASCII assertion on purpose: the SSE payload is JSON with ensure_ascii escapes,
    # so the Spanish copy would appear as é sequences in the raw body.
    assert "I couldn't find information in this document" in body
    assert "Denied group content." not in body
    assert state["audit"]["scope_document_id"] == denied_document_id
    assert state["citation_document_ids"] == []
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `uv run pytest tests/test_chat_rag.py -q -k "doc_scoped_chat"`
Expected: FAIL — `documentId` is silently ignored today, so the cache-bypass and scope-audit assertions fail (the audit row has no `scope_document_id` value / cache-hit occurs on the second call).

- [ ] **Step 3: Add `document_id` to the request schema**

In `services/rag-api/src/advanced_rag/schemas/chat.py`, change `ChatRequest`:

```python
class ChatRequest(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    question: str = Field(min_length=1, max_length=4000)
    session_id: UUID | None = Field(default=None, alias="sessionId")
    document_id: UUID | None = Field(default=None, alias="documentId")
    filters: ChatFilters | None = None
    locale: str | None = None
```

- [ ] **Step 4: Pass it through the router**

In `services/rag-api/src/advanced_rag/api/routers/chat.py`, in `post_chat`, change the `service.answer(...)` call:

```python
    answer = await service.answer(
        question=body.question,
        claims=claims,
        request_id=request_id,
        filters=filters,
        session_id=body.session_id,
        locale=body.locale,
        scope_document_id=body.document_id,
    )
```

- [ ] **Step 5: Implement the scoped behavior in `ChatService`**

All edits in `services/rag-api/src/advanced_rag/rag/chat_service.py`.

(a) `answer` signature — add the keyword-only parameter:

```python
    async def answer(
        self,
        *,
        question: str,
        claims: ChatTokenClaims,
        request_id: str,
        filters: list[UUID] | None = None,
        session_id: UUID | None = None,
        locale: str | None = None,
        scope_document_id: UUID | None = None,
    ) -> ChatAnswer:
```

(b) Corpus selection — replace the single line `corpus = "published" if claims.role == "Viewer" else claims.corpus` with:

```python
        if scope_document_id is not None:
            # Doc-scoped mini chat always answers from published content, regardless of
            # role, preserving the "public chat retrieves only Published content" invariant.
            corpus = "published"
        else:
            corpus = "published" if claims.role == "Viewer" else claims.corpus
```

(c) Cache lookup — wrap the existing `cache_hit = await self._lookup_cache(...)` assignment:

```python
            cache_hit = None
            if scope_document_id is None:
                cache_hit = await self._lookup_cache(
                    session,
                    corpus=corpus,
                    access_scope_hash=claims.access_scope_hash,
                    filters_hash=filters_hash,
                    question_embedding=question_embedding,
                )
```

(d) Retrieval call — add the argument to the existing `self._retrieve_chunks(...)` call:

```python
            chunks, rerank_audit = await self._retrieve_chunks(
                session,
                corpus=corpus,
                claims=claims,
                question=retrieval_question,
                question_embedding=question_embedding,
                filters=filters,
                scope_document_id=scope_document_id,
            )
```

(e) No-results copy — in the `if not chunks:` branch, replace `answer=_no_results_message(active_locale),` with:

```python
                completion = AnswerGeneration(
                    answer=(
                        _no_results_message_scoped(active_locale)
                        if scope_document_id is not None
                        else _no_results_message(active_locale)
                    ),
                    cited_chunk_ids=[],
                    usage=_zero_usage(),
                )
```

(f) Audit insert (generated-answer path) — add the new argument to the existing `self._insert_audit(...)` call (after `rerank_audit=rerank_audit,`):

```python
                rerank_audit=rerank_audit,
                scope_document_id=scope_document_id,
            )
```

(g) Cache write — change the guard `if citations:` to:

```python
            if citations and scope_document_id is None:
```

(h) `_retrieve_chunks` — add the parameter and forward it:

```python
    async def _retrieve_chunks(
        self,
        session: AsyncSession,
        *,
        corpus: str,
        claims: ChatTokenClaims,
        question: str,
        question_embedding: list[float],
        filters: list[UUID] | None,
        scope_document_id: UUID | None = None,
    ) -> tuple[list[RetrievedChunk], dict | None]:
```

and inside `HybridRetrievalParams(...)` add:

```python
            dimension_value_filter=filters,
            scope_document_id=scope_document_id,
```

(i) `_insert_audit` — add the keyword-only parameter (after `rerank_audit: dict | None,`):

```python
        rerank_audit: dict | None,
        scope_document_id: UUID | None = None,
    ) -> None:
```

then add `scope_document_id` to the INSERT column list (after `reranker_model, reranker_score`):

```sql
                    reranker_model, reranker_score,
                    scope_document_id
```

and to the VALUES list (after `:reranker_model, :reranker_score`):

```sql
                    :reranker_model, :reranker_score,
                    :scope_document_id
```

and to the bound-parameters dict (after the `reranker_score` entry):

```python
                "scope_document_id": scope_document_id,
```

(j) `_audit_cache_hit` — its `self._insert_audit(...)` call gets an explicit `scope_document_id=None,` after `rerank_audit=None,` (the cache path can never run scoped because of (c), but keep the call site explicit).

(k) Scoped no-results helper — add directly below `_no_results_message`:

```python
def _no_results_message_scoped(locale: str) -> str:
    if locale.startswith("es"):
        return "No encontré información en este documento para responder esa pregunta."
    return "I couldn't find information in this document to answer that question."
```

- [ ] **Step 6: Run the new tests**

Run: `uv run pytest tests/test_chat_rag.py -q -k "doc_scoped_chat"`
Expected: all 3 PASS.

- [ ] **Step 7: Run the full rag chat suite + linters for regressions**

Run: `uv run pytest tests/test_chat_rag.py -q; uv run ruff check .; uv run mypy src tests`
Expected: all tests PASS, ruff/mypy clean.

- [ ] **Step 8: Commit**

```powershell
git add services/rag-api/src/advanced_rag/schemas/chat.py services/rag-api/src/advanced_rag/api/routers/chat.py services/rag-api/src/advanced_rag/rag/chat_service.py services/rag-api/tests/test_chat_rag.py
git commit -m "feat(rag-api): document-scoped chat with cache bypass and scope audit"
```

---

## Task 4: Exclude doc-scoped turns from the chat session list

**Files:**
- Modify: `services/rag-api/src/advanced_rag/rag/chat_service.py` (only `list_sessions`)
- Test: `services/rag-api/tests/test_chat_rag.py`

- [ ] **Step 1: Write the failing test**

Add after `test_doc_scoped_chat_outside_access_returns_scoped_no_results_message`:

```python
def test_doc_scoped_turns_are_excluded_from_session_list() -> None:
    with _postgres() as database:
        allowed_document_id = uuid4()
        denied_document_id = uuid4()
        preview_document_id = uuid4()
        asyncio.run(
            database.seed_chat_corpus(
                allowed_document_id=allowed_document_id,
                denied_document_id=denied_document_id,
                preview_document_id=preview_document_id,
                monthly_budget=Decimal("5.0000"),
            )
        )
        app = create_app(
            Settings(
                rag_database_url=database.async_url,
                openai_chat_model=CHAT_MODEL,
                openai_embedding_model=EMBEDDING_MODEL,
                openai_embedding_dimensions=EMBEDDING_DIMENSIONS,
                customer_timezone="UTC",
                enable_reranker=False,
                csrf_signing_key=TEST_CSRF_SIGNING_KEY,
            ),
            embedding_provider=FakeEmbeddingProvider(),
            llm_provider=FakeLlmProvider(),
            session_validator=FakeSessionValidator(
                ChatTokenClaims(
                    user_id=str(USER_ID),
                    role="Viewer",
                    groups=[str(ALLOWED_GROUP_ID)],
                    access_scope_hash="scope-allowed",
                    corpus="published",
                )
            ),
        )
        client = TestClient(app)
        client.cookies.set("__Host-session", "valid")
        set_csrf(client)

        doc_session_id = uuid4()
        corpus_session_id = uuid4()
        with client.stream(
            "POST",
            "/api/chat",
            json={
                "question": "What credential rule applies?",
                "documentId": str(allowed_document_id),
                "sessionId": str(doc_session_id),
            },
            headers={"X-Request-ID": "req-list-1"},
        ) as scoped:
            scoped.read()
        with client.stream(
            "POST",
            "/api/chat",
            json={
                "question": "What credential rule applies everywhere?",
                "sessionId": str(corpus_session_id),
            },
            headers={"X-Request-ID": "req-list-2"},
        ) as unscoped:
            unscoped.read()

        sessions_response = client.get("/api/chat/sessions")

    assert sessions_response.status_code == 200
    session_ids = [item["sessionId"] for item in sessions_response.json()["sessions"]]
    # The ephemeral doc-chat session never appears in chat-web's drawer.
    assert session_ids == [str(corpus_session_id)]
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `uv run pytest tests/test_chat_rag.py::test_doc_scoped_turns_are_excluded_from_session_list -q`
Expected: FAIL — both session ids are listed.

- [ ] **Step 3: Implement the exclusion**

In `services/rag-api/src/advanced_rag/rag/chat_service.py`, in `list_sessions`, extend the `ranked` CTE WHERE clause:

```sql
                        from rag.query_audit_events
                        where user_id = :user_id
                          and session_id is not null
                          and scope_document_id is null
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `uv run pytest tests/test_chat_rag.py::test_doc_scoped_turns_are_excluded_from_session_list -q`
Expected: PASS.

- [ ] **Step 5: Full backend verification**

Run: `uv run pytest -q; uv run ruff check .; uv run mypy src tests`
Expected: all PASS / clean. (Full suite needs Docker.)

- [ ] **Step 6: Commit**

```powershell
git add services/rag-api/src/advanced_rag/rag/chat_service.py services/rag-api/tests/test_chat_rag.py
git commit -m "feat(rag-api): hide doc-scoped turns from the chat session list"
```

---

## Task 5: Caddy — route doc-chat paths on the docs host to FastAPI

**Files:**
- Modify: `infra/compose/Caddyfile` (the `docs.{$PUBLIC_DOMAIN}` block)

- [ ] **Step 1: Edit the docs site block**

Replace the existing `docs.{$PUBLIC_DOMAIN}` block with:

```caddyfile
docs.{$PUBLIC_DOMAIN} {
  tls internal
  import security_headers

  @rag path /api/chat /api/feedback /api/feedback/*
  handle @rag {
    reverse_proxy rag-api:8000 {
      flush_interval -1
    }
  }

  @api path /api/*
  handle @api {
    reverse_proxy dotnet-api:8080
  }

  handle {
    reverse_proxy docs-web:80
  }
}
```

Order matters: `@rag` must appear before `@api` (first matching `handle` wins). `/api/chat` is an exact path match, so `/api/chat/sessions` deliberately still falls through to .NET (404) — doc chat is ephemeral and the session endpoints stay private to `chat.*`.

- [ ] **Step 2: Validate the Compose config**

Run (repo root): `docker compose --env-file infra/compose/.env.example -f infra/compose/compose.yaml config`
Expected: renders without errors.

- [ ] **Step 3: Commit**

```powershell
git add infra/compose/Caddyfile
git commit -m "feat(infra): route docs-host doc-chat and feedback paths to rag-api"
```

---

## Task 6: docs-web foundations — shared libs, error keys, final i18n files

**Files:**
- Create: `apps/docs-web/src/lib/csrf.ts`
- Create: `apps/docs-web/src/lib/dates.ts`
- Create: `apps/docs-web/src/lib/documentState.ts`
- Create: `apps/docs-web/src/lib/documentType.ts`
- Create: `apps/docs-web/src/lib/url.ts`
- Modify: `apps/docs-web/src/api/viewer.ts` (full replacement below)
- Modify: `apps/docs-web/src/i18n/es-AR.json`, `apps/docs-web/src/i18n/en-US.json` (full replacement)
- Modify: `apps/docs-web/src/App.tsx` (error-key plumbing only)
- Test: `apps/docs-web/src/App.test.tsx`

- [ ] **Step 1: Update the error-state tests (they will fail until Step 6)**

In `apps/docs-web/src/App.test.tsx`, replace the `test.each` error block and the loading test:

```tsx
test('shows loading exchange state', () => {
  vi.spyOn(globalThis, 'fetch').mockReturnValue(new Promise<Response>(() => undefined))

  render(<App />)

  expect(screen.getByText('Validando enlace…')).toBeInTheDocument()
})
```

```tsx
test.each([
  ['AUTH_FORBIDDEN', 'No tenés permiso para abrir este documento.'],
  ['AUTH_REQUIRED', 'Iniciá sesión para continuar.'],
  ['NOT_FOUND', 'No encontramos el documento solicitado.'],
])('shows safe error state for %s', async (code, message) => {
  mockFetch([
    errorResponse(code),
  ])

  render(<App />)

  expect(await screen.findByRole('alert')).toHaveTextContent(message)
})
```

- [ ] **Step 2: Run the tests to verify the new expectations fail**

Run: `pnpm.cmd --dir apps\docs-web test -- --run src/App.test.tsx`
Expected: the loading test and 2 of the 3 error cases FAIL (old copy still rendered).

- [ ] **Step 3: Create the shared libs**

Create `apps/docs-web/src/lib/csrf.ts` (no module-level token cache, on purpose: the
existing docs-web tests assert exact fetch sequences per flow, and the current `viewer.ts`
also re-fetches before every mutating call):

```ts
import { parseApiError } from './api-error'

export async function ensureCsrfToken(): Promise<string> {
  const response = await fetch('/api/csrf', {
    credentials: 'include',
    headers: { 'X-Request-ID': createRequestId() },
  })
  const text = await response.text()
  const body = text.length > 0 ? JSON.parse(text) : null
  if (!response.ok) {
    throw parseApiError(response, body)
  }

  return response.headers.get('X-CSRF-Token') ?? ''
}

export function createRequestId(): string {
  if ('randomUUID' in crypto) {
    return crypto.randomUUID()
  }

  return `request-${Date.now()}`
}
```

Create `apps/docs-web/src/lib/dates.ts`:

```ts
export function formatDateTime(value: string, locale: string): string {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) {
    return '-'
  }

  return new Intl.DateTimeFormat(locale, {
    dateStyle: 'short',
    timeStyle: 'short',
  }).format(date)
}

export function formatRelativeDate(value: string, locale: string): string {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) {
    return ''
  }

  const formatter = new Intl.RelativeTimeFormat(locale, { numeric: 'auto' })
  const diffMinutes = Math.round((date.getTime() - Date.now()) / 60_000)
  if (Math.abs(diffMinutes) < 60) {
    return formatter.format(diffMinutes, 'minute')
  }
  const diffHours = Math.round(diffMinutes / 60)
  if (Math.abs(diffHours) < 24) {
    return formatter.format(diffHours, 'hour')
  }
  const diffDays = Math.round(diffHours / 24)
  if (Math.abs(diffDays) < 30) {
    return formatter.format(diffDays, 'day')
  }
  const diffMonths = Math.round(diffDays / 30)
  if (Math.abs(diffMonths) < 12) {
    return formatter.format(diffMonths, 'month')
  }
  return formatter.format(Math.round(diffMonths / 12), 'year')
}
```

Create `apps/docs-web/src/lib/documentState.ts`:

```ts
const STATE_KEYS: Record<string, string> = {
  Published: 'states.published',
  Draft: 'states.draft',
  'In Review': 'states.in_review',
  Archived: 'states.archived',
}

export function documentStateKey(state: string): string | null {
  return STATE_KEYS[state] ?? null
}
```

Create `apps/docs-web/src/lib/documentType.ts`:

```ts
import { BookOpen, Compass, FileText, ListChecks, ShieldCheck, type LucideIcon } from 'lucide-react'

export function typeTintIndex(documentType: string): number {
  let sum = 0
  for (const char of documentType) {
    sum += char.charCodeAt(0)
  }
  return sum % 6
}

export function typeIcon(documentType: string): LucideIcon {
  const normalized = documentType.toLowerCase()
  if (normalized.includes('manual')) {
    return BookOpen
  }
  if (normalized.includes('polit') || normalized.includes('polít') || normalized.includes('policy')) {
    return ShieldCheck
  }
  if (normalized.includes('proced')) {
    return ListChecks
  }
  if (normalized.includes('guia') || normalized.includes('guía') || normalized.includes('guide')) {
    return Compass
  }
  return FileText
}
```

Create `apps/docs-web/src/lib/url.ts`:

```ts
export function removeHandoffFromUrl(): void {
  const url = new URL(window.location.href)
  url.searchParams.delete('handoff')
  window.history.replaceState(null, '', `${url.pathname}${url.search}${url.hash}`)
}
```

- [ ] **Step 4: Replace `apps/docs-web/src/api/viewer.ts`**

Full new content (same endpoints; CSRF/request-id moved to `lib/csrf.ts`; `viewerErrorMessage` becomes `viewerErrorKey`):

```ts
import { ApiError, parseApiError } from '../lib/api-error'
import { createRequestId, ensureCsrfToken } from '../lib/csrf'

export interface ViewerDocument {
  documentId: string
  documentVersionId: string
  title: string
  state: string
  documentType: string
  audience: string
  contentHtml: string
  tokenExpiresAt: string
}

export interface SessionUser {
  id: string
  email: string
  displayName: string
  roles: string[]
  groups: ViewerDocumentGroup[]
}

export interface SessionResponse {
  user: SessionUser
}

export interface ViewerDocumentGroup {
  id: string
  name: string
}

export interface ViewerCatalogDocument {
  id: string
  title: string
  state: string
  documentType: string
  audience: string
  allowedGroups: ViewerDocumentGroup[]
  updatedAt: string
}

export interface ViewerDocumentCatalog {
  documents: ViewerCatalogDocument[]
  groups: ViewerDocumentGroup[]
}

export async function getSession(): Promise<SessionResponse> {
  return requestJson<SessionResponse>('/api/session')
}

export async function login(email: string, password: string): Promise<SessionResponse> {
  const csrfToken = await ensureCsrfToken()
  return requestJson<SessionResponse>('/api/auth/login', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-CSRF-Token': csrfToken,
    },
    body: JSON.stringify({ email, password }),
  })
}

export async function listViewerDocuments(): Promise<ViewerDocumentCatalog> {
  return requestJson<ViewerDocumentCatalog>('/api/viewer/documents')
}

export async function createViewerLink(documentId: string, purpose: 'chat' | 'management'): Promise<string> {
  const csrfToken = await ensureCsrfToken()
  const response = await requestJson<{ url: string }>('/api/viewer/links', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-CSRF-Token': csrfToken,
    },
    body: JSON.stringify({ documentId, purpose }),
  })
  return response.url
}

export async function consumeViewerHandoff(
  handoffCode: string,
  documentId: string,
): Promise<SessionResponse> {
  const csrfToken = await ensureCsrfToken()
  return requestJson<SessionResponse>('/api/viewer/session-handoff', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-CSRF-Token': csrfToken,
    },
    body: JSON.stringify({ documentId, handoffCode }),
  })
}

export async function consumeSessionHandoff(handoffCode: string): Promise<SessionResponse> {
  const csrfToken = await ensureCsrfToken()
  return requestJson<SessionResponse>('/api/auth/session-handoffs/consume', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-CSRF-Token': csrfToken,
    },
    body: JSON.stringify({ handoffCode, target: 'docs' }),
  })
}

export async function getViewerDocument(documentId: string): Promise<ViewerDocument> {
  return requestJson<ViewerDocument>(`/api/viewer/document?documentId=${encodeURIComponent(documentId)}`)
}

export function viewerErrorKey(error: unknown): string {
  const code = error instanceof ApiError ? error.code : 'INTERNAL_ERROR'
  const keys: Record<string, string> = {
    AUTH_FORBIDDEN: 'errors.forbidden',
    AUTH_REQUIRED: 'errors.auth_required',
    NOT_FOUND: 'errors.not_found',
    VIEWER_HANDOFF_EXPIRED: 'errors.handoff_expired',
    VIEWER_HANDOFF_INVALID: 'errors.handoff_invalid',
    VIEWER_HANDOFF_USED: 'errors.handoff_used',
  }
  return keys[code] ?? 'errors.generic'
}

async function requestJson<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers)
  if (!headers.has('X-Request-ID')) {
    headers.set('X-Request-ID', createRequestId())
  }

  const response = await fetch(path, {
    ...init,
    credentials: 'include',
    headers,
  })
  const body = await readJson(response)
  if (!response.ok) {
    throw parseApiError(response, body)
  }

  return body as T
}

async function readJson(response: Response): Promise<unknown> {
  const text = await response.text()
  return text.length > 0 ? JSON.parse(text) : null
}
```

- [ ] **Step 5: Replace both i18n resource files**

`apps/docs-web/src/i18n/es-AR.json` (full content; `portal.header_copy` and `viewer.instruction_viewer_title` are transitional keys removed in Tasks 7/8):

```json
{
  "app": {
    "name": "Centro de Ayuda",
    "loading": "Cargando…",
    "error_generic": "Ocurrió un error inesperado."
  },
  "common": {
    "language": "Idioma",
    "toggle_theme": "Cambiar tema"
  },
  "portal": {
    "title": "Biblioteca de documentos",
    "header_copy": "Encontrá guías y documentación según tus permisos.",
    "subtitle": "Encontrá guías y documentación según tus permisos.",
    "search_placeholder": "Buscar por título, tipo o audiencia…",
    "filter_all": "Todos",
    "filters_label": "Filtrar por tipo de documento",
    "empty_title": "Sin documentos para mostrar",
    "empty_no_access": "Todavía no hay documentos disponibles para tu usuario.",
    "empty_filtered": "No encontramos documentos con los filtros actuales.",
    "clear_filters": "Limpiar filtros",
    "open_document": "Abrir {{title}}",
    "updated_ago": "Actualizado {{when}}"
  },
  "viewer": {
    "instruction_viewer_title": "Visor de instrucciones",
    "back_to_library": "Volver a la biblioteca",
    "nav_label": "Navegación del documento",
    "session_valid_until": "Sesión válida hasta {{time}} · Acceso verificado",
    "draft_banner": "Estás viendo una versión en borrador. No es visible para usuarios finales.",
    "review_banner": "Estás viendo una versión en revisión. No es visible para usuarios finales.",
    "loading": "Validando enlace…"
  },
  "docChat": {
    "open": "Preguntale a este documento",
    "close": "Cerrar chat",
    "title": "Preguntale a este documento",
    "subtitle": "Respuestas basadas solo en este documento.",
    "empty_hint": "Hacé una pregunta sobre el contenido de este documento.",
    "suggestion_1": "¿De qué trata este documento?",
    "suggestion_2": "Resumime los puntos principales.",
    "placeholder": "Escribí tu pregunta…",
    "send": "Enviar",
    "sending": "Generando respuesta…",
    "retry": "Reintentar",
    "feedback_up": "Respuesta útil",
    "feedback_down": "Respuesta no útil",
    "feedback_comment_placeholder": "Contanos qué mejorarías (opcional)",
    "feedback_send": "Enviar feedback",
    "feedback_thanks": "Gracias por tu feedback.",
    "feedback_error": "No pudimos guardar tu feedback. Probá de nuevo.",
    "error_budget": "Alcanzaste tu límite mensual de uso de IA. Podés seguir leyendo el documento sin problema.",
    "error_rate_limited": "Demasiadas preguntas seguidas. Esperá unos segundos y probá de nuevo.",
    "error_generic": "No pudimos responder tu pregunta. Probá de nuevo."
  },
  "errors": {
    "auth_required": "Iniciá sesión para continuar.",
    "forbidden": "No tenés permiso para abrir este documento.",
    "not_found": "No encontramos el documento solicitado.",
    "handoff_expired": "El enlace de acceso expiró. Volvé a abrir el documento desde la app.",
    "handoff_invalid": "El enlace de acceso no es válido. Volvé a abrir el documento desde la app.",
    "handoff_used": "Este enlace de acceso ya fue usado. Volvé a abrir el documento desde la app.",
    "generic": "No pudimos abrir el documento."
  },
  "states": {
    "published": "Publicado",
    "draft": "Borrador",
    "in_review": "En revisión",
    "archived": "Archivado"
  },
  "login": {
    "eyebrow": "Documentos",
    "title": "Iniciar sesión",
    "email": "Email",
    "password": "Contraseña",
    "submit": "Entrar",
    "failed": "No se pudo iniciar sesión."
  }
}
```

`apps/docs-web/src/i18n/en-US.json` (full content):

```json
{
  "app": {
    "name": "Help Center",
    "loading": "Loading…",
    "error_generic": "An unexpected error occurred."
  },
  "common": {
    "language": "Language",
    "toggle_theme": "Change theme"
  },
  "portal": {
    "title": "Document library",
    "header_copy": "Find guides and documentation based on your permissions.",
    "subtitle": "Find guides and documentation based on your permissions.",
    "search_placeholder": "Search by title, type, or audience…",
    "filter_all": "All",
    "filters_label": "Filter by document type",
    "empty_title": "No documents to show",
    "empty_no_access": "There are no documents available for your user yet.",
    "empty_filtered": "No documents match the current filters.",
    "clear_filters": "Clear filters",
    "open_document": "Open {{title}}",
    "updated_ago": "Updated {{when}}"
  },
  "viewer": {
    "instruction_viewer_title": "Instruction viewer",
    "back_to_library": "Back to the library",
    "nav_label": "Document navigation",
    "session_valid_until": "Session valid until {{time}} · Access verified",
    "draft_banner": "You are viewing a draft version. It is not visible to end users.",
    "review_banner": "You are viewing a version in review. It is not visible to end users.",
    "loading": "Validating link…"
  },
  "docChat": {
    "open": "Ask this document",
    "close": "Close chat",
    "title": "Ask this document",
    "subtitle": "Answers based only on this document.",
    "empty_hint": "Ask a question about this document's content.",
    "suggestion_1": "What is this document about?",
    "suggestion_2": "Summarize the main points.",
    "placeholder": "Type your question…",
    "send": "Send",
    "sending": "Generating answer…",
    "retry": "Retry",
    "feedback_up": "Helpful answer",
    "feedback_down": "Unhelpful answer",
    "feedback_comment_placeholder": "Tell us what you would improve (optional)",
    "feedback_send": "Send feedback",
    "feedback_thanks": "Thanks for your feedback.",
    "feedback_error": "We couldn't save your feedback. Try again.",
    "error_budget": "You reached your monthly AI usage limit. You can keep reading the document without any problem.",
    "error_rate_limited": "Too many questions in a row. Wait a few seconds and try again.",
    "error_generic": "We couldn't answer your question. Try again."
  },
  "errors": {
    "auth_required": "Sign in to continue.",
    "forbidden": "You don't have permission to open this document.",
    "not_found": "We couldn't find the requested document.",
    "handoff_expired": "The access link expired. Open the document again from the app.",
    "handoff_invalid": "The access link is not valid. Open the document again from the app.",
    "handoff_used": "This access link was already used. Open the document again from the app.",
    "generic": "We couldn't open the document."
  },
  "states": {
    "published": "Published",
    "draft": "Draft",
    "in_review": "In review",
    "archived": "Archived"
  },
  "login": {
    "eyebrow": "Documents",
    "title": "Sign in",
    "email": "Email",
    "password": "Password",
    "submit": "Sign in",
    "failed": "Could not sign in."
  }
}
```

- [ ] **Step 6: Re-plumb `App.tsx` to error keys (minimal edits)**

In `apps/docs-web/src/App.tsx`:

(a) In the import from `'./api/viewer'`, replace `viewerErrorMessage,` with `viewerErrorKey,`.

(b) `ViewerLinkApp` initial state:

```tsx
  const [state, setState] = useState<ViewerState>({
    status: 'loading',
    message: 'viewer.loading',
  })
```

(c) Both `catch` blocks that call `setState({ status: 'error', message: viewerErrorMessage(error) })` become:

```tsx
          setState({ status: 'error', message: viewerErrorKey(error) })
```

(d) The two `EmptyState` usages in `ViewerLinkApp` translate the stored key:

```tsx
        {state.status === 'loading' ? (
          <EmptyState
            className="state-panel"
            title={t(state.message)}
            aria-live="polite"
            icon={<FileText size={22} aria-hidden="true" />}
          />
        ) : null}

        {state.status === 'error' ? (
          <EmptyState
            className="state-panel error"
            title={t(state.message)}
            role="alert"
            icon={<AlertTriangle size={22} aria-hidden="true" />}
          />
        ) : null}
```

(e) In `DocumentPortalApp`, the error branch `setState({ status: 'error', message: viewerErrorMessage(error) })` becomes `viewerErrorKey(error)`, and its error `EmptyState` renders `title={t(state.message)}`.

(f) In `DocsLoginPage`, replace the catch body with:

```tsx
      setError('login.failed')
```

and render `{error ? <p className="status-message error">{t(error)}</p> : null}` — add `const { t } = useTranslation()` at the top of `DocsLoginPage` and the `useTranslation` import is already present in the module.

- [ ] **Step 7: Run tests, typecheck, build**

Run: `pnpm.cmd --dir apps\docs-web test -- --run src/App.test.tsx; pnpm.cmd --dir apps\docs-web typecheck; pnpm.cmd --dir apps\docs-web build`
Expected: all tests PASS (including the Step 1 updates), typecheck and build clean.

- [ ] **Step 8: Commit**

```powershell
git add apps/docs-web/src
git commit -m "feat(docs-web): shared client libs, error-code i18n keys, final locale resources"
```

---

## Task 7: Portal + login in final form

**Files:**
- Create: `apps/docs-web/src/features/portal/DocumentPortalApp.tsx`
- Create: `apps/docs-web/src/features/portal/DocumentPortal.tsx`
- Create: `apps/docs-web/src/features/portal/DocumentCard.tsx`
- Create: `apps/docs-web/src/features/auth/DocsLoginPage.tsx`
- Modify: `apps/docs-web/src/App.tsx` (remove the moved code, import the feature)
- Modify: `apps/docs-web/src/App.css` (full replacement)
- Modify: `apps/docs-web/src/i18n/es-AR.json`, `en-US.json` (delete `portal.header_copy`)
- Test: `apps/docs-web/src/App.test.tsx`

- [ ] **Step 1: Update/extend the portal tests (failing first)**

In `apps/docs-web/src/App.test.tsx`:

(a) Replace `test('renders the independent document portal grouped by category', ...)` with:

```tsx
test('renders the portal with document-type chips and cards', async () => {
  setLocation('https://docs.localhost/')
  mockFetch([
    jsonResponse({
      user: {
        id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
        email: 'viewer@example.com',
        displayName: 'Viewer User',
        roles: ['Viewer'],
        groups: [{ id: '33333333-3333-3333-3333-333333333333', name: 'Legales' }],
      },
    }),
    jsonResponse({
      groups: [{ id: '33333333-3333-3333-3333-333333333333', name: 'Legales' }],
      documents: [
        {
          id: '55555555-5555-5555-5555-555555555555',
          title: 'Manual legal',
          state: 'Published',
          documentType: 'Manual',
          audience: 'Legal',
          allowedGroups: [{ id: '33333333-3333-3333-3333-333333333333', name: 'Legales' }],
          updatedAt: '2026-05-22T12:00:00Z',
        },
        {
          id: '66666666-6666-6666-6666-666666666666',
          title: 'Política de viajes',
          state: 'Published',
          documentType: 'Política',
          audience: 'Todos',
          allowedGroups: [],
          updatedAt: '2026-06-01T12:00:00Z',
        },
      ],
    }),
  ])
  const user = userEvent.setup()

  render(<App />)

  expect(await screen.findByRole('heading', { name: 'Biblioteca de documentos' })).toBeInTheDocument()
  // Filter chips come from document types, not access groups.
  expect(screen.getByRole('button', { name: 'Manual' })).toBeInTheDocument()
  expect(screen.queryByRole('button', { name: 'Legales' })).not.toBeInTheDocument()
  expect(screen.getByRole('heading', { name: 'Manual legal' })).toBeInTheDocument()

  await user.click(screen.getByRole('button', { name: 'Política' }))

  expect(screen.queryByRole('heading', { name: 'Manual legal' })).not.toBeInTheDocument()
  expect(screen.getByRole('heading', { name: 'Política de viajes' })).toBeInTheDocument()

  // "Todos" resets the type filter (the clear-filters button only renders on the
  // filtered-empty state, which this flow never reaches).
  await user.click(screen.getByRole('button', { name: 'Todos' }))
  expect(screen.getByRole('heading', { name: 'Manual legal' })).toBeInTheDocument()
})
```

(b) Replace the empty-state test:

```tsx
test('shows a semantic empty state when the document portal has no visible documents', async () => {
  setLocation('https://docs.localhost/')
  mockFetch([
    jsonResponse({
      user: {
        id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
        email: 'viewer@example.com',
        displayName: 'Viewer User',
        roles: ['Viewer'],
        groups: [],
      },
    }),
    jsonResponse({
      groups: [],
      documents: [],
    }),
  ])

  render(<App />)

  expect(await screen.findByRole('heading', { name: 'Sin documentos para mostrar' })).toBeInTheDocument()
  expect(screen.getByText('Todavía no hay documentos disponibles para tu usuario.')).toBeInTheDocument()
})
```

(c) Update the login test heading to the accented copy:

```tsx
  const heading = await screen.findByRole('heading', { name: 'Iniciar sesión' })
```

(d) Add a filtered-empty + clear-filters test:

```tsx
test('shows the filtered empty state with a clear-filters action', async () => {
  setLocation('https://docs.localhost/')
  mockFetch([
    jsonResponse({
      user: {
        id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
        email: 'viewer@example.com',
        displayName: 'Viewer User',
        roles: ['Viewer'],
        groups: [],
      },
    }),
    jsonResponse({
      groups: [],
      documents: [
        {
          id: '55555555-5555-5555-5555-555555555555',
          title: 'Manual legal',
          state: 'Published',
          documentType: 'Manual',
          audience: 'Legal',
          allowedGroups: [],
          updatedAt: '2026-05-22T12:00:00Z',
        },
      ],
    }),
  ])
  const user = userEvent.setup()

  render(<App />)

  await user.type(await screen.findByPlaceholderText('Buscar por título, tipo o audiencia…'), 'inexistente')

  expect(await screen.findByText('No encontramos documentos con los filtros actuales.')).toBeInTheDocument()

  await user.click(screen.getByRole('button', { name: 'Limpiar filtros' }))

  expect(screen.getByRole('heading', { name: 'Manual legal' })).toBeInTheDocument()
})
```

- [ ] **Step 2: Run the tests to verify the new ones fail**

Run: `pnpm.cmd --dir apps\docs-web test -- --run src/App.test.tsx`
Expected: the portal tests FAIL (type chips and new copy don't exist yet).

- [ ] **Step 3: Create `apps/docs-web/src/features/auth/DocsLoginPage.tsx`**

```tsx
import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import {
  AuthCardHeader,
  AuthShell,
  Button,
  DarkModeToggle,
  Input,
  LanguageSelect,
} from '@helpcenter/shared-ui'
import { login, type SessionUser } from '../../api/viewer'

export function DocsLoginPage({ onAuthenticated }: { onAuthenticated: (user: SessionUser) => void }) {
  const { t, i18n } = useTranslation()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [errorKey, setErrorKey] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setErrorKey(null)
    setIsSubmitting(true)
    try {
      const session = await login(email, password)
      onAuthenticated(session.user)
    } catch {
      setErrorKey('login.failed')
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <AuthShell>
      <div className="auth-toolbar">
        <LanguageSelect
          label={t('common.language')}
          value={i18n.resolvedLanguage ?? i18n.language}
          onChange={(value) => void i18n.changeLanguage(value)}
          options={[
            { value: 'es-AR', label: 'ES' },
            { value: 'en-US', label: 'EN' },
          ]}
        />
        <DarkModeToggle label={t('common.toggle_theme')} />
      </div>
      <form className="auth-card" onSubmit={submit}>
        <AuthCardHeader eyebrow={t('login.eyebrow')} title={t('login.title')} />
        {errorKey ? <p className="status-message error">{t(errorKey)}</p> : null}
        <label className="field">
          <span>{t('login.email')}</span>
          <Input type="email" value={email} onChange={(event) => setEmail(event.target.value)} />
        </label>
        <label className="field">
          <span>{t('login.password')}</span>
          <Input
            type="password"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
          />
        </label>
        <Button className="primary-button" type="submit" disabled={isSubmitting}>
          {t('login.submit')}
        </Button>
      </form>
    </AuthShell>
  )
}
```

- [ ] **Step 4: Create `apps/docs-web/src/features/portal/DocumentCard.tsx`**

```tsx
import { useTranslation } from 'react-i18next'
import type { ViewerCatalogDocument } from '../../api/viewer'
import { formatRelativeDate } from '../../lib/dates'
import { documentStateKey } from '../../lib/documentState'
import { typeIcon, typeTintIndex } from '../../lib/documentType'

export function DocumentCard({
  document,
  onOpen,
}: {
  document: ViewerCatalogDocument
  onOpen: () => void
}) {
  const { t, i18n } = useTranslation()
  const locale = i18n.resolvedLanguage ?? i18n.language
  const Icon = typeIcon(document.documentType)
  const stateKey = documentStateKey(document.state)
  const updated = formatRelativeDate(document.updatedAt, locale)

  return (
    <button
      type="button"
      className="document-card"
      onClick={onOpen}
      aria-label={t('portal.open_document', { title: document.title })}
    >
      <span className={`type-chip tint-${typeTintIndex(document.documentType)}`}>
        <Icon size={13} aria-hidden="true" />
        {document.documentType}
      </span>
      <h2>{document.title}</h2>
      <p className="card-audience">{document.audience}</p>
      <span className="card-footer">
        {updated ? (
          <span className="card-updated">{t('portal.updated_ago', { when: updated })}</span>
        ) : null}
        {document.state !== 'Published' ? (
          <span className="state-chip">{stateKey ? t(stateKey) : document.state}</span>
        ) : null}
      </span>
    </button>
  )
}
```

- [ ] **Step 5: Create `apps/docs-web/src/features/portal/DocumentPortal.tsx`**

```tsx
import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { FileText, Search } from 'lucide-react'
import { Button, EmptyState, Input } from '@helpcenter/shared-ui'
import {
  createViewerLink,
  type SessionUser,
  type ViewerCatalogDocument,
  type ViewerDocumentCatalog,
} from '../../api/viewer'
import { DocumentCard } from './DocumentCard'

const MANAGEMENT_ROLES: readonly string[] = ['Admin', 'DocumentEditor', 'DocumentPublisher']

export function DocumentPortal({
  user,
  catalog,
}: {
  user: SessionUser
  catalog: ViewerDocumentCatalog
}) {
  const { t } = useTranslation()
  const [selectedType, setSelectedType] = useState<string>('all')
  const [query, setQuery] = useState('')
  const isManagementUser = user.roles.some((role) => MANAGEMENT_ROLES.includes(role))

  const documentTypes = useMemo(
    () =>
      [...new Set(catalog.documents.map((document) => document.documentType))].sort((a, b) =>
        a.localeCompare(b),
      ),
    [catalog.documents],
  )

  const filteredDocuments = useMemo(() => {
    const normalizedQuery = query.trim().toLowerCase()
    return catalog.documents.filter((document) => {
      const matchesType = selectedType === 'all' || document.documentType === selectedType
      const matchesQuery =
        normalizedQuery.length === 0 ||
        [document.title, document.documentType, document.audience]
          .join(' ')
          .toLowerCase()
          .includes(normalizedQuery)
      return matchesType && matchesQuery
    })
  }, [catalog.documents, query, selectedType])

  async function openDocument(document: ViewerCatalogDocument) {
    const purpose = isManagementUser ? 'management' : 'chat'
    const url = await createViewerLink(document.id, purpose)
    window.location.assign(url)
  }

  const hasDocuments = catalog.documents.length > 0
  const hasMatches = filteredDocuments.length > 0

  return (
    <>
      <section className="portal-controls" aria-label={t('portal.filters_label')}>
        <label className="search-control">
          <Search size={16} aria-hidden="true" />
          <span className="sr-only">{t('portal.search_placeholder')}</span>
          <Input
            type="search"
            value={query}
            onChange={(event) => setQuery(event.target.value)}
            placeholder={t('portal.search_placeholder')}
            className="border-0 bg-transparent p-0 shadow-none focus-visible:ring-0 focus-visible:ring-offset-0"
          />
        </label>
        <div className="category-strip">
          <button
            type="button"
            className={selectedType === 'all' ? 'category-button selected' : 'category-button'}
            onClick={() => setSelectedType('all')}
          >
            {t('portal.filter_all')}
          </button>
          {documentTypes.map((type) => (
            <button
              key={type}
              type="button"
              className={selectedType === type ? 'category-button selected' : 'category-button'}
              onClick={() => setSelectedType(type)}
            >
              {type}
            </button>
          ))}
        </div>
      </section>

      {!hasMatches ? (
        <>
          <EmptyState
            className="state-panel"
            title={t('portal.empty_title')}
            description={hasDocuments ? t('portal.empty_filtered') : t('portal.empty_no_access')}
            icon={<FileText size={22} aria-hidden="true" />}
          />
          {hasDocuments ? (
            <div className="empty-actions">
              <Button
                type="button"
                className="ghost-button"
                onClick={() => {
                  setSelectedType('all')
                  setQuery('')
                }}
              >
                {t('portal.clear_filters')}
              </Button>
            </div>
          ) : null}
        </>
      ) : (
        <section className="document-grid" aria-label={t('portal.title')}>
          {filteredDocuments.map((document) => (
            <DocumentCard
              key={document.id}
              document={document}
              onOpen={() => void openDocument(document)}
            />
          ))}
        </section>
      )}
    </>
  )
}
```

- [ ] **Step 6: Create `apps/docs-web/src/features/portal/DocumentPortalApp.tsx`**

```tsx
import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { AlertTriangle, BookOpenText } from 'lucide-react'
import {
  AppShell,
  DarkModeToggle,
  EmptyState,
  LanguageSelect,
  Skeleton,
} from '@helpcenter/shared-ui'
import {
  consumeSessionHandoff,
  getSession,
  listViewerDocuments,
  viewerErrorKey,
  type SessionUser,
  type ViewerDocumentCatalog,
} from '../../api/viewer'
import { ApiError } from '../../lib/api-error'
import { removeHandoffFromUrl } from '../../lib/url'
import { DocsLoginPage } from '../auth/DocsLoginPage'
import { DocumentPortal } from './DocumentPortal'

type PortalState =
  | { status: 'loading' }
  | { status: 'login' }
  | { status: 'error'; messageKey: string }
  | { status: 'ready'; user: SessionUser; catalog: ViewerDocumentCatalog }

export function DocumentPortalApp({ handoffCode }: { handoffCode: string | null }) {
  const { t, i18n } = useTranslation()
  const [state, setState] = useState<PortalState>({ status: 'loading' })

  useEffect(() => {
    let isMounted = true

    async function loadPortal() {
      try {
        const session = handoffCode ? await consumeSessionHandoff(handoffCode) : await getSession()
        if (handoffCode) {
          removeHandoffFromUrl()
        }
        const catalog = await listViewerDocuments()
        if (isMounted) {
          setState({ status: 'ready', user: session.user, catalog })
        }
      } catch (error) {
        if (!isMounted) {
          return
        }

        if (error instanceof ApiError && error.code === 'AUTH_REQUIRED') {
          setState({ status: 'login' })
          return
        }

        setState({ status: 'error', messageKey: viewerErrorKey(error) })
      }
    }

    void loadPortal()
    return () => {
      isMounted = false
    }
  }, [handoffCode])

  async function loadAfterLogin(user: SessionUser) {
    const catalog = await listViewerDocuments()
    setState({ status: 'ready', user, catalog })
  }

  if (state.status === 'login') {
    return <DocsLoginPage onAuthenticated={(user) => void loadAfterLogin(user)} />
  }

  return (
    <AppShell className="viewer-main">
      <section className="portal-shell">
        <header className="portal-topbar">
          <span className="brand">
            <BookOpenText size={18} aria-hidden="true" />
            {t('app.name')}
          </span>
          <div className="toolbar">
            <LanguageSelect
              label={t('common.language')}
              value={i18n.resolvedLanguage ?? i18n.language}
              onChange={(value) => void i18n.changeLanguage(value)}
              options={[
                { value: 'es-AR', label: 'ES' },
                { value: 'en-US', label: 'EN' },
              ]}
            />
            <DarkModeToggle label={t('common.toggle_theme')} />
          </div>
        </header>
        <div className="portal-intro">
          <h1>{t('portal.title')}</h1>
          <p>{t('portal.subtitle')}</p>
        </div>

        {state.status === 'loading' ? (
          <section className="document-grid" aria-hidden="true">
            {Array.from({ length: 6 }).map((_, index) => (
              <Skeleton key={index} className="card-skeleton" />
            ))}
          </section>
        ) : null}

        {state.status === 'error' ? (
          <EmptyState
            className="state-panel error"
            title={t(state.messageKey)}
            role="alert"
            icon={<AlertTriangle size={22} aria-hidden="true" />}
          />
        ) : null}

        {state.status === 'ready' ? <DocumentPortal user={state.user} catalog={state.catalog} /> : null}
      </section>
    </AppShell>
  )
}
```

- [ ] **Step 7: Shrink `apps/docs-web/src/App.tsx`**

Delete `DocumentPortalApp`, `DocumentPortal`, and `DocsLoginPage` (and their now-unused imports) from `App.tsx`, keep `ViewerLinkApp`/`DocumentView`/`removeHandoffFromUrl`/`formatDateTime`/`displayState` untouched, and change the top of the file:

```tsx
import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { AlertTriangle, Clock3, FileText, ShieldCheck } from 'lucide-react'
import {
  AppShell,
  DarkModeToggle,
  EmptyState,
  LanguageSelect,
} from '@helpcenter/shared-ui'
import {
  consumeViewerHandoff,
  getViewerDocument,
  viewerErrorKey,
  type ViewerDocument,
} from './api/viewer'
import { DocumentPortalApp } from './features/portal/DocumentPortalApp'
import './i18n'
import './App.css'
```

The `App()` dispatch function itself stays as-is (it already renders `<DocumentPortalApp handoffCode={handoffCode} />`). Remove the local `removeHandoffFromUrl` only if you also switch `ViewerLinkApp` to import it from `../lib/url` — simplest is to replace the local definition with `import { removeHandoffFromUrl } from './lib/url'`.

- [ ] **Step 8: Replace `apps/docs-web/src/App.css`**

Full new content. The block marked `LEGACY` keeps the old viewer styled until Task 8 removes it.

```css
/* ====== Shell & shared ====== */
.viewer-shell,
.portal-shell {
  padding: 28px;
  color: #1f1b17;
  background: #fff8f5;
  min-height: 100%;
}

.brand {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  color: #00685f;
  font-size: 14px;
  font-weight: 700;
}

.toolbar {
  display: flex;
  align-items: end;
  gap: 10px;
}

.auth-toolbar {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
  margin: 0 auto 14px;
  max-width: 420px;
}

h1,
h2 {
  margin: 0;
  line-height: 1.2;
  letter-spacing: 0;
}

.sr-only {
  position: absolute;
  width: 1px;
  height: 1px;
  margin: -1px;
  padding: 0;
  overflow: hidden;
  clip: rect(0 0 0 0);
  white-space: nowrap;
  border: 0;
}

.state-panel {
  display: flex;
  align-items: center;
  gap: 12px;
  max-width: 1040px;
  margin: 0 auto;
  border: 1px solid #e2d8d2;
  border-radius: 8px;
  background: #ffffff;
  padding: 18px;
}

.state-panel p {
  margin: 0;
}

.state-panel.error {
  border-color: #fecaca;
  color: #991b1b;
  background: #fff7f7;
}

.empty-actions {
  display: flex;
  justify-content: center;
  max-width: 1040px;
  margin: 12px auto 0;
}

.ghost-button {
  min-height: 36px;
  border: 1px solid #bcc9c6;
  border-radius: 6px;
  background: transparent;
  color: #00685f;
  padding: 6px 12px;
  font: inherit;
}

.primary-button {
  justify-self: start;
  min-height: 38px;
  border: 1px solid #00685f;
  border-radius: 6px;
  background: #00685f;
  color: #ffffff;
  padding: 8px 12px;
  font: inherit;
}

/* ====== Type & state chips ====== */
.type-chip {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  width: fit-content;
  border-radius: 999px;
  padding: 3px 10px;
  font-size: 12px;
  font-weight: 700;
}

.tint-0 { background: #e7f5f2; color: #005049; }
.tint-1 { background: #fdeede; color: #8a4b08; }
.tint-2 { background: #ece9fb; color: #46398f; }
.tint-3 { background: #fdeaf0; color: #92325a; }
.tint-4 { background: #e9f2fd; color: #1f4e8a; }
.tint-5 { background: #eef4e3; color: #4a6312; }

.state-chip {
  display: inline-flex;
  align-items: center;
  border-radius: 999px;
  border: 1px solid #f0d8a8;
  background: #fdf3dd;
  color: #8a6d1d;
  padding: 2px 8px;
  font-size: 12px;
  font-weight: 700;
}

/* ====== Portal ====== */
.portal-topbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  max-width: 1120px;
  margin: 0 auto 26px;
}

.portal-intro {
  max-width: 1120px;
  margin: 0 auto 20px;
}

.portal-intro h1 {
  font-size: 32px;
}

.portal-intro p {
  margin: 8px 0 0;
  color: #3d4947;
}

.portal-controls {
  display: grid;
  grid-template-columns: minmax(240px, 560px) minmax(0, 1fr);
  align-items: center;
  gap: 12px;
  max-width: 1120px;
  margin: 0 auto 20px;
}

.search-control {
  display: grid;
  grid-template-columns: 18px minmax(0, 1fr);
  align-items: center;
  gap: 8px;
  min-height: 44px;
  border: 1px solid #d9cdc6;
  border-radius: 8px;
  background: #ffffff;
  padding: 0 12px;
}

.search-control input {
  min-width: 0;
  border: 0;
  outline: 0;
  font: inherit;
}

.category-strip {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.category-button {
  min-height: 36px;
  border: 1px solid #d9cdc6;
  border-radius: 999px;
  background: #ffffff;
  color: #1f1b17;
  padding: 6px 14px;
  font: inherit;
  font-size: 14px;
  transition: border-color 120ms ease, background-color 120ms ease;
}

.category-button:hover {
  border-color: #00685f;
}

.category-button.selected {
  border-color: #00685f;
  background: #e7f5f2;
  color: #005049;
  font-weight: 700;
}

.document-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(260px, 1fr));
  gap: 16px;
  max-width: 1120px;
  margin: 0 auto;
}

.document-card {
  display: grid;
  gap: 10px;
  align-content: start;
  text-align: left;
  border: 1px solid #e2d8d2;
  border-radius: 10px;
  background: #ffffff;
  padding: 18px;
  font: inherit;
  color: inherit;
  cursor: pointer;
  transition: transform 140ms ease, box-shadow 140ms ease, border-color 140ms ease;
}

.document-card:hover,
.document-card:focus-visible {
  border-color: #00685f;
  transform: translateY(-1px);
  box-shadow: 0 4px 14px rgba(31, 27, 23, 0.08);
}

.document-card h2 {
  font-size: 17px;
  font-weight: 600;
}

.card-audience {
  margin: 0;
  color: #3d4947;
  font-size: 14px;
}

.card-footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  margin-top: 4px;
}

.card-updated {
  color: #6b625c;
  font-size: 12.5px;
}

.card-skeleton {
  height: 148px;
  border-radius: 10px;
}

/* ====== Auth ====== */
.status-message.error {
  margin: 0;
  color: #991b1b;
}

/* ====== LEGACY viewer styles (removed in Task 8) ====== */
.viewer-header,
.portal-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 20px;
  margin: 0 auto 24px;
  max-width: 1040px;
}

.eyebrow {
  margin: 0 0 6px;
  color: #3d4947;
  font-size: 12px;
  font-weight: 700;
  text-transform: uppercase;
}

.viewer-toolbar {
  display: flex;
  align-items: end;
  gap: 10px;
}

.trust-strip {
  display: flex;
  flex-wrap: wrap;
  justify-content: flex-end;
  gap: 8px;
}

.trust-strip span {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  min-height: 32px;
  padding: 6px 10px;
  border: 1px solid #d4e6e2;
  border-radius: 6px;
  background: #ffffff;
  color: #00685f;
  font-size: 13px;
  font-weight: 700;
}

.document-view {
  max-width: 1040px;
  margin: 0 auto;
  border: 1px solid #e2d8d2;
  border-radius: 8px;
  background: #ffffff;
}

.document-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 20px;
  padding: 22px;
  border-bottom: 1px solid #e2d8d2;
}

.document-layout {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 280px;
  align-items: start;
}

.document-meta {
  display: grid;
  gap: 12px;
  margin: 0;
}

.document-meta dt {
  color: #3d4947;
  font-size: 12px;
  text-transform: uppercase;
}

.document-meta dd {
  margin: 3px 0 0;
  font-weight: 700;
}

.document-side-rail {
  display: grid;
  gap: 14px;
  margin: 24px 24px 24px 0;
  border: 1px solid #e2d8d2;
  border-radius: 6px;
  background: #fcf2eb;
  padding: 16px;
}

.document-side-rail h3 {
  margin: 0;
  font-size: 16px;
}
/* ====== END LEGACY ====== */

/* ====== Document content (rich text) ====== */
.document-content {
  padding: 24px;
  color: #1f1b17;
  font-size: 17px;
  line-height: 1.7;
}

.document-content h1,
.document-content h2,
.document-content h3 {
  margin: 24px 0 10px;
}

.document-content p,
.document-content ul,
.document-content ol,
.document-content blockquote,
.document-content pre,
.document-content table,
.document-content hr {
  margin: 0 0 14px;
}

.document-content ul,
.document-content ol {
  padding-left: 1.5rem;
}

.document-content ul { list-style: disc; }
.document-content ul ul { list-style: circle; }
.document-content ul ul ul { list-style: square; }
.document-content ol { list-style: decimal; }
.document-content ol ol { list-style: lower-alpha; }
.document-content ol ol ol { list-style: lower-roman; }

.document-content li {
  margin: 3px 0;
}

.document-content blockquote {
  border-left: 3px solid #00685f;
  color: #3d4947;
  padding-left: 12px;
}

.document-content code {
  border-radius: 4px;
  background: #f0e6e0;
  padding: 1px 4px;
  font-family: Consolas, 'Courier New', monospace;
  font-size: 0.92em;
}

.document-content pre {
  overflow-x: auto;
  border: 1px solid #e2d8d2;
  border-radius: 6px;
  background: #1f1b17;
  color: #ffffff;
  padding: 10px 12px;
}

.document-content pre code {
  background: transparent;
  color: inherit;
  padding: 0;
}

.document-content mark {
  border-radius: 2px;
  background: #fff4a3;
  padding: 0 0.08em;
}

.document-content table {
  width: 100%;
  border-collapse: collapse;
}

.document-content th,
.document-content td {
  border: 1px solid #e2d8d2;
  padding: 8px;
  text-align: left;
  vertical-align: top;
}

.document-content th {
  background: #fcf2eb;
}

.document-content hr {
  border: 0;
  border-top: 1px solid #bcc9c6;
}

.document-content img {
  display: block;
  max-width: 100%;
  height: auto;
  border-radius: 6px;
}

/* ====== Viewer (final, used from Task 8) ====== */
.viewer-topbar {
  position: sticky;
  top: 0;
  z-index: 5;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  max-width: 1040px;
  margin: -28px auto 22px;
  padding: 14px 0;
  background: #fff8f5;
}

.back-link {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  color: #00685f;
  font-weight: 600;
  text-decoration: none;
}

.back-link:hover {
  text-decoration: underline;
}

.state-banner {
  max-width: 1040px;
  margin: 0 auto 16px;
  border: 1px solid #f0d8a8;
  border-radius: 8px;
  background: #fdf3dd;
  color: #8a6d1d;
  padding: 10px 14px;
  font-size: 14px;
}

.document-titleblock {
  max-width: 72ch;
  margin: 0 auto;
  padding: 8px 24px 0;
}

.document-chips {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
  margin-bottom: 12px;
}

.meta-chip {
  display: inline-flex;
  align-items: center;
  border-radius: 999px;
  border: 1px solid #e2d8d2;
  background: #ffffff;
  color: #3d4947;
  padding: 3px 10px;
  font-size: 12px;
  font-weight: 600;
}

.document-titleblock h1 {
  font-size: 30px;
  padding-bottom: 14px;
  border-bottom: 1px solid #e2d8d2;
}

.document-view .document-content {
  max-width: 72ch;
  margin: 0 auto;
}

.document-footer {
  max-width: 72ch;
  margin: 0 auto;
  padding: 0 24px 24px;
  color: #6b625c;
  font-size: 13px;
}

/* ====== Doc chat widget (used from Task 10) ====== */
.doc-chat-fab {
  position: fixed;
  right: 24px;
  bottom: 24px;
  z-index: 30;
  display: grid;
  place-items: center;
  width: 52px;
  height: 52px;
  border: 0;
  border-radius: 999px;
  background: #00685f;
  color: #ffffff;
  cursor: pointer;
  box-shadow: 0 6px 18px rgba(31, 27, 23, 0.22);
  transition: transform 140ms ease;
}

.doc-chat-fab:hover {
  transform: translateY(-1px);
}

.doc-chat-panel {
  position: fixed;
  right: 24px;
  bottom: 88px;
  z-index: 30;
  display: flex;
  flex-direction: column;
  width: 380px;
  max-width: calc(100vw - 32px);
  max-height: min(70vh, 560px);
  border: 1px solid #e2d8d2;
  border-radius: 12px;
  background: #ffffff;
  box-shadow: 0 12px 32px rgba(31, 27, 23, 0.18);
  animation: doc-chat-in 150ms ease-out;
}

@keyframes doc-chat-in {
  from { opacity: 0; transform: translateY(8px); }
  to { opacity: 1; transform: translateY(0); }
}

.doc-chat-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 10px;
  border-bottom: 1px solid #e2d8d2;
  padding: 14px 16px;
}

.doc-chat-header h2 {
  font-size: 15px;
}

.doc-chat-header p {
  margin: 2px 0 0;
  color: #6b625c;
  font-size: 12.5px;
}

.icon-button {
  display: grid;
  place-items: center;
  width: 30px;
  height: 30px;
  border: 0;
  border-radius: 6px;
  background: transparent;
  color: #3d4947;
  cursor: pointer;
}

.icon-button:hover {
  background: #f4ece7;
}

.doc-chat-transcript {
  flex: 1;
  overflow-y: auto;
  display: grid;
  gap: 10px;
  padding: 14px 16px;
}

.doc-chat-empty {
  display: grid;
  gap: 8px;
  color: #3d4947;
  font-size: 14px;
}

.doc-chat-empty p {
  margin: 0;
}

.suggestion {
  text-align: left;
  border: 1px solid #d9cdc6;
  border-radius: 8px;
  background: #fff8f5;
  color: #00685f;
  padding: 8px 10px;
  font: inherit;
  font-size: 13.5px;
  cursor: pointer;
}

.suggestion:hover {
  border-color: #00685f;
}

.doc-chat-turn {
  display: grid;
  gap: 8px;
}

.bubble {
  border-radius: 10px;
  padding: 9px 12px;
  font-size: 14px;
  line-height: 1.55;
}

.bubble.user {
  justify-self: end;
  max-width: 85%;
  margin: 0;
  background: #e7f5f2;
  color: #1f1b17;
}

.bubble.assistant {
  justify-self: start;
  max-width: 95%;
  border: 1px solid #ece2dc;
  background: #fdfaf8;
}

.doc-chat-typing {
  display: inline-flex;
  gap: 4px;
  margin: 0;
  padding: 9px 12px;
}

.doc-chat-typing span {
  width: 6px;
  height: 6px;
  border-radius: 999px;
  background: #b0a59d;
  animation: doc-chat-pulse 900ms infinite ease-in-out;
}

.doc-chat-typing span:nth-child(2) { animation-delay: 150ms; }
.doc-chat-typing span:nth-child(3) { animation-delay: 300ms; }

@keyframes doc-chat-pulse {
  0%, 100% { opacity: 0.35; }
  50% { opacity: 1; }
}

.doc-chat-error {
  display: grid;
  gap: 8px;
  border: 1px solid #fecaca;
  border-radius: 8px;
  background: #fff7f7;
  color: #991b1b;
  padding: 10px 12px;
  font-size: 13.5px;
}

.doc-chat-error p {
  margin: 0;
}

.doc-chat-feedback {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 6px;
  margin-top: 8px;
}

.feedback-button {
  display: grid;
  place-items: center;
  width: 28px;
  height: 28px;
  border: 1px solid #d9cdc6;
  border-radius: 6px;
  background: #ffffff;
  color: #3d4947;
  cursor: pointer;
}

.feedback-button.selected {
  border-color: #00685f;
  background: #e7f5f2;
  color: #005049;
}

.feedback-note {
  color: #3d4947;
  font-size: 12.5px;
}

.feedback-note.error {
  color: #991b1b;
}

.feedback-comment {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto;
  gap: 6px;
  width: 100%;
}

.doc-chat-composer {
  border-top: 1px solid #e2d8d2;
  padding: 10px 12px;
}

/* ====== Responsive ====== */
@media (max-width: 720px) {
  .viewer-shell,
  .portal-shell {
    padding: 18px;
  }

  .portal-topbar,
  .viewer-topbar {
    flex-direction: column;
    align-items: stretch;
  }

  .viewer-topbar {
    margin-top: -18px;
  }

  .portal-controls {
    grid-template-columns: 1fr;
  }

  .doc-chat-panel {
    right: 0;
    left: 0;
    bottom: 0;
    width: 100%;
    max-width: none;
    height: 75dvh;
    max-height: none;
    border-radius: 12px 12px 0 0;
  }

  .doc-chat-fab {
    right: 16px;
    bottom: 16px;
  }
}

/* ====== Dark mode ====== */
[data-theme='dark'] .viewer-shell,
[data-theme='dark'] .portal-shell,
[data-theme='dark'] .viewer-topbar {
  background: var(--bg);
  color: var(--fg);
}

[data-theme='dark'] .portal-intro p,
[data-theme='dark'] .card-audience,
[data-theme='dark'] .card-updated,
[data-theme='dark'] .document-footer,
[data-theme='dark'] .doc-chat-header p,
[data-theme='dark'] .doc-chat-empty,
[data-theme='dark'] .feedback-note,
[data-theme='dark'] .meta-chip,
[data-theme='dark'] .eyebrow,
[data-theme='dark'] .document-content blockquote,
[data-theme='dark'] .state-panel {
  color: var(--fg-muted);
}

[data-theme='dark'] h1,
[data-theme='dark'] h2,
[data-theme='dark'] h3,
[data-theme='dark'] .document-content,
[data-theme='dark'] .category-button,
[data-theme='dark'] .bubble.user,
[data-theme='dark'] .bubble.assistant {
  color: var(--fg);
}

[data-theme='dark'] .state-panel,
[data-theme='dark'] .document-view,
[data-theme='dark'] .document-card,
[data-theme='dark'] .search-control,
[data-theme='dark'] .category-button,
[data-theme='dark'] .meta-chip,
[data-theme='dark'] .doc-chat-panel,
[data-theme='dark'] .suggestion,
[data-theme='dark'] .feedback-button,
[data-theme='dark'] .bubble.assistant,
[data-theme='dark'] .document-side-rail,
[data-theme='dark'] .trust-strip span,
[data-theme='dark'] .document-content th {
  border-color: var(--border);
  background: var(--bg-elevated);
}

[data-theme='dark'] .document-header,
[data-theme='dark'] .doc-chat-header,
[data-theme='dark'] .doc-chat-composer,
[data-theme='dark'] .document-titleblock h1,
[data-theme='dark'] .document-content th,
[data-theme='dark'] .document-content td {
  border-color: var(--border);
}

[data-theme='dark'] .category-button.selected,
[data-theme='dark'] .bubble.user,
[data-theme='dark'] .feedback-button.selected,
[data-theme='dark'] .document-side-rail {
  background: var(--bg-subtle);
}

[data-theme='dark'] .document-content code {
  background: var(--bg-subtle);
}

[data-theme='dark'] .document-content mark {
  background: #5b4a12;
  color: inherit;
}

[data-theme='dark'] .brand,
[data-theme='dark'] .back-link,
[data-theme='dark'] .ghost-button,
[data-theme='dark'] .suggestion,
[data-theme='dark'] .trust-strip span,
[data-theme='dark'] .category-button.selected {
  color: var(--accent);
}

[data-theme='dark'] .state-banner,
[data-theme='dark'] .state-chip {
  border-color: #5b4a12;
  background: rgba(91, 74, 18, 0.35);
  color: #e8c96b;
}

[data-theme='dark'] .tint-0 { background: rgba(0, 104, 95, 0.25); color: #7fd4c9; }
[data-theme='dark'] .tint-1 { background: rgba(138, 75, 8, 0.25); color: #f0b277; }
[data-theme='dark'] .tint-2 { background: rgba(70, 57, 143, 0.3); color: #b6a9f5; }
[data-theme='dark'] .tint-3 { background: rgba(146, 50, 90, 0.28); color: #f0a3c4; }
[data-theme='dark'] .tint-4 { background: rgba(31, 78, 138, 0.3); color: #9ec5f5; }
[data-theme='dark'] .tint-5 { background: rgba(74, 99, 18, 0.3); color: #c4d98a; }
```

- [ ] **Step 9: Delete the transitional `portal.header_copy` key from both i18n files**

Remove the `"header_copy": ...` line from the `portal` group in `es-AR.json` and `en-US.json`.

- [ ] **Step 10: Run tests, typecheck, build**

Run: `pnpm.cmd --dir apps\docs-web test -- --run src/App.test.tsx; pnpm.cmd --dir apps\docs-web typecheck; pnpm.cmd --dir apps\docs-web build`
Expected: all PASS / clean. The language-switch test still passes (`portal.title` exists in both locales).

- [ ] **Step 11: Commit**

```powershell
git add apps/docs-web/src
git commit -m "feat(docs-web): warm help-center portal with type filters and final login"
```

---

## Task 8: Viewer in final form

**Files:**
- Create: `apps/docs-web/src/features/viewer/ViewerLinkApp.tsx`
- Create: `apps/docs-web/src/features/viewer/DocumentView.tsx`
- Modify: `apps/docs-web/src/App.tsx` (becomes pure dispatch)
- Modify: `apps/docs-web/src/App.css` (delete the LEGACY block)
- Modify: `apps/docs-web/src/i18n/es-AR.json`, `en-US.json` (delete `viewer.instruction_viewer_title`)
- Test: `apps/docs-web/src/App.test.tsx`

- [ ] **Step 1: Update the viewer tests (failing first)**

In `apps/docs-web/src/App.test.tsx`, replace `test('renders document through the unified session and document id', ...)` with:

```tsx
test('renders the document with back navigation and session footer', async () => {
  mockFetch([
    jsonResponse({
      documentId: '55555555-5555-5555-5555-555555555555',
      documentVersionId: 'version-1',
      title: 'Procedimiento publicado',
      state: 'Published',
      documentType: 'Politica',
      audience: 'Operaciones',
      contentHtml: '<h2>Contenido publicado</h2><p>Usa el equipo de seguridad.</p>',
      tokenExpiresAt: '2026-05-18T12:15:00Z',
    }),
  ])

  render(<App />)

  expect(await screen.findByRole('heading', { name: 'Procedimiento publicado' })).toBeInTheDocument()
  expect(screen.getByText('Usa el equipo de seguridad.')).toBeInTheDocument()
  const backLink = screen.getByRole('link', { name: 'Volver a la biblioteca' })
  expect(backLink).toHaveAttribute('href', '/')
  expect(screen.getByText(/Sesión válida hasta/)).toBeInTheDocument()
  // Published documents show no state chip or banner.
  expect(screen.queryByText('Publicado')).not.toBeInTheDocument()
})
```

and add:

```tsx
test('shows a draft banner when viewing an unpublished version', async () => {
  mockFetch([
    jsonResponse({
      documentId: '55555555-5555-5555-5555-555555555555',
      documentVersionId: 'version-1',
      title: 'Borrador interno',
      state: 'Draft',
      documentType: 'Manual',
      audience: 'Operaciones',
      contentHtml: '<p>Contenido en preparación.</p>',
      tokenExpiresAt: '2026-05-18T12:15:00Z',
    }),
  ])

  render(<App />)

  expect(await screen.findByRole('heading', { name: 'Borrador interno' })).toBeInTheDocument()
  expect(
    screen.getByText('Estás viendo una versión en borrador. No es visible para usuarios finales.'),
  ).toBeInTheDocument()
})
```

- [ ] **Step 2: Run the tests to verify the new expectations fail**

Run: `pnpm.cmd --dir apps\docs-web test -- --run src/App.test.tsx`
Expected: the two viewer tests FAIL.

- [ ] **Step 3: Create `apps/docs-web/src/features/viewer/DocumentView.tsx`**

```tsx
import { useTranslation } from 'react-i18next'
import type { ViewerDocument } from '../../api/viewer'
import { formatDateTime } from '../../lib/dates'
import { documentStateKey } from '../../lib/documentState'
import { typeIcon, typeTintIndex } from '../../lib/documentType'

export function DocumentView({ document }: { document: ViewerDocument }) {
  const { t, i18n } = useTranslation()
  const locale = i18n.resolvedLanguage ?? i18n.language
  const Icon = typeIcon(document.documentType)
  const stateKey = documentStateKey(document.state)

  return (
    <article className="document-view">
      {document.state !== 'Published' ? (
        <p className="state-banner" role="status">
          {document.state === 'Draft' ? t('viewer.draft_banner') : t('viewer.review_banner')}
        </p>
      ) : null}
      <header className="document-titleblock">
        <div className="document-chips">
          <span className={`type-chip tint-${typeTintIndex(document.documentType)}`}>
            <Icon size={13} aria-hidden="true" />
            {document.documentType}
          </span>
          <span className="meta-chip">{document.audience}</span>
          {document.state !== 'Published' && stateKey ? (
            <span className="state-chip">{t(stateKey)}</span>
          ) : null}
        </div>
        <h1>{document.title}</h1>
      </header>
      <section
        className="document-content"
        dangerouslySetInnerHTML={{ __html: document.contentHtml }}
      />
      <footer className="document-footer">
        {t('viewer.session_valid_until', { time: formatDateTime(document.tokenExpiresAt, locale) })}
      </footer>
    </article>
  )
}
```

Note: the `state-banner` paragraph sits inside `.document-view`; if the banner should span the full card width it stays as the first child — no extra container is needed.

- [ ] **Step 4: Create `apps/docs-web/src/features/viewer/ViewerLinkApp.tsx`**

```tsx
import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { AlertTriangle, ArrowLeft, BookOpenText, FileText } from 'lucide-react'
import { AppShell, DarkModeToggle, EmptyState, LanguageSelect } from '@helpcenter/shared-ui'
import {
  consumeViewerHandoff,
  getViewerDocument,
  viewerErrorKey,
  type ViewerDocument,
} from '../../api/viewer'
import { removeHandoffFromUrl } from '../../lib/url'
import { DocumentView } from './DocumentView'

type ViewerState =
  | { status: 'loading' }
  | { status: 'error'; messageKey: string }
  | { status: 'ready'; document: ViewerDocument }

export function ViewerLinkApp({
  documentId,
  handoffCode,
}: {
  documentId: string
  handoffCode: string | null
}) {
  const { t, i18n } = useTranslation()
  const [state, setState] = useState<ViewerState>({ status: 'loading' })

  useEffect(() => {
    let isMounted = true

    async function loadViewer() {
      try {
        if (handoffCode) {
          await consumeViewerHandoff(handoffCode, documentId)
          removeHandoffFromUrl()
        }

        const document = await getViewerDocument(documentId)
        if (isMounted) {
          setState({ status: 'ready', document })
        }
      } catch (error) {
        if (isMounted) {
          setState({ status: 'error', messageKey: viewerErrorKey(error) })
        }
      }
    }

    void loadViewer()
    return () => {
      isMounted = false
    }
  }, [documentId, handoffCode])

  return (
    <AppShell className="viewer-main">
      <div className="viewer-shell">
        <nav className="viewer-topbar" aria-label={t('viewer.nav_label')}>
          <a className="back-link" href="/">
            <ArrowLeft size={16} aria-hidden="true" />
            {t('viewer.back_to_library')}
          </a>
          <span className="brand">
            <BookOpenText size={16} aria-hidden="true" />
            {t('app.name')}
          </span>
          <div className="toolbar">
            <LanguageSelect
              label={t('common.language')}
              value={i18n.resolvedLanguage ?? i18n.language}
              onChange={(value) => void i18n.changeLanguage(value)}
              options={[
                { value: 'es-AR', label: 'ES' },
                { value: 'en-US', label: 'EN' },
              ]}
            />
            <DarkModeToggle label={t('common.toggle_theme')} />
          </div>
        </nav>

        {state.status === 'loading' ? (
          <EmptyState
            className="state-panel"
            title={t('viewer.loading')}
            aria-live="polite"
            icon={<FileText size={22} aria-hidden="true" />}
          />
        ) : null}

        {state.status === 'error' ? (
          <EmptyState
            className="state-panel error"
            title={t(state.messageKey)}
            role="alert"
            icon={<AlertTriangle size={22} aria-hidden="true" />}
          />
        ) : null}

        {state.status === 'ready' ? <DocumentView document={state.document} /> : null}
      </div>
    </AppShell>
  )
}
```

- [ ] **Step 5: Reduce `apps/docs-web/src/App.tsx` to the dispatch**

Full new content:

```tsx
import { DocumentPortalApp } from './features/portal/DocumentPortalApp'
import { ViewerLinkApp } from './features/viewer/ViewerLinkApp'
import './i18n'
import './App.css'

export default function App() {
  const params = new URLSearchParams(window.location.search)
  const documentId = params.get('documentId')
  const handoffCode = params.get('handoff')
  return documentId ? (
    <ViewerLinkApp documentId={documentId} handoffCode={handoffCode} />
  ) : (
    <DocumentPortalApp handoffCode={handoffCode} />
  )
}
```

- [ ] **Step 6: Delete the LEGACY block from `App.css` and the transitional key**

Remove everything between `/* ====== LEGACY viewer styles (removed in Task 8) ====== */` and `/* ====== END LEGACY ====== */` in `apps/docs-web/src/App.css`. Remove `"instruction_viewer_title": ...` from the `viewer` group in both i18n files.

- [ ] **Step 7: Run tests, typecheck, build**

Run: `pnpm.cmd --dir apps\docs-web test -- --run src/App.test.tsx; pnpm.cmd --dir apps\docs-web typecheck; pnpm.cmd --dir apps\docs-web build`
Expected: all PASS / clean (including the untouched handoff and list-content tests).

- [ ] **Step 8: Commit**

```powershell
git add apps/docs-web/src
git commit -m "feat(docs-web): reading-first viewer with back navigation and state banner"
```

---

## Task 9: docChat API client (SSE)

**Files:**
- Create: `apps/docs-web/src/api/docChat.ts`

(The client is exercised end-to-end by the widget tests in Task 10; it mirrors the proven parser in `apps/chat-web/src/api/chat.ts`.)

- [ ] **Step 1: Create `apps/docs-web/src/api/docChat.ts`**

```ts
import { parseApiError } from '../lib/api-error'
import { createRequestId, ensureCsrfToken } from '../lib/csrf'

export type FeedbackValue = 'up' | 'down'

export interface DocChatResult {
  answer: string
  queryAuditEventId: string | null
  requestId: string | null
}

export interface DocChatStreamHandlers {
  onAnswerToken?: (delta: string) => void
}

export interface AskDocumentInput {
  question: string
  documentId: string
  sessionId: string
  locale?: string
}

export async function askDocument(
  input: AskDocumentInput,
  handlers: DocChatStreamHandlers = {},
): Promise<DocChatResult> {
  const csrfToken = await ensureCsrfToken()
  const response = await fetch('/api/chat', {
    method: 'POST',
    credentials: 'include',
    headers: {
      'Content-Type': 'application/json',
      'X-CSRF-Token': csrfToken,
      'X-Request-ID': createRequestId(),
    },
    body: JSON.stringify({
      question: input.question,
      documentId: input.documentId,
      sessionId: input.sessionId,
      locale: input.locale,
    }),
  })
  if (!response.ok) {
    const text = await response.text()
    throw parseApiError(response, safeJson(text))
  }

  if (!response.body) {
    return parseSseText(await response.text(), handlers)
  }

  return parseSseStream(response.body, handlers)
}

export async function submitDocFeedback(
  queryAuditEventId: string,
  value: FeedbackValue,
  comment: string,
): Promise<void> {
  const csrfToken = await ensureCsrfToken()
  const response = await fetch(`/api/feedback/${queryAuditEventId}`, {
    method: 'POST',
    credentials: 'include',
    headers: {
      'Content-Type': 'application/json',
      'X-CSRF-Token': csrfToken,
      'X-Request-ID': createRequestId(),
    },
    body: JSON.stringify({ value, comment }),
  })
  if (!response.ok) {
    const text = await response.text()
    throw parseApiError(response, safeJson(text))
  }
}

interface SseState extends DocChatResult {
  done: boolean
  handlers: DocChatStreamHandlers
}

async function parseSseStream(
  stream: ReadableStream<Uint8Array>,
  handlers: DocChatStreamHandlers,
): Promise<DocChatResult> {
  const reader = stream.getReader()
  const decoder = new TextDecoder()
  const state = createSseState(handlers)
  let buffer = ''

  while (true) {
    const { done, value } = await reader.read()
    if (done) {
      break
    }

    buffer += decoder.decode(value, { stream: true })
    buffer = processSseBuffer(buffer, state)
  }

  buffer += decoder.decode()
  buffer = processSseBuffer(buffer, state)
  if (buffer.trim().length > 0) {
    processSseEvent(buffer, state)
  }
  if (!state.done) {
    throw new Error('Doc chat stream ended before the done event.')
  }

  return toResult(state)
}

function parseSseText(text: string, handlers: DocChatStreamHandlers): DocChatResult {
  const state = createSseState(handlers)
  for (const rawEvent of text.split('\n\n').filter(Boolean)) {
    processSseEvent(rawEvent, state)
  }
  return toResult(state)
}

function createSseState(handlers: DocChatStreamHandlers): SseState {
  return {
    answer: '',
    queryAuditEventId: null,
    requestId: null,
    done: false,
    handlers,
  }
}

function processSseBuffer(buffer: string, state: SseState): string {
  const normalized = buffer.replace(/\r\n/g, '\n')
  const events = normalized.split('\n\n')
  const remainder = events.pop() ?? ''
  for (const rawEvent of events) {
    processSseEvent(rawEvent, state)
  }

  return remainder
}

function processSseEvent(rawEvent: string, state: SseState): void {
  const eventName = rawEvent.match(/^event: (.+)$/m)?.[1]
  const dataLines = rawEvent
    .split('\n')
    .filter((line) => line.startsWith('data:'))
    .map((line) => line.slice('data:'.length).trimStart())

  if (!eventName || dataLines.length === 0) {
    return
  }

  const payload = safeJson(dataLines.join('\n')) as Record<string, unknown> | null
  if (!payload) {
    return
  }

  if (eventName === 'request-id' && typeof payload.request_id === 'string') {
    state.requestId = payload.request_id
  }
  if (eventName === 'answer-token' && typeof payload.delta === 'string') {
    state.answer += payload.delta
    state.handlers.onAnswerToken?.(payload.delta)
  }
  if (eventName === 'citations' && typeof payload.query_audit_event_id === 'string') {
    state.queryAuditEventId = payload.query_audit_event_id
  }
  if (eventName === 'done') {
    state.done = true
  }
}

function toResult(state: SseState): DocChatResult {
  return {
    answer: state.answer,
    queryAuditEventId: state.queryAuditEventId,
    requestId: state.requestId,
  }
}

function safeJson(text: string): unknown {
  if (!text) {
    return null
  }
  try {
    return JSON.parse(text)
  } catch {
    return null
  }
}
```

- [ ] **Step 2: Typecheck**

Run: `pnpm.cmd --dir apps\docs-web typecheck`
Expected: clean.

- [ ] **Step 3: Commit**

```powershell
git add apps/docs-web/src/api/docChat.ts
git commit -m "feat(docs-web): document-scoped chat SSE client"
```

---

## Task 10: DocChatWidget + useDocChat + tests

**Files:**
- Create: `apps/docs-web/src/features/docChat/useDocChat.ts`
- Create: `apps/docs-web/src/features/docChat/DocChatWidget.tsx`
- Modify: `apps/docs-web/src/features/viewer/ViewerLinkApp.tsx` (mount the widget)
- Test: `apps/docs-web/src/App.test.tsx`

- [ ] **Step 1: Write the failing tests**

Add to `apps/docs-web/src/App.test.tsx`. First extend the helpers at the bottom of the file:

```tsx
function sseResponse(events: string) {
  return new Response(events, {
    status: 200,
    headers: { 'Content-Type': 'text/event-stream' },
  })
}

function docChatSse(answer: string, auditId = '11111111-1111-1111-1111-111111111111') {
  return sseResponse(
    `event: request-id\ndata: {"request_id":"req-1"}\n\n` +
      `event: answer-token\ndata: ${JSON.stringify({ delta: answer })}\n\n` +
      `event: citations\ndata: ${JSON.stringify({ query_audit_event_id: auditId, citations: [] })}\n\n` +
      `event: usage\ndata: {"input_tokens":1,"cached_tokens":0,"output_tokens":1,"cost_usd":0.0001}\n\n` +
      `event: done\ndata: {}\n\n`,
  )
}

const PUBLISHED_DOCUMENT = {
  documentId: '55555555-5555-5555-5555-555555555555',
  documentVersionId: 'version-1',
  title: 'Procedimiento publicado',
  state: 'Published',
  documentType: 'Politica',
  audience: 'Operaciones',
  contentHtml: '<p>Usa el equipo de seguridad.</p>',
  tokenExpiresAt: '2026-05-18T12:15:00Z',
}
```

Also change `errorResponse` to accept an explicit status:

```tsx
function errorResponse(code: string, status?: number) {
  return new Response(
    JSON.stringify({
      error: {
        code,
        message: 'Safe error',
        details: null,
        requestId: 'request-1',
      },
    }),
    {
      status: status ?? (code === 'AUTH_FORBIDDEN' ? 403 : code === 'NOT_FOUND' ? 404 : 410),
      headers: { 'Content-Type': 'application/json' },
    },
  )
}
```

Then add the tests:

```tsx
test('shows the doc chat bubble only for published documents', async () => {
  mockFetch([jsonResponse({ ...PUBLISHED_DOCUMENT, state: 'Draft' })])

  render(<App />)

  expect(await screen.findByRole('heading', { name: 'Procedimiento publicado' })).toBeInTheDocument()
  expect(screen.queryByRole('button', { name: 'Preguntale a este documento' })).not.toBeInTheDocument()
})

test('asks the document mini chat and renders the streamed answer with feedback', async () => {
  const fetch = mockFetch([
    jsonResponse(PUBLISHED_DOCUMENT),
    jsonResponse({ status: 'ok' }, { 'X-CSRF-Token': 'csrf-token' }),
    docChatSse('La respuesta sale de este documento.'),
    jsonResponse({ status: 'ok' }, { 'X-CSRF-Token': 'csrf-token' }),
    jsonResponse({
      queryAuditEventId: '11111111-1111-1111-1111-111111111111',
      value: 'up',
      comment: 'Muy claro',
    }),
  ])
  const user = userEvent.setup()

  render(<App />)

  await user.click(await screen.findByRole('button', { name: 'Preguntale a este documento' }))
  await user.click(screen.getByRole('button', { name: '¿De qué trata este documento?' }))

  expect(await screen.findByText('La respuesta sale de este documento.')).toBeInTheDocument()
  const chatCall = fetch.mock.calls.find(([url]) => url === '/api/chat')
  expect(chatCall).toBeDefined()
  expect(JSON.parse((chatCall![1] as RequestInit).body as string)).toMatchObject({
    question: '¿De qué trata este documento?',
    documentId: '55555555-5555-5555-5555-555555555555',
  })

  await user.click(screen.getByRole('button', { name: 'Respuesta útil' }))
  await user.type(
    screen.getByPlaceholderText('Contanos qué mejorarías (opcional)'),
    'Muy claro',
  )
  await user.click(screen.getByRole('button', { name: 'Enviar feedback' }))

  expect(await screen.findByText('Gracias por tu feedback.')).toBeInTheDocument()
  const feedbackCall = fetch.mock.calls.find(([url]) =>
    String(url).startsWith('/api/feedback/11111111-1111-1111-1111-111111111111'),
  )
  expect(feedbackCall).toBeDefined()
  expect(JSON.parse((feedbackCall![1] as RequestInit).body as string)).toEqual({
    value: 'up',
    comment: 'Muy claro',
  })
})

test('shows the budget-limited message and retry when the doc chat is over budget', async () => {
  mockFetch([
    jsonResponse(PUBLISHED_DOCUMENT),
    jsonResponse({ status: 'ok' }, { 'X-CSRF-Token': 'csrf-token' }),
    errorResponse('AI_BUDGET_EXCEEDED', 429),
  ])
  const user = userEvent.setup()

  render(<App />)

  await user.click(await screen.findByRole('button', { name: 'Preguntale a este documento' }))
  await user.click(screen.getByRole('button', { name: 'Resumime los puntos principales.' }))

  expect(
    await screen.findByText(
      'Alcanzaste tu límite mensual de uso de IA. Podés seguir leyendo el documento sin problema.',
    ),
  ).toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Reintentar' })).toBeInTheDocument()
  // The failed question was rolled back from the transcript.
  expect(screen.queryByText('Resumime los puntos principales.', { selector: '.bubble' })).not.toBeInTheDocument()
})
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `pnpm.cmd --dir apps\docs-web test -- --run src/App.test.tsx`
Expected: the three new tests FAIL (the widget does not exist).

- [ ] **Step 3: Create `apps/docs-web/src/features/docChat/useDocChat.ts`**

```ts
import { useCallback, useRef, useState } from 'react'
import { askDocument, submitDocFeedback, type FeedbackValue } from '../../api/docChat'
import { ApiError } from '../../lib/api-error'

export interface DocChatFeedback {
  value: FeedbackValue | null
  comment: string
  status: 'idle' | 'sending' | 'sent' | 'error'
}

export interface DocChatTurn {
  key: string
  question: string
  answer: string
  status: 'pending' | 'streaming' | 'done'
  queryAuditEventId: string | null
  feedback: DocChatFeedback
}

export interface DocChatFailure {
  messageKey: string
  question: string
}

const INITIAL_FEEDBACK: DocChatFeedback = { value: null, comment: '', status: 'idle' }

export function useDocChat(documentId: string, locale: string) {
  const [turns, setTurns] = useState<DocChatTurn[]>([])
  const [isStreaming, setIsStreaming] = useState(false)
  const [failure, setFailure] = useState<DocChatFailure | null>(null)
  const sessionIdRef = useRef(createId())

  const ask = useCallback(
    async (question: string) => {
      const key = createId()
      setFailure(null)
      setIsStreaming(true)
      setTurns((current) => [
        ...current,
        {
          key,
          question,
          answer: '',
          status: 'pending',
          queryAuditEventId: null,
          feedback: INITIAL_FEEDBACK,
        },
      ])
      try {
        const result = await askDocument(
          { question, documentId, sessionId: sessionIdRef.current, locale },
          {
            onAnswerToken: (delta) => {
              setTurns((current) =>
                current.map((turn) =>
                  turn.key === key
                    ? { ...turn, status: 'streaming', answer: turn.answer + delta }
                    : turn,
                ),
              )
            },
          },
        )
        setTurns((current) =>
          current.map((turn) =>
            turn.key === key
              ? {
                  ...turn,
                  status: 'done',
                  answer: result.answer,
                  queryAuditEventId: result.queryAuditEventId,
                }
              : turn,
          ),
        )
      } catch (error) {
        // Roll back the partial turn; the failed question stays available for retry.
        setTurns((current) => current.filter((turn) => turn.key !== key))
        setFailure({ messageKey: chatErrorKey(error), question })
      } finally {
        setIsStreaming(false)
      }
    },
    [documentId, locale],
  )

  const retry = useCallback(() => {
    if (failure) {
      void ask(failure.question)
    }
  }, [ask, failure])

  const setFeedbackValue = useCallback((key: string, value: FeedbackValue) => {
    setTurns((current) =>
      current.map((turn) =>
        turn.key === key
          ? {
              ...turn,
              feedback: {
                ...turn.feedback,
                value: turn.feedback.value === value ? null : value,
              },
            }
          : turn,
      ),
    )
  }, [])

  const setFeedbackComment = useCallback((key: string, comment: string) => {
    setTurns((current) =>
      current.map((turn) =>
        turn.key === key ? { ...turn, feedback: { ...turn.feedback, comment } } : turn,
      ),
    )
  }, [])

  const sendFeedback = useCallback(
    async (key: string) => {
      const turn = turns.find((item) => item.key === key)
      if (!turn || !turn.queryAuditEventId || !turn.feedback.value) {
        return
      }
      setTurns((current) =>
        current.map((item) =>
          item.key === key ? { ...item, feedback: { ...item.feedback, status: 'sending' } } : item,
        ),
      )
      try {
        await submitDocFeedback(turn.queryAuditEventId, turn.feedback.value, turn.feedback.comment)
        setTurns((current) =>
          current.map((item) =>
            item.key === key ? { ...item, feedback: { ...item.feedback, status: 'sent' } } : item,
          ),
        )
      } catch {
        setTurns((current) =>
          current.map((item) =>
            item.key === key ? { ...item, feedback: { ...item.feedback, status: 'error' } } : item,
          ),
        )
      }
    },
    [turns],
  )

  return { turns, isStreaming, failure, ask, retry, setFeedbackValue, setFeedbackComment, sendFeedback }
}

function chatErrorKey(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.code === 'AI_BUDGET_EXCEEDED') {
      return 'docChat.error_budget'
    }
    if (error.code === 'CHAT_RATE_LIMITED') {
      return 'docChat.error_rate_limited'
    }
    if (error.code === 'AUTH_REQUIRED') {
      return 'errors.auth_required'
    }
  }
  return 'docChat.error_generic'
}

function createId(): string {
  if ('randomUUID' in crypto) {
    return crypto.randomUUID()
  }
  return `00000000-0000-4000-8000-${Date.now().toString().padStart(12, '0').slice(-12)}`
}
```

- [ ] **Step 4: Create `apps/docs-web/src/features/docChat/DocChatWidget.tsx`**

```tsx
import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { MessageCircleQuestion, RotateCcw, ThumbsDown, ThumbsUp, X } from 'lucide-react'
import { Button, ChatComposer, Input, Markdown } from '@helpcenter/shared-ui'
import { useDocChat, type DocChatTurn } from './useDocChat'

export function DocChatWidget({ documentId }: { documentId: string }) {
  const { t, i18n } = useTranslation()
  const locale = i18n.resolvedLanguage ?? i18n.language
  const [isOpen, setIsOpen] = useState(false)
  const chat = useDocChat(documentId, locale)
  const endRef = useRef<HTMLDivElement | null>(null)

  useEffect(() => {
    endRef.current?.scrollIntoView?.({ block: 'end' })
  }, [chat.turns, isOpen])

  return (
    <div className="doc-chat">
      {isOpen ? (
        <section className="doc-chat-panel" role="dialog" aria-label={t('docChat.title')}>
          <header className="doc-chat-header">
            <div>
              <h2>{t('docChat.title')}</h2>
              <p>{t('docChat.subtitle')}</p>
            </div>
            <button
              type="button"
              className="icon-button"
              onClick={() => setIsOpen(false)}
              aria-label={t('docChat.close')}
            >
              <X size={16} aria-hidden="true" />
            </button>
          </header>
          <div className="doc-chat-transcript" aria-live="polite">
            {chat.turns.length === 0 && !chat.failure ? (
              <div className="doc-chat-empty">
                <p>{t('docChat.empty_hint')}</p>
                <button
                  type="button"
                  className="suggestion"
                  onClick={() => void chat.ask(t('docChat.suggestion_1'))}
                >
                  {t('docChat.suggestion_1')}
                </button>
                <button
                  type="button"
                  className="suggestion"
                  onClick={() => void chat.ask(t('docChat.suggestion_2'))}
                >
                  {t('docChat.suggestion_2')}
                </button>
              </div>
            ) : null}
            {chat.turns.map((turn) => (
              <DocChatTurnView
                key={turn.key}
                turn={turn}
                onFeedbackValue={chat.setFeedbackValue}
                onFeedbackComment={chat.setFeedbackComment}
                onSendFeedback={chat.sendFeedback}
              />
            ))}
            {chat.isStreaming && chat.turns.at(-1)?.status === 'pending' ? (
              <p className="doc-chat-typing" aria-hidden="true">
                <span />
                <span />
                <span />
              </p>
            ) : null}
            {chat.failure ? (
              <div className="doc-chat-error" role="alert">
                <p>{t(chat.failure.messageKey)}</p>
                <Button type="button" className="ghost-button" onClick={chat.retry}>
                  <RotateCcw size={14} aria-hidden="true" />
                  {t('docChat.retry')}
                </Button>
              </div>
            ) : null}
            <div ref={endRef} />
          </div>
          <footer className="doc-chat-composer">
            <ChatComposer
              onSubmit={(question) => void chat.ask(question)}
              disabled={chat.isStreaming}
              placeholder={t('docChat.placeholder')}
              submitLabel={t('docChat.send')}
              pendingLabel={chat.isStreaming ? t('docChat.sending') : undefined}
            />
          </footer>
        </section>
      ) : null}
      <button
        type="button"
        className="doc-chat-fab"
        onClick={() => setIsOpen((open) => !open)}
        aria-expanded={isOpen}
        aria-label={isOpen ? t('docChat.close') : t('docChat.open')}
        title={isOpen ? t('docChat.close') : t('docChat.open')}
      >
        {isOpen ? <X size={22} aria-hidden="true" /> : <MessageCircleQuestion size={22} aria-hidden="true" />}
      </button>
    </div>
  )
}

function DocChatTurnView({
  turn,
  onFeedbackValue,
  onFeedbackComment,
  onSendFeedback,
}: {
  turn: DocChatTurn
  onFeedbackValue: (key: string, value: 'up' | 'down') => void
  onFeedbackComment: (key: string, comment: string) => void
  onSendFeedback: (key: string) => void
}) {
  const { t } = useTranslation()
  return (
    <div className="doc-chat-turn">
      <p className="bubble user">{turn.question}</p>
      {turn.answer ? (
        <div className="bubble assistant">
          <Markdown content={turn.answer} />
          {turn.status === 'done' && turn.queryAuditEventId ? (
            <div className="doc-chat-feedback">
              <button
                type="button"
                className={turn.feedback.value === 'up' ? 'feedback-button selected' : 'feedback-button'}
                aria-pressed={turn.feedback.value === 'up'}
                aria-label={t('docChat.feedback_up')}
                title={t('docChat.feedback_up')}
                onClick={() => onFeedbackValue(turn.key, 'up')}
              >
                <ThumbsUp size={14} aria-hidden="true" />
              </button>
              <button
                type="button"
                className={turn.feedback.value === 'down' ? 'feedback-button selected' : 'feedback-button'}
                aria-pressed={turn.feedback.value === 'down'}
                aria-label={t('docChat.feedback_down')}
                title={t('docChat.feedback_down')}
                onClick={() => onFeedbackValue(turn.key, 'down')}
              >
                <ThumbsDown size={14} aria-hidden="true" />
              </button>
              {turn.feedback.status === 'sent' ? (
                <span className="feedback-note">{t('docChat.feedback_thanks')}</span>
              ) : null}
              {turn.feedback.status === 'error' ? (
                <span className="feedback-note error">{t('docChat.feedback_error')}</span>
              ) : null}
              {turn.feedback.value && turn.feedback.status !== 'sent' ? (
                <div className="feedback-comment">
                  <Input
                    value={turn.feedback.comment}
                    onChange={(event) => onFeedbackComment(turn.key, event.target.value)}
                    placeholder={t('docChat.feedback_comment_placeholder')}
                  />
                  <Button
                    type="button"
                    className="primary-button"
                    disabled={turn.feedback.status === 'sending'}
                    onClick={() => void onSendFeedback(turn.key)}
                  >
                    {t('docChat.feedback_send')}
                  </Button>
                </div>
              ) : null}
            </div>
          ) : null}
        </div>
      ) : null}
    </div>
  )
}
```

- [ ] **Step 5: Mount the widget in `ViewerLinkApp.tsx`**

Add the import:

```tsx
import { DocChatWidget } from '../docChat/DocChatWidget'
```

and change the ready branch:

```tsx
        {state.status === 'ready' ? (
          <>
            <DocumentView document={state.document} />
            {state.document.state === 'Published' ? (
              <DocChatWidget documentId={state.document.documentId} />
            ) : null}
          </>
        ) : null}
```

- [ ] **Step 6: Run the tests**

Run: `pnpm.cmd --dir apps\docs-web test -- --run src/App.test.tsx`
Expected: all PASS, including the three new doc-chat tests.

- [ ] **Step 7: Typecheck + build + lint**

Run: `pnpm.cmd --dir apps\docs-web typecheck; pnpm.cmd --dir apps\docs-web build; pnpm.cmd --dir apps\docs-web lint`
Expected: typecheck and build clean; lint reports nothing new in the files this plan touched.

- [ ] **Step 8: Commit**

```powershell
git add apps/docs-web/src
git commit -m "feat(docs-web): ephemeral document-scoped mini chat with streaming and feedback"
```

---

## Task 11: Context docs, graph refresh, final verification

**Files:**
- Modify: `context/architecture.md`
- Modify: `context/ui-context.md`
- Modify: `context/progress-tracker.md`
- (Already recorded on 2026-06-10: the `context/design-decisions.md` entry.)

- [ ] **Step 1: Update `context/architecture.md`**

(a) In the same-origin routing table (~line 43), change the docs row:

```markdown
| `docs.client.com` | `/api/chat`, `/api/feedback*` | FastAPI RAG API |
| `docs.client.com` | all other `/api/*` | .NET API |
```

(b) In the prose paragraph (~line 67) that starts "Management and docs frontends call the .NET-owned contract groups.", replace the first sentence with:

```markdown
Management calls only the .NET-owned contract groups. The docs frontend calls .NET for everything except the document-scoped mini chat, which posts `/api/chat` (with `documentId`) and `/api/feedback/*` to FastAPI through the same-origin docs host routes. The chat frontend calls FastAPI for chat and feedback, and calls .NET through same-origin chat routes only for auth/session and viewer-link support.
```

(c) In the Chat/RAG row of the service table (~line 61), append to the scope description: `Document-scoped chat requests force the published corpus, bypass the semantic cache, and audit scope_document_id.`

- [ ] **Step 2: Update `context/ui-context.md` — replace the "Document Viewer UI" section**

```markdown
## Document Viewer UI

- `docs.localhost` root renders an independent authenticated document portal styled as a warm help center: bounded search, document-type filter chips (access groups are not navigation), and a card grid with type icon/tint, audience, relative updated date, and a state chip only for non-published documents. Visible documents are determined by the current user; management roles (`Admin`, `DocumentEditor`, `DocumentPublisher`) open documents with management purpose links.
- Document-id URLs render the focused viewer: a sticky top bar with a "Volver a la biblioteca" link back to the portal root, type/audience chips above the title, a centered reading column (max 72ch, 17px/1.7), a quiet session-expiry footer line, and a tinted banner when viewing draft/in-review versions.
- A floating document-chat bubble renders only on Published documents. The panel is ephemeral (state lost on reload), streams answers from FastAPI's document-scoped `/api/chat`, supports 👍/👎 feedback with optional comment per answer, shows budget-limited and rate-limited states with friendly copy, and rolls back partial turns on stream failure with a retry action. Doc-chat conversations never appear in chat-web's session drawer.
- The viewer must handle missing session, unauthorized, document-not-found, and successful document states. All docs-web user-facing strings are i18n keys (es-AR default, en-US), including error-code mappings.
- Credential-bearing tokens must never be visible to JavaScript or shown in the URL.
```

- [ ] **Step 3: Update `context/progress-tracker.md`**

Add a Completed entry (date, summary of: doc-scoped chat backend, Caddy routing, docs-web redesign, verification results) and update In Progress/Next Up to point at user-owned browser acceptance (spec §9). Keep status only — no architecture restating.

- [ ] **Step 4: Full verification suite**

```powershell
Set-Location services\rag-api
uv run pytest -q
uv run ruff check .
uv run mypy src tests
Set-Location ..\..
pnpm.cmd --dir apps\docs-web test -- --run
pnpm.cmd --dir apps\docs-web typecheck
pnpm.cmd --dir apps\docs-web build
pnpm.cmd --dir apps\chat-web test -- --run src/App.test.tsx
docker compose --env-file infra/compose/.env.example -f infra/compose/compose.yaml config
git diff --check
```

Expected: everything passes (chat-web is untouched but its suite guards the shared `/api/chat` contract); `git diff --check` shows at most Windows line-ending warnings.

- [ ] **Step 5: Refresh the knowledge graph**

Run: `graphify update .`

- [ ] **Step 6: Commit**

```powershell
git add context/architecture.md context/ui-context.md context/progress-tracker.md
git commit -m "docs: record docs-web redesign and document-scoped chat in context"
```

---

## Post-implementation (user-owned)

Browser acceptance through the Compose stack — see spec §9 (`docs/superpowers/specs/2026-06-10-docs-web-redesign-and-doc-chat-design.md`), including the Postman checklist:

| Check | Method | Path | Auth | Expected |
| --- | --- | --- | --- | --- |
| Doc-scoped chat | POST | `https://docs.localhost/api/chat` | session cookie + `X-CSRF-Token` | 200 SSE with `answer-token`/`citations`/`done` |
| Without session | POST | `https://docs.localhost/api/chat` | none | 401 error envelope |
| Sessions not routed | GET | `https://docs.localhost/api/chat/sessions` | session cookie | 404 (falls through to .NET) |
| Feedback | POST | `https://docs.localhost/api/feedback/{auditId}` | session cookie + CSRF | 200 `{queryAuditEventId, value, comment}` |
