# Slice 4: Retrieval Scale Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Keep retrieval recall and latency stable as the document base grows: resolve the user's allowed-document set once per query (instead of per candidate row), configure HNSW search depth, enable pgvector iterative scans, and move to partial vector indexes.

**Architecture:** Restructure `HYBRID_RETRIEVAL_SQL` so a `MATERIALIZED` `allowed_documents` CTE evaluates the access-rule + lifecycle predicates once over `app.documents`, and both candidate CTEs semi-join it. Before the query, the session sets `hnsw.ef_search` and (when pgvector >= 0.8) `hnsw.iterative_scan = relaxed_order`, which makes the index scan continue past `ef_search` until the LIMIT is satisfied — this is the fix for filtered-recall collapse. Replace the global HNSW index with per-corpus partial indexes that exclude inactive rows.

**Tech Stack:** PostgreSQL + pgvector (compose image `pgvector/pgvector:pg16`), SQLAlchemy async, Alembic, pytest + Testcontainers.

**Depends on:** Slice 1 (`_DOCUMENT_STATE_PREDICATE` and the `app.documents` grant must already exist).

---

## Background For A Zero-Context Engineer

- The problem: `ORDER BY embedding <=> :q LIMIT k` uses the HNSW index, which yields at most `hnsw.ef_search` candidates (default 40, never configured today). The permission WHERE clause then discards inaccessible rows **after** the scan. A user who can access 2% of a large corpus may end up with zero rows even though relevant accessible chunks exist. pgvector 0.8 added iterative index scans precisely for this.
- Secondary problem: the access predicate is a correlated `EXISTS` chain (permissions → permission_groups → org-unit closure) executed per candidate row, in both the vector CTE and the BM25 CTE. Its result depends only on `(document, user)`, so it belongs in a per-query CTE over documents, not per chunk row.
- Files: `services/rag-api/src/advanced_rag/rag/hybrid_retrieval.py` (SQL + params), `services/rag-api/src/advanced_rag/rag/chat_service.py` (`_retrieve_chunks` passes settings), `services/rag-api/src/advanced_rag/core/config.py` (knobs), `services/rag-api/src/advanced_rag/main.py` (startup capability detection).

## File Map

- Modify: `services/rag-api/src/advanced_rag/rag/hybrid_retrieval.py`
- Modify: `services/rag-api/src/advanced_rag/rag/chat_service.py`
- Modify: `services/rag-api/src/advanced_rag/core/config.py`
- Modify: `services/rag-api/src/advanced_rag/main.py`
- Create: `services/rag-api/alembic/versions/<next_id>_partial_hnsw_indexes.py` (id = current head date + 1, e.g. `20260611_150000`; confirm with `uv run alembic heads`)
- Modify: `services/rag-api/tests/test_chat_rag.py`
- Modify: `services/rag-api/tests/test_migrations.py`

---

## Task 1: Config Knobs And pgvector Capability Detection

**Files:**
- Modify: `services/rag-api/src/advanced_rag/core/config.py`
- Modify: `services/rag-api/src/advanced_rag/main.py`
- Modify: `services/rag-api/src/advanced_rag/rag/hybrid_retrieval.py`

- [ ] **Step 1: Add settings**

In `Settings` (next to the existing `rag_*` retrieval knobs):

```python
rag_hnsw_ef_search: int = 80
rag_hnsw_iterative_scan: bool = True  # effective only when pgvector >= 0.8
```

- [ ] **Step 2: Add a capability probe in `hybrid_retrieval.py`**

```python
async def detect_iterative_scan_support(connection: AsyncConnection) -> bool:
    """pgvector >= 0.8.0 supports `SET hnsw.iterative_scan`."""
    result = await connection.execute(
        text("select extversion from pg_extension where extname = 'vector'")
    )
    version = result.scalar_one_or_none()
    if not version:
        return False
    parts = str(version).split(".")
    try:
        major, minor = int(parts[0]), int(parts[1])
    except (IndexError, ValueError):
        return False
    return (major, minor) >= (0, 8)
```

- [ ] **Step 3: Probe once at startup**

In `main.py`'s lifespan/startup (where `app.state.chat_service` is built), open a connection from the session factory, run the probe, and store `app.state.hnsw_iterative_scan_supported: bool`. Pass it into `ChatService` as a constructor argument `hnsw_iterative_scan_supported: bool = False` stored on the service.

- [ ] **Step 4: Unit test for the version parser**

