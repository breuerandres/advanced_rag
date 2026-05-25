# v2 Refactor Progress Tracker

Mirror of `docs/v2/03-phases.md` checklists but maintained as a journal. The MVP
`progress-tracker.md` is **frozen** as historical record; do not edit it.

## Current state at handoff

- Branch: `feature/v2-generic`
- Baseline commit on `main`: `chore: import existing MVP working tree as baseline`
- Operator: andresbr (this PC) → next PC handover pending
- Tests run: local baseline verification now executed on 2026-05-25 (see Phase 0 and Phase 2 notes).
- Dependencies installed: frontend/Python/.NET dependencies are present on this PC.

## Phase 0 — Preparation

- 2026-05-22 — `git init` on the working copy; baseline commit `c3bb5a5` on `main`.
- 2026-05-22 — Branch `feature/v2-generic` created.
- 2026-05-22 — `HANDOFF.md` authored.
- 2026-05-22 — `docs/v2/*` (6 files) authored.
- 2026-05-22 — `docs/adr/0001–0010` (10 ADRs) authored.
- 2026-05-22 — `context/v2-overview.md` and this `context/v2-progress.md` created.
- 2026-05-22 — Appended v2 entries to `context/design-decisions.md`.
- 2026-05-22 — **Commit `072943d`**: `docs: introduce v2 handoff documentation and ADRs`.
- 2026-05-25 — Local baseline verification completed after Compose rebuilds:
  - `pnpm.cmd --dir tests\e2e test` passed: first-run setup, management visual audit, and MVP happy path (3/3).
  - Prior verification in this checkpoint also passed the three SPA typechecks/tests/builds, `packages/shared-ui` typecheck, FastAPI `pytest`/`ruff`/`mypy`, `.NET` solution tests, Compose config, and `git diff --check`.
  - E2E compatibility fixes aligned tests with the current login-first chat/docs surfaces, current Spanish headings, and one-time viewer exchange-code behavior.

## Phase 1 — Foundations generic

- 2026-05-22 — Schema migrations authored (NOT applied):
  - 5 Alembic versions: embedding 1024d, BM25 columns, audit session/filters/rerank fields, citation span, unresolved_questions.
  - 13 SQL up/down scripts in `services/dotnet-api/v2-migrations-sql/` for the `app` schema (tenant_config, role column, dimensions, document_views/reactions/favorites, api_keys, webhooks, doc language/summary/external_key, doc_versions markdown, drop viewer_exchange_codes, mv_document_metrics, rag_owner grants).
  - **Commit `b8ca5ed`**: `feat(db): v2 schema migrations (Alembic + EF SQL scripts)`.
- 2026-05-22 — Provider abstraction package authored:
  - `services/rag-api/src/advanced_rag/providers/{base,_retry,factory,openai_provider,anthropic_provider,azure_openai_provider,ollama_provider,tei_embedding_provider,tei_reranker_provider,cohere_reranker}.py` plus `__init__.py`.
  - **Commit `257d620`**: `feat(providers): multi-provider LLM/embedding/reranker abstraction`.
- 2026-05-22 — Multi-language prompts authored:
  - `system_{es-AR,en-US,pt-BR}.md`, `condenser_*`, `rewriter_*` (9 prompt files).
  - **Commit `7a64551`**: `feat(rag): hybrid retrieval, reranker wrapper, multi-turn memory, multilingual prompts`.
- 2026-05-22 — i18n scaffolds in all 3 SPAs (`apps/{chat,manage,docs}-web/src/i18n/`).
  - **Commit `41810a5`**: `feat(infra): i18n scaffolds, MinIO + TEI overlay, OTel observability, RAGAS evals`.
- 2026-05-22 — `chat_service.py` + `indexing_service.py` rewired to consume the new providers + hybrid retrieval + (optional) reranker:
  - New module `rag/answer_generator.py` bridges `ILlmProvider.chat_complete` to the domain `{answer, cited_chunk_ids}` JSON contract, loading the system prompt from `prompts/system_<locale>.md`.
  - `chat_service.ChatService.__init__` now takes `IEmbeddingProvider`, `ILlmProvider`, and an optional `IRerankerProvider`; `answer()` accepts `filters`, `session_id`, and `locale` parameters and threads them through hybrid retrieval, cache partitioning (`filters_hash`), and audit (`session_id`, `filters`, `vector_top_k`, `bm25_top_k`, `rerank_top_k`, `reranker_model`, `reranker_score`).
  - `indexing_service.InternalIndexingService` no longer threads model/dimensions per call; it reads them from the injected provider's intrinsic attributes.
  - `main.create_app` builds a `ProviderFactory` from `Settings`; tests can still pass explicit fakes for each protocol. `Settings` extends with `llm_*`, `embedding_*`, `reranker_*`, `azure_*`, and hybrid-retrieval knobs.
  - `OpenAILlmProvider` / `OpenAIEmbeddingProvider` defer `AsyncOpenAI` client construction so an app can be created without an API key (tests and CI rely on this).
  - `schemas/chat.ChatRequest` extended with `filters.dimensionValueIds`, `sessionId`, and `locale`; the chat router passes them to `ChatService.answer`.
  - Legacy `rag/embeddings.py` and `rag/chat_completion.py` removed; tests rewritten with `FakeLlmProvider` / `FakeEmbeddingProvider` against the new `ILlmProvider` / `IEmbeddingProvider` protocols.
  - Test infrastructure: `_bootstrap` in `test_chat_rag.py`, `test_indexing.py`, and `test_migrations.py` now installs `pg_trgm` and `unaccent` extensions so Alembic `upgrade head` succeeds (BM25 migration requires them). 1024-dim seeds throughout.
  - `pyproject.toml` adds `tenacity==9.1.2` (required by `providers/_retry.py`).
