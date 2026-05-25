# v2 Refactor Progress Tracker

Mirror of `docs/v2/03-phases.md` checklists, maintained as a journal. The MVP
`progress-tracker.md` is historical; do not use it as the active v2 tracker.

## Current State

- Current branch: `mvp-implementation`.
- `feature/v2-generic` has been merged into `mvp-implementation`.
- Merge commit at reconciliation time: `953c027 Merge branch 'feature/v2-generic' into mvp-implementation`.
- Working tree status at reconciliation start: clean (`git status --short` returned no output).
- This reconciliation did not run Playwright or the full verification suite. It reconciled
  docs against Git history, manifests, source files, migrations, and current file presence.

## Phase 0 - Preparation

- 2026-05-22 - `git init` on the working copy; baseline commit `c3bb5a5` on `main`.
- 2026-05-22 - Branch `feature/v2-generic` created.
- 2026-05-22 - `HANDOFF.md`, `docs/v2/*`, `docs/adr/0001-0010`,
  `context/v2-overview.md`, and this tracker were created.
- 2026-05-22 - Commit `f031d4a`: `docs: introduce v2 handoff documentation and ADRs`.
- 2026-05-25 - Local baseline verification was previously recorded as passing after
  Compose rebuilds, including the Playwright package run for first-run setup, management
  visual audit, and MVP happy path. This entry is historical evidence from that checkpoint;
  it was not re-run during the reconciliation.
- 2026-05-25 - Commit `953c027` merged `feature/v2-generic` into `mvp-implementation`.

## Phase 1 - Foundations Generic

- 2026-05-22 - Commit `e39481f`: raw v2 database migration scripts and Alembic migrations
  were introduced.
  - RAG Alembic migration files exist under `services/rag-api/alembic/versions/20260522_*`.
  - `.NET` raw SQL scripts exist under `services/dotnet-api/v2-migrations-sql/`.
  - Only `20260522150000_AddConfigurableDimensions.cs` is materialized as an EF Core v2
    migration; the other v2 `app` schema scripts remain raw SQL.
- 2026-05-22 - Commit `4665177`: provider abstraction files were added under
  `services/rag-api/src/advanced_rag/providers/`.
- 2026-05-22 - Commit `992476b`: hybrid retrieval, reranker wrapper, query rewrite,
  conversation memory, and multilingual prompt files were added.
- 2026-05-25 - Commit `f86bd28`: provider factory and hybrid retrieval were wired into
  FastAPI app composition, `ChatService`, and `InternalIndexingService`.
  - `ChatService.answer` consumes `IEmbeddingProvider`, `ILlmProvider`, optional
    `IRerankerProvider`, `filters`, `session_id`, and `locale`.
  - `InternalIndexingService` consumes `IEmbeddingProvider`.
  - Provider factory inputs still come from `Settings`; `app.tenant_config` is not read yet.
- 2026-05-25 - Current RAG embedding dimension defaults are 1024 in `Settings` and
  `infra/compose/.env.example`; `OPENAI_EMBEDDING_MODEL` remains `text-embedding-3-small`
  in Compose until `OQ-002` is resolved.
- Pending:
  - Materialize remaining v2 `.NET` SQL scripts as EF Core migrations.
  - Extend setup/config APIs to persist and read `app.tenant_config`.
  - Add reindex tooling and `docs/operations/reindex.md`.
  - Replace whitespace token counting in `chunking.py` with model-aware tokenization.
  - Add dedicated retry behavior tests for provider retry paths.
  - Wire `conversation_memory.condense_question` and `query_rewrite.rewrite_query` into
    the chat path when Phase 5 work starts.

## Phase 1.5 - Unified Auth + Shared UI

- 2026-05-22 - Commit `0ca016f`: `packages/shared-ui` was scaffolded.
- 2026-05-25 - Commit `b091647`: shared UI wiring and product surface consolidation were
  committed with:
  - Shared styles imported by all three SPAs.
  - `AppShell`, `DarkModeToggle`, and `LanguageSelect` used by manage/chat/docs.
  - `Sidebar` used by management.
  - Local workflow headers in chat/docs instead of a forced shared global header.