In a new or existing test module (e.g. `tests/test_hybrid_retrieval.py`), test `detect_iterative_scan_support` against a stub connection returning `"0.8.1"`, `"0.7.4"`, `None` → `True/False/False`. Run: `uv run pytest tests/test_hybrid_retrieval.py -q`. Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add services/rag-api
git commit -m "feat(rag): hnsw search knobs and pgvector iterative-scan capability probe"
```

## Task 2: Allowed-Documents CTE Restructure

**Files:**
- Modify: `services/rag-api/src/advanced_rag/rag/hybrid_retrieval.py`
- Modify: `services/rag-api/tests/test_chat_rag.py`

- [ ] **Step 1: Rewrite `HYBRID_RETRIEVAL_SQL`**

Replace the per-row predicates with a document-level CTE. The full new statement:

```sql
WITH allowed_documents AS MATERIALIZED (
    SELECT doc."Id" AS document_id,
           doc.current_published_version_id,
           doc.current_draft_version_id,
           doc.current_state
    FROM app.documents doc
    WHERE
      (
          CAST(:is_global_admin AS boolean)
          OR EXISTS (
              SELECT 1
              FROM app.document_permissions p
              WHERE p.document_id = doc."Id"
                AND (
                    p.organizational_unit_id IS NOT NULL
                    OR EXISTS (
                        SELECT 1
                        FROM app.document_permission_groups pg
                        WHERE pg.document_permission_id = p."Id"
                    )
                )
                AND (
                    p.organizational_unit_id IS NULL
                    OR p.organizational_unit_id = :root_organizational_unit_id
                    OR EXISTS (
                        SELECT 1
                        FROM app.organizational_unit_closure c
                        WHERE
                            (c.ancestor_id = p.organizational_unit_id
                                AND c.descendant_id = :user_organizational_unit_id)
                            OR
                            (c.ancestor_id = :user_organizational_unit_id
                                AND c.descendant_id = p.organizational_unit_id)
                    )
                )
                AND (
                    NOT EXISTS (
                        SELECT 1
                        FROM app.document_permission_groups pg
                        WHERE pg.document_permission_id = p."Id"
                    )
                    OR EXISTS (
                        SELECT 1
                        FROM app.document_permission_groups pg
                        WHERE pg.document_permission_id = p."Id"
                          AND pg.group_id = ANY(CAST(:user_groups AS uuid[]))
                    )
                )
          )
      )
      AND (
          CAST(:scope_document_id AS uuid) IS NULL
          OR doc."Id" = CAST(:scope_document_id AS uuid)
      )
      AND (
          CAST(:dimension_value_filter AS uuid[]) IS NULL
          OR EXISTS (
              SELECT 1 FROM app.document_dimension_values ddv
              WHERE ddv.document_id = doc."Id"
                AND ddv.dimension_value_id = ANY(CAST(:dimension_value_filter AS uuid[]))
          )
      )
),
vector_candidates AS (
    SELECT
        chunk.id,
        chunk.document_id,
        chunk.document_version_id,
        chunk.content,
        chunk.heading_path,
        row_number() OVER (ORDER BY chunk.embedding <=> CAST(:q_embedding AS vector)) AS rank
    FROM rag.document_chunks chunk
    JOIN allowed_documents ad ON ad.document_id = chunk.document_id
    WHERE chunk.corpus = :corpus
      AND chunk.is_active = true
      AND (
          (chunk.corpus = 'published'
              AND ad.current_state = 'Published'
              AND ad.current_published_version_id = chunk.document_version_id)
          OR
          (chunk.corpus = 'preview' AND ad.current_state <> 'Archived')
      )
    ORDER BY chunk.embedding <=> CAST(:q_embedding AS vector)
    LIMIT :vector_top_k
),
bm25_candidates AS (
    SELECT
        chunk.id,
        chunk.document_id,
        chunk.document_version_id,
        chunk.content,
        chunk.heading_path,
        row_number() OVER (
            ORDER BY
                ts_rank_cd(chunk.content_tsv, websearch_to_tsquery('simple', rag.f_immutable_unaccent(:q_text))) DESC,
                similarity(chunk.content, :q_text) DESC
        ) AS rank
    FROM rag.document_chunks chunk
    JOIN allowed_documents ad ON ad.document_id = chunk.document_id
    WHERE chunk.corpus = :corpus
      AND chunk.is_active = true
      AND (
          (chunk.corpus = 'published'
              AND ad.current_state = 'Published'
              AND ad.current_published_version_id = chunk.document_version_id)
          OR
          (chunk.corpus = 'preview' AND ad.current_state <> 'Archived')
      )
      AND (
          chunk.content_tsv @@ websearch_to_tsquery('simple', rag.f_immutable_unaccent(:q_text))
          OR chunk.content % :q_text
      )
    ORDER BY
        ts_rank_cd(chunk.content_tsv, websearch_to_tsquery('simple', rag.f_immutable_unaccent(:q_text))) DESC,
        similarity(chunk.content, :q_text) DESC
    LIMIT :bm25_top_k
),
fused AS (
    SELECT
        id,
        document_id,
        document_version_id,
        content,
        heading_path,
        SUM(1.0 / (CAST(:rrf_k AS float) + rank)) AS rrf_score
    FROM (
        SELECT id, document_id, document_version_id, content, heading_path, rank FROM vector_candidates
        UNION ALL
        SELECT id, document_id, document_version_id, content, heading_path, rank FROM bm25_candidates
    ) AS combined
    GROUP BY id, document_id, document_version_id, content, heading_path
)
SELECT id, document_id, document_version_id, content, heading_path, rrf_score
FROM fused
ORDER BY rrf_score DESC
LIMIT :final_top_k
```

Notes for the implementer:

- The bind-parameter set is identical to today's; no `hybrid_retrieve` signature change in this step.
- Delete the now-unused `_ACCESS_RULE_PREDICATE`, `_SCOPE_DOCUMENT_PREDICATE`, and `_DOCUMENT_STATE_PREDICATE` constants (their logic moved into the CTE / the join condition).
- `MATERIALIZED` is intentional: it forces a single evaluation of the permission resolution and gives the planner a stable row set for both candidate scans.

- [ ] **Step 2: Run the whole retrieval test suite**

Run: `uv run pytest tests/test_chat_rag.py -q`
Expected: PASS — Slice 1's lifecycle tests and the hierarchical branch tests are the behavioral contract; this is a pure restructure and must change no results.

- [ ] **Step 3: Commit**

```bash
git add services/rag-api/src/advanced_rag/rag/hybrid_retrieval.py
git commit -m "perf(rag): resolve allowed documents once per query via materialized CTE"
```

## Task 3: `ef_search` + Iterative Scan Session Settings

**Files:**
- Modify: `services/rag-api/src/advanced_rag/rag/hybrid_retrieval.py`
- Modify: `services/rag-api/src/advanced_rag/rag/chat_service.py`
- Modify: `services/rag-api/tests/test_chat_rag.py`

- [ ] **Step 1: Write the failing recall test**

Add to `test_chat_rag.py` (uses the standard fixture + fake embedding provider; embeddings in these tests are deterministic vectors, so craft them accordingly):

```python
async def test_selective_scope_recall_survives_large_inaccessible_corpus(...):
    # Arrange: seed ONE accessible document with a chunk whose embedding is the
    # closest to the query embedding, plus 300 chunks across documents the user
    # CANNOT access, all nearer-than-noise so they fill the HNSW candidate queue.
    # Build the HNSW index AFTER inserts (CREATE INDEX in the test, or reindex)
    # so the scan actually uses it.
    # Act: ChatService.answer with the restrictive claims.
    # Assert: the accessible chunk IS cited (non-empty citations).
