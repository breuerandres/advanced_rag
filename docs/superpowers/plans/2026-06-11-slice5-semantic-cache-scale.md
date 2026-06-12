# Slice 5: Semantic Cache Scale And Fidelity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make semantic cache lookups O(log n) in SQL instead of a Python full-scan per partition, replay the original citations on cache hits, and guard both cache and retrieval against embedding-model changes.

**Architecture:** One Alembic migration adds an HNSW index on `semantic_cache_entries.question_embedding` and a `citations jsonb` column. `_lookup_cache` becomes a single SQL nearest-neighbor query with the threshold checked on the returned similarity; `_write_cache` persists the citations payload verbatim. Retrieval and cache lookups both filter by the configured `embedding_model`, and cache lookup failures degrade gracefully (skip cache, log warning) per `rag-spec.md`.

**Tech Stack:** PostgreSQL + pgvector, SQLAlchemy async, Alembic, pytest + Testcontainers.

**Depends on:** Slice 4 (both slices edit `hybrid_retrieval.py`; this slice adds the chunk-level `embedding_model` filter to the restructured SQL).

---

## Background For A Zero-Context Engineer

- Today `_lookup_cache` (`services/rag-api/src/advanced_rag/rag/chat_service.py` ~line 501) selects **every** non-expired row of the `(corpus, access_scope_hash, filters_hash)` partition, parses each `question_embedding::text`, and computes cosine similarity in Python. With thousands of cached questions per scope this is per-request linear work.
- On a hit, citations are rebuilt by picking the first active chunk of each source document (lateral join) because original citations were never stored. That loses citation fidelity (C5).
- `rag.semantic_cache_entries` already has `embedding_model` and `embedding_dimensions` columns (written, never read back in lookup). Vectors from different embedding models must never be compared: same-dimension comparisons are silently meaningless, and different dimensions make `zip(..., strict=True)` raise (a 500 today).
- `rag.document_chunks.embedding_model` exists too; retrieval never filters by it (C4).
- The btree `ix_semantic_cache_entries_scope_lookup (corpus, access_scope_hash, expires_at)` exists; there is no vector index on the cache table.

## File Map

- Create: `services/rag-api/alembic/versions/<next_id>_cache_citations_and_vector_index.py` (confirm head with `uv run alembic heads` first)
- Modify: `services/rag-api/src/advanced_rag/rag/chat_service.py`
- Modify: `services/rag-api/src/advanced_rag/rag/hybrid_retrieval.py`
- Modify: `services/rag-api/tests/test_chat_rag.py`
- Modify: `services/rag-api/tests/test_migrations.py`
- Modify: `context/rag-spec.md` (Semantic Cache section)

---

## Task 1: Migration — Citations Column And Cache Vector Index

**Files:**
- Create: `services/rag-api/alembic/versions/<next_id>_cache_citations_and_vector_index.py`
- Modify: `services/rag-api/tests/test_migrations.py`

- [ ] **Step 1: Failing migration test**

Extend `test_migrations.py` to assert after upgrade: column `citations` (jsonb, not null, default `'[]'`) exists on `rag.semantic_cache_entries`, and index `ix_semantic_cache_entries_question_embedding_hnsw` exists.

- [ ] **Step 2: Run to verify failure**

Run: `uv run pytest tests/test_migrations.py -q` → FAIL.

- [ ] **Step 3: Write the migration**

```python
def upgrade() -> None:
    op.execute(
        """
        ALTER TABLE rag.semantic_cache_entries
            ADD COLUMN citations jsonb NOT NULL DEFAULT '[]'::jsonb;
        """
    )
    op.execute(
        """
        CREATE INDEX ix_semantic_cache_entries_question_embedding_hnsw
            ON rag.semantic_cache_entries
            USING hnsw (question_embedding vector_cosine_ops)
            WITH (m = 16, ef_construction = 64);
        """
    )
    # Pre-existing entries have no stored citations; they cannot be replayed
    # faithfully, so retire them instead of serving degraded hits.
    op.execute("DELETE FROM rag.semantic_cache_entries WHERE citations = '[]'::jsonb;")


def downgrade() -> None:
    op.execute("DROP INDEX IF EXISTS rag.ix_semantic_cache_entries_question_embedding_hnsw;")
    op.execute("ALTER TABLE rag.semantic_cache_entries DROP COLUMN IF EXISTS citations;")
```

- [ ] **Step 4: Run migration tests** → PASS. **Commit:**

```bash
git add services/rag-api/alembic services/rag-api/tests/test_migrations.py
git commit -m "feat(rag): cache citations column and hnsw index on question embeddings"
```

## Task 2: Store And Replay Citations; SQL Nearest-Neighbor Lookup

**Files:**
- Modify: `services/rag-api/src/advanced_rag/rag/chat_service.py`
- Modify: `services/rag-api/tests/test_chat_rag.py`

- [ ] **Step 1: Failing tests**

Add/adjust in `test_chat_rag.py` (reuse the existing cache-hit test arrangement):

```python
async def test_cache_hit_replays_original_citations(...):
    # Arrange: full RAG answer (writes cache) where the cited chunk is NOT the
    # first chunk of its document (seed two chunks; make the second one match).
    # Act: ask a near-identical question (same embedding → similarity 1.0).
    # Assert: cache_hit is True and citations == the original answer's citations
    # (same chunk_id, document_version_id, heading_path).

async def test_cache_lookup_ignores_entries_from_other_embedding_model(...):
    # Arrange: insert a cache entry row directly with embedding_model='other-model'
    # and a question_embedding equal to the query embedding.
    # Act: ask the question.
    # Assert: cache_hit is False (full RAG path ran).

async def test_cache_lookup_failure_degrades_gracefully(...):
    # Arrange: monkeypatch the session execute used by _lookup_cache to raise
    # (or drop the citations column in this test's DB) — simplest: patch
    # ChatService._lookup_cache_query to raise RuntimeError via a small seam.
    # Assert: ChatService.answer still returns a full generated answer.
```

