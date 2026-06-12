# Slice 1: Retrieval Lifecycle Correctness Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make retrieval respect the document lifecycle: archived or restored-to-draft documents must never be retrieved, and only the current published version of a document is retrievable in the `published` corpus.

**Architecture:** Two complementary fixes. (a) A lifecycle predicate inside the retrieval SQL joins `app.documents` (read through a new `.NET`-granted SELECT) and pins published-corpus chunks to `current_published_version_id` — self-healing, no event ordering. (b) The indexing pipeline deactivates prior chunks by `(document_id, corpus)` instead of by `document_version_id`, so superseded versions also stop occupying the HNSW index.

**Tech Stack:** FastAPI + SQLAlchemy async + asyncpg, Alembic (no new migration needed on rag side), EF Core migration (grant only), pytest + Testcontainers, xUnit.

**Why both:** the SQL predicate guarantees correctness even if a deactivation call is missed; the deactivation keeps dead vectors out of the index. Neither alone is sufficient.

---

## Background For A Zero-Context Engineer

- `rag.document_chunks` rows are created by `services/rag-api/src/advanced_rag/rag/indexing_service.py` when `.NET` calls `POST /internal/indexing-jobs` at publish time, with `corpus` = `published` or `preview` and the **new** `document_version_id`.
- Current bug 1: `create_job` runs `update rag.document_chunks set is_active = false where document_version_id = :document_version_id` — that targets the *new* version (no rows yet), so the previous published version's chunks stay `is_active = true` forever.
- Current bug 2: the retrieval SQL in `services/rag-api/src/advanced_rag/rag/hybrid_retrieval.py` filters by `corpus`, `is_active` and access rules (`app.document_permissions` + closure) but never consults `app.documents.current_state`, so archiving a document changes nothing for retrieval.
- FastAPI already reads several `app.*` tables through grants applied by hand-written `.NET` EF migrations (see `services/dotnet-api/src/AdvancedRag.Infrastructure/Migrations/20260520173000_GrantRagOwnerAppReadAccess.cs`). `app.documents` is not granted yet.
- `app.documents` columns (from `20260513184201_InitialAppSchema.cs`): `"Id" uuid`, `title`, `current_state varchar(32)` (values `Draft`, `InReview`-style strings, `Published`, `Archived` — tests only need `Published`/`Archived`), `current_draft_version_id uuid null`, `current_published_version_id uuid null`, `created_by_user_id`, timestamps. Note `"Id"` is quoted PascalCase; the rest snake_case.
- rag-api integration tests bootstrap their own mirror of the `app` schema in `services/rag-api/tests/test_chat_rag.py` (`ChatDatabase._bootstrap`, ~line 938) and seed documents around line 1101. The seed currently inserts `app.documents` **without** `current_published_version_id` — this slice must update the fixture or the new predicate filters everything out.

## File Map

- Modify: `services/rag-api/src/advanced_rag/rag/hybrid_retrieval.py` (new lifecycle predicate)
- Modify: `services/rag-api/src/advanced_rag/rag/indexing_service.py` (supersede by document+corpus)
- Modify: `services/rag-api/tests/test_chat_rag.py` (fixture version pointers + 3 new tests)
- Modify: `services/rag-api/tests/test_indexing.py` (supersede test)
- Create: `services/dotnet-api/src/AdvancedRag.Infrastructure/Migrations/20260611090000_GrantRagOwnerDocumentsReadAccess.cs`
- Modify: `context/rag-spec.md` (Retrieval section: document the lifecycle predicate)

---

## Task 1: .NET Grant For `app.documents`

**Files:**
- Create: `services/dotnet-api/src/AdvancedRag.Infrastructure/Migrations/20260611090000_GrantRagOwnerDocumentsReadAccess.cs`

- [ ] **Step 1: Read the reference migration**

Open `services/dotnet-api/src/AdvancedRag.Infrastructure/Migrations/20260520173000_GrantRagOwnerAppReadAccess.cs` and copy its exact class scaffolding (attributes, namespace, partial class + Designer conventions used by this repo for hand-written migrations).

- [ ] **Step 2: Create the migration with this SQL**

Up:

```sql
do $$
begin
    if to_regrole('rag_owner') is not null then
        if to_regclass('app.documents') is not null then
            grant select on table app.documents to rag_owner;
        end if;
    end if;
end
$$;
```

Down:

```sql
do $$
begin
    if to_regrole('rag_owner') is not null then
        if to_regclass('app.documents') is not null then
            revoke select on table app.documents from rag_owner;
        end if;
    end if;
end
$$;
```

Wrap both in `migrationBuilder.Sql(...)` exactly like the reference migration does.

- [ ] **Step 3: Build and run .NET tests**