```

With default `ef_search=40` semantics and no iterative scan this can return zero accessible rows; the assertion encodes the recall guarantee.

- [ ] **Step 2: Add the session settings to `hybrid_retrieve`**

Extend `HybridRetrievalParams` with:

```python
ef_search: int = 80
iterative_scan: bool = False  # set only when server support was detected
```

At the top of `hybrid_retrieve`, before executing the main statement (same connection/transaction, so `SET LOCAL` scopes correctly):

```python
await connection.execute(
    text("SET LOCAL hnsw.ef_search = :ef").bindparams(ef=params.ef_search)
)
if params.iterative_scan:
    await connection.execute(text("SET LOCAL hnsw.iterative_scan = 'relaxed_order'"))
```

If `SET LOCAL` with a bind parameter is rejected by the driver, format the integer directly (`text(f"SET LOCAL hnsw.ef_search = {int(params.ef_search)}")`) — it is validated as `int` by Pydantic, so no injection surface.

- [ ] **Step 3: Thread from chat_service**

In `ChatService._retrieve_chunks`, add to the `HybridRetrievalParams(...)` construction:

```python
ef_search=self._settings.rag_hnsw_ef_search,
iterative_scan=(
    self._settings.rag_hnsw_iterative_scan and self._hnsw_iterative_scan_supported
),
```

(`self._hnsw_iterative_scan_supported` comes from Task 1's constructor argument.)

- [ ] **Step 4: Run the recall test and the suite**

Run: `uv run pytest tests/test_chat_rag.py -q`
Expected: PASS (testcontainers pulls `pgvector/pgvector:pg16`, which ships pgvector >= 0.8; if the local image is older, the capability probe makes the test exercise only `ef_search` — keep the seeded inaccessible count at 300 so a raised `ef_search=80` still demonstrates recall).

- [ ] **Step 5: Lint, typecheck, commit**

Run: `uv run ruff check . && uv run mypy src tests`

```bash
git add services/rag-api
git commit -m "perf(rag): set hnsw.ef_search and iterative scan per retrieval query"
```

## Task 4: Partial HNSW Indexes

**Files:**
- Create: `services/rag-api/alembic/versions/<next_id>_partial_hnsw_indexes.py`
- Modify: `services/rag-api/tests/test_migrations.py`

- [ ] **Step 1: Write the failing migration test**

In `test_migrations.py`, extend the index-assertion test (see the existing `ix_document_chunks_*` list near the top of the file) to expect `ix_document_chunks_embedding_hnsw_published` and `ix_document_chunks_embedding_hnsw_preview`, and to expect the old `ix_document_chunks_embedding_hnsw` to be **absent** after upgrade.

- [ ] **Step 2: Run to verify failure**

Run: `uv run pytest tests/test_migrations.py -q`
Expected: FAIL on the new index names.

- [ ] **Step 3: Write the migration**

```python
def upgrade() -> None:
    op.execute("DROP INDEX IF EXISTS rag.ix_document_chunks_embedding_hnsw;")
    op.execute(
        """
        CREATE INDEX ix_document_chunks_embedding_hnsw_published
            ON rag.document_chunks
            USING hnsw (embedding vector_cosine_ops)
            WITH (m = 16, ef_construction = 64)
            WHERE corpus = 'published' AND is_active = true;
        """
    )
    op.execute(
        """
        CREATE INDEX ix_document_chunks_embedding_hnsw_preview
            ON rag.document_chunks
            USING hnsw (embedding vector_cosine_ops)
            WITH (m = 16, ef_construction = 64)
            WHERE corpus = 'preview' AND is_active = true;
        """
    )