- [ ] **Step 2: Run to verify failures** → the first two FAIL (third may fail for the wrong reason until the seam exists).

- [ ] **Step 3: Implement**

In `_write_cache`, add the citations payload to the INSERT:

```python
"citations": json.dumps(
    [
        {
            "chunk_id": str(c.chunk_id),
            "document_id": str(c.document_id),
            "document_version_id": str(c.document_version_id),
            "heading_path": c.heading_path,
        }
        for c in citations
    ]
),
```

with the column added to the INSERT list as `citations` and value `cast(:citations as jsonb)`.

Replace `_lookup_cache` entirely:

```python
async def _lookup_cache(
    self,
    session: AsyncSession,
    *,
    corpus: str,
    access_scope_hash: str,
    filters_hash: str | None,
    question_embedding: list[float],
) -> dict[str, Any] | None:
    try:
        result = await session.execute(
            text(
                """
                select
                    id,
                    answer,
                    cached_at,
                    citations,
                    1 - (question_embedding <=> (:question_embedding)::vector) as similarity
                from rag.semantic_cache_entries
                where corpus = :corpus
                  and access_scope_hash = :access_scope_hash
                  and filters_hash is not distinct from :filters_hash
                  and embedding_model = :embedding_model
                  and expires_at > now()
                order by question_embedding <=> (:question_embedding)::vector
                limit 1
                """
            ),
            {
                "corpus": corpus,
                "access_scope_hash": access_scope_hash,
                "filters_hash": filters_hash,
                "embedding_model": self._settings.resolved_embedding_model,
                "question_embedding": _vector_literal(question_embedding),
            },
        )
        row = result.first()
    except Exception:  # noqa: BLE001 - cache must never break the chat path
        logger.warning("Semantic cache lookup failed; continuing without cache.", exc_info=True)
        return None
    if row is None:
        return None
    # Note: the HNSW scan orders globally and the WHERE filters by partition, so a
    # very crowded cache table can occasionally miss a valid entry (post-filtering).
    # That is acceptable here — a missed hit just runs the full RAG path. Do not add
    # correctness logic that depends on cache hits.
    threshold = Decimal(str(self._settings.rag_semantic_cache_similarity_threshold))
    if Decimal(str(row.similarity)) < threshold:
        return None
    payload = row.citations if isinstance(row.citations, list) else json.loads(row.citations)
    return {
        "id": row.id,
        "answer": row.answer,
        "cached_at": row.cached_at,
        "citations": [
            Citation(
                chunk_id=UUID(item["chunk_id"]),
                document_id=UUID(item["document_id"]),
                document_version_id=UUID(item["document_version_id"]),
                heading_path=list(item["heading_path"]),
            )
            for item in payload
        ],
    }
```

Add a module-level `logger = logging.getLogger(__name__)` (`import logging`) if the module has none. Delete the now-unused `_cosine_similarity` and `_parse_vector` helpers and the old lateral-join citation reconstruction.

- [ ] **Step 4: Run the cache tests and full suite**

Run: `uv run pytest tests/test_chat_rag.py -q` → PASS.

- [ ] **Step 5: Commit**

```bash
git add services/rag-api/src/advanced_rag/rag/chat_service.py services/rag-api/tests/test_chat_rag.py
git commit -m "perf(rag): sql nearest-neighbor cache lookup with stored citations and model guard"
```

## Task 3: Embedding-Model Guard In Retrieval

**Files:**
- Modify: `services/rag-api/src/advanced_rag/rag/hybrid_retrieval.py`
- Modify: `services/rag-api/src/advanced_rag/rag/chat_service.py`
- Modify: `services/rag-api/tests/test_chat_rag.py`

- [ ] **Step 1: Failing test**

```python
async def test_retrieval_ignores_chunks_from_other_embedding_model(...):
    # Arrange: standard accessible document, but its chunk row has
    # embedding_model = 'legacy-model' (insert directly).
    # Act: ChatService.answer.
    # Assert: no citations (chunk filtered out).
```

- [ ] **Step 2: Implement**

Add to `HybridRetrievalParams`: `embedding_model: str`. Add to **both** candidate CTE WHERE clauses (next to `chunk.is_active = true`):

```sql
AND chunk.embedding_model = :embedding_model
```

Bind it in `hybrid_retrieve` (`"embedding_model": params.embedding_model`) and pass `embedding_model=self._settings.resolved_embedding_model` from `ChatService._retrieve_chunks`.

- [ ] **Step 3: Run suite** → PASS. **Step 4: Lint/typecheck.** **Step 5: Commit:**

```bash
git add services/rag-api
git commit -m "fix(rag): filter retrieval and cache by configured embedding model"
```

## Task 4: Spec Update And Verification

- [ ] **Step 1: Update `context/rag-spec.md` Semantic Cache section**: lookup is a pgvector nearest-neighbor query (`HNSW` on `question_embedding`) filtered by `(corpus, access_scope_hash, filters_hash, embedding_model)`; entries store the original citations and replay them verbatim; lookup failure degrades to the full RAG path. Note in Retrieval: chunks are filtered by the configured `embedding_model`; changing the embedding model requires re-indexing (documents stop being retrievable until re-indexed under the new model).

- [ ] **Step 2: Full verification**: `uv run pytest -q && uv run ruff check . && uv run mypy src tests` → green.

- [ ] **Step 3:** `graphify update .`, then:

```bash
git add context/rag-spec.md graphify-out
git commit -m "docs(context): semantic cache sql lookup, stored citations, embedding-model guard"
```