Run: `dotnet build services\dotnet-api\AdvancedRag.sln --no-restore` then `dotnet test services\dotnet-api\AdvancedRag.sln`
Expected: build OK, tests green (the migration is grant-only; if a migrations-shape test asserts the migration list, update it the same way previous grant migrations did).

- [ ] **Step 4: Commit**

```bash
git add services/dotnet-api
git commit -m "feat(dotnet): grant rag_owner read access to app.documents for retrieval lifecycle filter"
```

## Task 2: Test Fixture — Version Pointers In The rag Test `app` Schema

**Files:**
- Modify: `services/rag-api/tests/test_chat_rag.py`

- [ ] **Step 1: Extend the document seed with version pointers**

In `ChatDatabase` (the seed loop near line 1101), give each seeded document an explicit published version id and store it on the document row. Replace the `INSERT INTO app.documents` statement with:

```python
INSERT INTO app.documents (
    "Id", title, current_state, current_published_version_id, created_by_user_id
)
VALUES ($1, $2, 'Published', $3, $4)
```

Generate `version_id = uuid4()` per document inside the loop, pass it as `$3`, and pass the same `version_id` into the chunk insert so `rag.document_chunks.document_version_id` matches `app.documents.current_published_version_id`. Extend the `_insert_chunk` helper with a `document_version_id: UUID | None = None` keyword (defaulting to its current behavior of generating one) and use it everywhere the seed inserts chunks for these documents. For the `preview` document, also set `current_draft_version_id` to the preview chunk's version id (add the column to the INSERT for that row, or issue an UPDATE after insert).