def downgrade() -> None:
    op.execute("DROP INDEX IF EXISTS rag.ix_document_chunks_embedding_hnsw_published;")
    op.execute("DROP INDEX IF EXISTS rag.ix_document_chunks_embedding_hnsw_preview;")
    op.execute(
        """
        CREATE INDEX ix_document_chunks_embedding_hnsw
            ON rag.document_chunks
            USING hnsw (embedding vector_cosine_ops)
            WITH (m = 16, ef_construction = 64);
        """
    )
```

Set `down_revision` to the current head (run `uv run alembic heads` first). Deactivated chunks drop out of the index automatically on vacuum.

**Critical companion change — inline the corpus literal:** a partial index is only usable when the planner can prove the predicate. With `corpus = :corpus` as a bound parameter, Postgres' generic plans (which prepared statements switch to after a few executions) cannot match the partial-index predicate and will silently stop using the vector index. Fix in `hybrid_retrieve`:

```python
_ALLOWED_CORPUS = {"published", "preview"}

# at the top of hybrid_retrieve:
if params.corpus not in _ALLOWED_CORPUS:
    raise ValueError(f"Unsupported corpus: {params.corpus!r}")
sql = HYBRID_RETRIEVAL_SQL_TEMPLATE.format(corpus_literal=f"'{params.corpus}'")
statement = text(sql)
```

Change `HYBRID_RETRIEVAL_SQL` into a module-level **string template** `HYBRID_RETRIEVAL_SQL_TEMPLATE` where every `chunk.corpus = :corpus` becomes `chunk.corpus = {corpus_literal}` and the two corpus comparisons inside the lifecycle join condition use `{corpus_literal}` likewise; drop the `:corpus` bind parameter. The value is whitelist-validated, so there is no injection surface. Add a unit test asserting `hybrid_retrieve` raises `ValueError` for an unknown corpus.

- [ ] **Step 4: Run migration + retrieval tests**

Run: `uv run pytest tests/test_migrations.py tests/test_chat_rag.py -q`
Expected: PASS.

- [ ] **Step 5: Update spec, graph, commit**

Update `context/rag-spec.md` "pgvector Index" section: partial per-corpus HNSW indexes, `ef_search` default 80 set per query via `SET LOCAL`, iterative scan `relaxed_order` when pgvector >= 0.8.

Run: `graphify update .`

```bash
git add services/rag-api context/rag-spec.md graphify-out
git commit -m "perf(rag): partial per-corpus hnsw indexes and spec update"
```

- [ ] **Step 6 (USER-OWNED): production note**

Tell the user: on an existing deployment with a large `rag.document_chunks` table this migration rebuilds vector indexes; run it in a maintenance window. Local Compose needs nothing special.