- 2026-05-22 — Refreshed `services/rag-api/uv.lock` so the `tenacity==9.1.2` dependency added during the provider abstraction work is locked. Verified the original Docker failure path with `uv lock --check`, `uv sync --locked --no-dev --no-install-project`, and `docker compose --env-file infra/compose/.env.example -f infra/compose/compose.yaml -f infra/compose/compose.override.yaml build rag-api`.
- 2026-05-22 — Installed the missing i18n runtime dependencies (`i18next`, `react-i18next`, and `i18next-browser-languagedetector`) in `apps/manage-web`, `apps/chat-web`, and `apps/docs-web`, then refreshed `pnpm-lock.yaml`. Verified the original `docs-web` Docker failure path with `docker compose --env-file infra/compose/.env.example -f infra/compose/compose.yaml -f infra/compose/compose.override.yaml build docs-web`, plus `pnpm --dir apps/docs-web typecheck`, `pnpm --dir apps/docs-web build`, `pnpm --dir apps/chat-web typecheck`, `pnpm --dir apps/chat-web build`, `pnpm --dir apps/manage-web typecheck`, and `pnpm --dir apps/manage-web build`.
- 2026-05-22 — Fixed the `20260522_120000` Alembic migration so existing MVP chunks can be preserved as inactive historical rows while their invalid 1536-dimensional embeddings are dropped during the move to `vector(1024)`. Added a regression test that seeds an existing chunk, cache entry, and query-audit citation before upgrading to head. Also fixed the provider streaming protocol type annotation and a stale chat test seed argument so the full FastAPI suite passes.
- 2026-05-22 — Fixed full Compose startup for the BM25 migration by installing `pg_trgm` and `unaccent` from `postgres-init` and by making the migration's `public.unaccent` wrapper use an explicit `regdictionary` cast. Verified `docker compose --env-file infra/compose/.env.example -f infra/compose/compose.yaml -f infra/compose/compose.override.yaml up -d --build --force-recreate`; all services reached healthy/running state.
- 2026-05-25 — Aligned local Compose RAG embedding dimensions with the v2 `vector(1024)` schema by setting `OPENAI_EMBEDDING_DIMENSIONS=1024` in `infra/compose/.env.example`. `OPENAI_EMBEDDING_MODEL` remains `text-embedding-3-small` until OQ-002 is resolved.
- 2026-05-25 — Added materialized EF migration `20260522150000_AddConfigurableDimensions` for `app.dimensions`, `app.dimension_values`, and `app.document_dimension_values`, with `rag_owner` read grants. This unblocked FastAPI hybrid retrieval queries that already join `app.document_dimension_values`.
- **PENDING (next PC)**:
  - Install `@anthropic-ai/sdk`, `cohere` Python deps in `services/rag-api/pyproject.toml` if/when those providers are exercised at runtime (the factory imports them lazily).
  - Apply Alembic migrations on first `uv run alembic upgrade head`.
  - Materialise the remaining SQL scripts as EF Core migrations (`services/dotnet-api/v2-migrations-sql/README.md`); configurable dimensions are now materialized.
  - Wire `conversation_memory.condense_question` and `query_rewrite.rewrite_query` into `chat_service` (Phase 5.2 / 5.3) — modules exist but are not yet called from the chat path.

## Phase 1.5 — Unified auth + design system base

- 2026-05-22 — `packages/shared-ui` scaffolded with tokens (light + dark), Inter font, hooks (`useTheme`, `useShortcut`), helper (`cn`), and components (`Button`, `AppShell`, `Header`, `Sidebar`, `DarkModeToggle`, `CommandPalette`).
  - `pnpm-workspace.yaml` updated to include `packages/*`.
  - **Commit `7de2164`**: `feat(shared-ui): design system scaffold with tokens + base components`.