- Unified auth is not implemented:
  - `.NET` still exposes `POST /api/auth/chat-token`.
  - `.NET` still exposes `/api/viewer/exchange`.
  - FastAPI chat still reads `__Host-chat-token`.
  - Chat/docs frontend clients still call the MVP token/exchange routes.
- 2026-05-25 - `OQ-001` was resolved: FastAPI will validate `__Host-session` by calling
  an internal-only .NET session validation endpoint on cache miss, guarded by
  `X-Internal-Service-Token`, and cache safe claims in process for 60 seconds. This is a
  design decision only; implementation is still pending.
- Pending:
  - Implement the selected `OQ-001` FastAPI session validation strategy.
  - Remove or replace MVP chat-token and viewer exchange flows in backend, frontend, and tests.
  - Finish pending shared-ui primitives listed in `packages/shared-ui/README.md`.

## Phase 1.7 - UX Refactor Per SPA

- Current state is partial, not complete:
  - All three SPAs use shared shell controls and visible language/theme controls.
  - The target v2 UX items such as three-pane chat, docs TOC, shared DataTable, analytics
    dashboard, and dimensions editor are still pending.

## Phase 2 - Hybrid Retrieval + Dimensions

- Hybrid retrieval is wired into `ChatService.answer`.
- `filters.dimensionValueIds` is accepted by the FastAPI chat schema.
- `filters_hash` is stored in semantic cache and query audit paths.
- `20260522150000_AddConfigurableDimensions` materializes `app.dimensions`,
  `app.dimension_values`, and `app.document_dimension_values`, including `rag_owner` read
  grants.
- Pending:
  - EF Core domain entities and DbContext mappings for dimensions beyond raw SQL migration
    materialization.
  - Dimensions CRUD controller and management UI.
  - Retrieval-quality validation against a golden set.
  - Per-query rerank toggle behavior.
  - Chat-web deep-link filter chips.

## Phase 3 - Object Storage + Bulk Import

- `infra/compose/compose.v2-extras.yaml` exists with MinIO and optional TEI sidecars.
- `infra/compose/minio/init-bucket.sh` exists.
- Pending:
  - Application storage abstractions in .NET and FastAPI.
  - Import/editor integration.
  - Caddy storage route and signed URL behavior.
  - Bulk import endpoint and progress UI.

## Phase 4 - CdA Features

- Raw SQL scripts exist for document views, reactions, favorites, API keys, webhooks, and
  metrics.
- Pending:
  - EF migrations for those scripts.
  - Controllers, services, middleware, and UI.
  - Materialized metrics refresh strategy.

## Phase 5 - Quality

- RAGAS files exist: `evals/golden.jsonl`, `evals/ragas_runner.py`,
  `evals/baseline_metrics.json`, `evals/requirements.txt`, and `.github/workflows/eval.yml`.
- Observability Compose overlay exists.
- Citation-span and unresolved-question Alembic migrations exist.
- Pending:
  - OpenTelemetry SDK instrumentation in .NET and FastAPI.
  - Grafana dashboard JSON.
  - Runtime unresolved-question clustering job and dashboard.
  - End-to-end conversation memory/query rewrite wiring.
  - Runtime citation `text_quote` generation, validation, and viewer highlighting.

## Phase 6 - Ship Readiness

Pending.

## Open Questions

See `docs/v2/open-questions.md`. `OQ-001` is resolved; `OQ-002` through `OQ-010` remain
open.

## Next Recommended Work

Implement the `OQ-001` internal session validation path and then remove or replace the MVP
chat-token and viewer exchange-code flows before adding more v2 UI polish. The code
currently has shared UI and RAG provider/retrieval work, but browser auth still uses the
MVP multi-token design.