Apply the same change to `seed_hierarchical_corpus` (each seeded published document needs `current_published_version_id` = its chunk's version id).

- [ ] **Step 2: Run the existing chat tests — they must still pass (predicate not added yet)**

Run: `uv run pytest tests/test_chat_rag.py -q` (from `services/rag-api`)
Expected: PASS (fixture change is backwards-compatible).

- [ ] **Step 3: Commit**

```bash
git add services/rag-api/tests/test_chat_rag.py
git commit -m "test(rag): seed app.documents version pointers in chat test fixture"
```

## Task 3: Lifecycle Predicate In Retrieval SQL (fix C1, hardens C2)

**Files:**
- Modify: `services/rag-api/src/advanced_rag/rag/hybrid_retrieval.py`
- Modify: `services/rag-api/tests/test_chat_rag.py`

- [ ] **Step 1: Write three failing tests**

Add to `test_chat_rag.py`, following the arrangement style of the existing branch-aware retrieval tests in the same file (same service construction, same fake providers, same claims helper). Test logic:

```python
async def test_archived_document_is_not_retrieved(...):
    # Arrange: seed an allowed Published document + chunk via the standard seed,
    # then flip it: UPDATE app.documents SET current_state = 'Archived' WHERE "Id" = $1
    # Act: ChatService.answer(question matching the chunk content, claims with access)
    # Assert: answer.citations == [] and the no-results message is returned.

async def test_restored_to_draft_document_is_not_retrieved(...):
    # Same arrangement but UPDATE app.documents
    # SET current_state = 'Draft', current_published_version_id = NULL WHERE "Id" = $1
    # Assert: no citations.

async def test_only_current_published_version_is_retrieved(...):
    # Arrange: one document with TWO chunk sets, version_a and version_b, both
    # corpus='published' and is_active=true (simulates the pre-fix supersede bug).
    # app.documents.current_published_version_id = version_b.
    # Act: ask a question matching both chunk contents.
    # Assert: every citation has document_version_id == version_b; none == version_a.
```

Write them as full tests (real inserts through the fixture connection, real `ChatService.answer` call) — copy the act/assert shape of the nearest existing test that asserts citations.

- [ ] **Step 2: Run the new tests to verify they fail**

Run: `uv run pytest tests/test_chat_rag.py -q -k "archived_document or restored_to_draft or current_published_version"`
Expected: FAIL — citations are returned for archived/draft/superseded content (that is the bug).

- [ ] **Step 3: Add the predicate**

In `hybrid_retrieval.py`, define next to `_ACCESS_RULE_PREDICATE`:

```python
# Lifecycle predicate: retrieval never serves chunks for documents that .NET no
# longer exposes. For the published corpus the chunk must belong to the document's
# CURRENT published version (self-healing against missed deactivations); for the
# preview corpus any non-archived document qualifies (version hygiene there is
# handled by `is_active`). Applies to every caller, including global admins —
# the admin bypass covers access rules only, not lifecycle state.
_DOCUMENT_STATE_PREDICATE = """
      AND EXISTS (
          SELECT 1
          FROM app.documents doc
          WHERE doc."Id" = chunk.document_id
            AND (
                (chunk.corpus = 'published'
                    AND doc.current_state = 'Published'
                    AND doc.current_published_version_id = chunk.document_version_id)
                OR
                (chunk.corpus = 'preview'
                    AND doc.current_state <> 'Archived')
            )
      )
"""
```

Insert `{_DOCUMENT_STATE_PREDICATE}` into **both** CTEs of `HYBRID_RETRIEVAL_SQL`, immediately after `{_ACCESS_RULE_PREDICATE}` (vector CTE and bm25 CTE).

- [ ] **Step 4: Run the full chat test file**

Run: `uv run pytest tests/test_chat_rag.py -q`
Expected: the three new tests PASS, and every pre-existing test PASSES (if an old test fails, its seeded document is missing the version pointer from Task 2 — fix the seed, not the predicate).

- [ ] **Step 5: Lint, typecheck, commit**

Run: `uv run ruff check .` and `uv run mypy src tests`
Expected: clean.

```bash
git add services/rag-api/src/advanced_rag/rag/hybrid_retrieval.py services/rag-api/tests/test_chat_rag.py
git commit -m "fix(rag): exclude archived and superseded documents from retrieval via lifecycle predicate"
```

## Task 4: Supersede By Document On Indexing (fix C2 at the data layer)

**Files:**
- Modify: `services/rag-api/src/advanced_rag/rag/indexing_service.py`
- Modify: `services/rag-api/tests/test_indexing.py`

- [ ] **Step 1: Write the failing test**

In `test_indexing.py`, add a test following the file's existing job-creation test pattern:

```python
async def test_new_version_deactivates_previous_version_chunks(...):
    # Arrange: run create_job for document D, version V1, corpus 'published' (succeeds).
    # Act: run create_job for the SAME document D, NEW version V2, corpus 'published'.
    # Assert:
    #   - all chunks with document_version_id == V1 have is_active = false
    #   - all chunks with document_version_id == V2 have is_active = true
    #   - chunks of a DIFFERENT document D2 indexed before remain is_active = true
```

- [ ] **Step 2: Run it to verify it fails**

Run: `uv run pytest tests/test_indexing.py -q -k deactivates_previous`
Expected: FAIL — V1 chunks remain `is_active = true`.

- [ ] **Step 3: Fix the deactivation statement**

In `indexing_service.py` `create_job`, replace the existing `update rag.document_chunks set is_active = false where document_version_id = :document_version_id` block with:

```python
await session.execute(
    text(
        """
        update rag.document_chunks
        set is_active = false
        where document_id = :document_id
          and corpus = :corpus
          and is_active = true
        """
    ),
    {"document_id": request.document_id, "corpus": request.corpus_mode},
)
```

This covers both re-indexing the same version and superseding an older version, scoped to the same corpus so a preview index never deactivates published chunks.

- [ ] **Step 4: Run the indexing tests**

Run: `uv run pytest tests/test_indexing.py -q`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add services/rag-api/src/advanced_rag/rag/indexing_service.py services/rag-api/tests/test_indexing.py
git commit -m "fix(rag): deactivate prior chunks by document and corpus when indexing a new version"
```

## Task 5: Spec Update And Full Verification

**Files:**
- Modify: `context/rag-spec.md`

- [ ] **Step 1: Update `context/rag-spec.md` Retrieval section**

In the Retrieval bullet list, replace the sentence about `is_active` (currently "Only chunks belonging to the latest successfully indexed version ... flipped to false when a newer version supersedes ...") with:

```markdown
- Retrieval applies a lifecycle predicate against `app.documents` (read-only grant):
  published-corpus chunks are eligible only when the document `current_state` is
  `Published` **and** the chunk's `document_version_id` equals the document's
  `current_published_version_id`; preview-corpus chunks require `current_state <>
  'Archived'`. `rag.document_chunks.is_active` remains an index-hygiene flag:
  indexing a version deactivates all prior chunks of the same `(document_id,
  corpus)`. Correctness never depends on `is_active` alone.
```

- [ ] **Step 2: Full verification**

Run, from `services/rag-api`: `uv run pytest -q && uv run ruff check . && uv run mypy src tests`
Run, from repo root: `dotnet test services\dotnet-api\AdvancedRag.sln`
Expected: all green.

- [ ] **Step 3: Update graph and commit**

Run: `graphify update .`

```bash
git add context/rag-spec.md graphify-out
git commit -m "docs(context): document retrieval lifecycle predicate and supersede rule"
```

- [ ] **Step 4 (USER-OWNED): Compose acceptance**

Ask the user to run the stack (`.\infra\compose\Start-Local.ps1`), publish a document, verify chat answers from it, then archive it and verify chat no longer cites it (a fresh question, not a cached one). Report results back.