- 2026-05-22 — Wired `packages/shared-ui` into `apps/manage-web`, `apps/chat-web`, and `apps/docs-web`.
  - Each SPA now declares `@helpcenter/shared-ui`, imports shared Inter/tokens/globals CSS, enables the Tailwind CSS v4 Vite plugin, and scans `packages/shared-ui/src` with `@source`.
  - Existing product workflows remain intact, but each app now uses shared `AppShell`, `Header`, `Sidebar`, and a visible `DarkModeToggle`.
  - Frontend Dockerfiles now copy `packages/shared-ui` before frozen installs/builds so Compose image builds work outside the local workspace.
  - React type packages were aligned to React 18 across the SPAs to avoid duplicate React type trees when compiling workspace package source.
  - `useTheme` now tolerates disabled/missing storage and media APIs.
  - Verified with `pnpm --dir packages/shared-ui typecheck`, all three SPA typechecks, all three SPA Vitest suites, all three SPA builds, all three SPA lints, `docker compose ... build manage-web chat-web docs-web`, `docker compose ... up -d --build --force-recreate manage-web chat-web docs-web`, `docker compose ... ps`, and HTTPS 200 checks for the three local hosts.
- **PENDING (next PC)**:
  - Apply the unified auth changes (delete `chat-token` and `viewer-exchange-*` endpoints, simplify FastAPI cookie handling — see open question OQ-001 for the chosen validation strategy).
  - Author the remaining components in `packages/shared-ui/README.md`'s pending list.

## Phase 1.7 — UX refactor per SPA

Pending. The shared-ui scaffold is in place; per-SPA refactor (3-pane chat, docs TOC, manage-web tables/editor polish) starts on the next PC after install.

## Phase 2 — Hybrid retrieval + dimensions

- 2026-05-22 — Hybrid retrieval SQL + reranker wrapper authored (`services/rag-api/src/advanced_rag/rag/{hybrid_retrieval,rerank,query_rewrite}.py`).
- 2026-05-22 — `hybrid_retrieve` + `rerank_candidates` wired into `chat_service.py`; `filters` exposed on `POST /api/chat` (`filters.dimensionValueIds`); cache key includes `filters_hash`.
- 2026-05-25 — Dimension schema is now materialized in an EF Core migration and verified through `AppDbContextMigrationTests`; CRUD endpoints + UI remain pending.

## Phase 3 — Object storage + bulk import

- 2026-05-22 — `compose.v2-extras.yaml` adds MinIO + optional TEI sidecars; `infra/compose/minio/init-bucket.sh` bootstraps the bucket with `tenant/brand/*` public-read.
- **PENDING (next PC)**: author `IObjectStorage` in .NET and `storage/minio_storage.py` in FastAPI; integrate with the importer and editor.

## Phase 4 — CdA features

- 2026-05-22 — Schema authored (`document_views`, `document_reactions`, `document_favorites`, `api_keys`, `webhooks`, `mv_document_metrics`).
- **PENDING (next PC)**: controllers + UI for each.

## Phase 5 — Quality

- 2026-05-22 — RAGAS evals scaffolded (`evals/`), GitHub Actions workflow at `.github/workflows/eval.yml`.
- 2026-05-22 — Conversational memory + query rewrite modules authored; need wiring into `chat_service.py`.
- 2026-05-22 — Observability overlay (`compose.observability.yaml`) ready with OTel collector + Tempo + Prometheus + Loki + Grafana.
- **PENDING (next PC)**: instrument .NET + FastAPI with OpenTelemetry SDKs; author Grafana dashboards.

## Phase 6 — Ship readiness

Pending.

## Summary of work shipped in this session

Seven commits on `feature/v2-generic` (plus `main` baseline `c3bb5a5`):

| Commit | Title |
|---|---|
| 072943d | docs: introduce v2 handoff documentation and ADRs |
| b8ca5ed | feat(db): v2 schema migrations (Alembic + EF SQL scripts) |
| 257d620 | feat(providers): multi-provider LLM/embedding/reranker abstraction |
| 7a64551 | feat(rag): hybrid retrieval, reranker wrapper, multi-turn memory, multilingual prompts |
| 7de2164 | feat(shared-ui): design system scaffold with tokens + base components |
| 41810a5 | feat(infra): i18n scaffolds, MinIO + TEI overlay, OTel observability, RAGAS evals |

Roughly 90 new files. No dependencies were installed and no tests were executed in this
session (per user instruction). The next PC must install + verify + then continue the
remaining "PENDING" items in each phase above.

## Open questions

See `docs/v2/open-questions.md`. As of handoff, OQ-001 through OQ-010 are all open.

## Notes for the next operator

- After bringing the MVP baseline up on the new PC, run the full existing test suite
  before touching v2 work. Mark which tests are red because of v2 partial scaffolds vs
  pre-existing issues.
- Phase order in `docs/v2/03-phases.md` is recommended but not strictly required;
  internal dependencies are noted per task. Some Phase 1 and Phase 1.5 tasks can be done
  in parallel.
- When a phase task completes, add a journal entry to this file under the matching
  section with the date and the commit hash.
- When an open question is resolved, move its entry from `docs/v2/open-questions.md` to
  the `## Resolved` section there with the chosen option and date.
