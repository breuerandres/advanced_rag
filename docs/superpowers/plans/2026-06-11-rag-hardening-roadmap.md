# RAG Hardening Roadmap (Slice Index)

> **For agentic workers:** This is the master index for a multi-session effort. Each slice below has its own implementation plan file and is executed in a **separate session** to keep context small. Within a session, use superpowers:executing-plans (or subagent-driven-development) on that slice's plan file only.

**Goal:** Fix the correctness bugs found in the 2026-06-11 rag-api deep review, ship query-time multimodal RAG, and prepare retrieval/cache for production-scale document growth.

**Source review findings (summary):**
- C1: Archived / restored-to-draft documents remain retrievable (no lifecycle check in retrieval SQL; nothing deactivates chunks by document).
- C2: Republishing never deactivates the previous published version's chunks (`indexing_service.py` deactivates by `document_version_id` only) — retrieval mixes versions.
- C3: `POST /internal/cache-invalidations` exists in rag-api but no .NET code ever calls it — stale cached answers survive archive/edit-after-publish up to TTL.
- Images: the entire query-time multimodal slice specified in `context/rag-spec.md` is unimplemented; only `<img alt>` text is indexed.
- E1: HNSW post-filtering recall collapse for users with selective access scopes (`hnsw.ef_search` never set; no iterative scan).
- E2: Triple correlated `EXISTS` permission predicate runs per candidate row in both CTEs.
- E3: Semantic cache lookup loads the whole partition and computes cosine in Python; no vector index; expired rows never deleted.
- C4: No embedding-model guard in retrieval or cache after a model change.
- C5: Cache-hit citations are reconstructed as "first chunk of source document", not the original citations.
- E4: Inactive chunks are never purged; HNSW bloat.
- E5: Budget check sums `rag.query_audit_events` per request with no `(user_id, created_at)` index.
- E6: SSE streaming is simulated — the full answer is generated before the first byte is sent.

## Slice Order And Dependencies

Execute strictly in this order; each slice merges before the next session starts (slices 3, 4, 5, 7 all touch `chat_service.py` / `hybrid_retrieval.py`).

| # | Slice | Plan file | Fixes | Depends on |
|---|-------|-----------|-------|------------|
| 1 | Retrieval lifecycle correctness | `2026-06-11-slice1-retrieval-correctness.md` | C1, C2 | — |
| 2 | .NET → rag invalidation client | `2026-06-11-slice2-dotnet-rag-invalidation.md` | C3 | — |
| 3 | Query-time multimodal RAG | `2026-06-01-query-time-multimodal-rag-implementation-plan.md` **+** `2026-06-11-slice3-multimodal-delta.md` | Images | 1 |
| 4 | Retrieval scale | `2026-06-11-slice4-retrieval-scale.md` | E1, E2 | 1 |
| 5 | Semantic cache scale + fidelity | `2026-06-11-slice5-semantic-cache-scale.md` | E3, C4, C5 | 4 |
| 6 | Operational hardening | `2026-06-11-slice6-operational-hardening.md` | E4, E5 | 1 |
| 7 | True SSE streaming | `2026-06-11-slice7-true-sse-streaming.md` | E6 | 3, 5 |

Status tracking: mark the checkbox here when a slice is merged and verified.

- [x] Slice 1 — Retrieval lifecycle correctness (branch `rag-hardening-s1-3`, agent-verified; Compose acceptance pending)
- [x] Slice 2 — .NET → rag invalidation client (branch `rag-hardening-s1-3`, agent-verified; Compose acceptance pending)
- [x] Slice 3 — Query-time multimodal RAG (branch `rag-hardening-s1-3`, agent-verified; Compose acceptance pending)
- [ ] Slice 4 — Retrieval scale
- [ ] Slice 5 — Semantic cache scale + fidelity
- [ ] Slice 6 — Operational hardening
- [ ] Slice 7 — True SSE streaming

## Deliberately Deferred (not in any slice)

- **Background indexing worker (E7):** indexing stays synchronous per request; the contract already allows moving it to a worker later without changes. Revisit when publish volume hurts.
- **Dimension-filter AND-between-dimensions semantics:** today `filters` is a flat OR over dimension values. Whether multi-dimension filters should intersect is a product decision — raise it with the user before changing.
- **tiktoken-based chunk sizing:** chunker counts whitespace words, not model tokens (spec says tiktoken). Fixing it changes `CHUNKER_VERSION` and forces re-indexing; bundle it with the next re-index-requiring change.
- **Offline image captioning/OCR at indexing time:** out of scope per `rag-spec.md`; reconsider if customers bring scanned/diagram-heavy documents where query-time multimodal (Slice 3) is not enough.

## Per-Session Protocol

Start every implementation session with this prompt (replace N):

```
Lee context/README.md y sigue su orden de lectura. Después abrí
docs/superpowers/plans/2026-06-11-rag-hardening-roadmap.md y ejecutá la Slice N
con su plan (usa superpowers:executing-plans). Slice N plan:
docs/superpowers/plans/<archivo de la tabla>.
Al terminar: tests verdes, actualizá context/progress-tracker.md (status),
marcá la slice en el roadmap, y si tocaste endpoints terminá con el checklist
de Postman.
```

Rules that apply to every slice (from `CLAUDE.md` / `context/`):

- Docker must be running (pytest uses Testcontainers; `dotnet test` uses Testcontainers too).
- Verification commands: `uv run pytest -q`, `uv run ruff check .`, `uv run mypy src tests` (from `services/rag-api`); `dotnet build services\dotnet-api\AdvancedRag.sln --no-restore` and `dotnet test services\dotnet-api\AdvancedRag.sln` (from repo root).
- All code/comments/commits in English. Conventional commits. No real secrets.
- New stable error codes go to the catalog in `context/code-patterns.md`.
- When a slice changes `context/rag-spec.md` behavior, update that file in the same slice.
- After code changes run `graphify update .`.
- User-owned steps (Compose stack, manual acceptance) are marked in each plan; stop and ask the user to run them.
