# v2 Refactor Progress Tracker

Mirror of `docs/v2/03-phases.md` checklists but maintained as a journal. The MVP
`progress-tracker.md` is **frozen** as historical record; do not edit it.

## Current state at handoff

- Branch: `feature/v2-generic`
- Baseline commit on `main`: `chore: import existing MVP working tree as baseline`
- Operator: andresbr (this PC) → next PC handover pending
- Tests run: none (per user instruction)
- Dependencies installed: none

## Phase 0 — Preparation

- 2026-05-22 — `git init` on the working copy; baseline commit `c3bb5a5` on `main`.
- 2026-05-22 — Branch `feature/v2-generic` created.
- 2026-05-22 — `HANDOFF.md` authored.
- 2026-05-22 — `docs/v2/*` (6 files) authored.
- 2026-05-22 — `docs/adr/0001–0010` (10 ADRs) authored.
- 2026-05-22 — `context/v2-overview.md` and this `context/v2-progress.md` created.
- 2026-05-22 — Appended v2 entries to `context/design-decisions.md`.
- 2026-05-22 — **Commit `072943d`**: `docs: introduce v2 handoff documentation and ADRs`.

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
- **PENDING (next PC)**:
  - Install `react-i18next`, `i18next`, `i18next-browser-languagedetector` in each SPA.
  - Install `@anthropic-ai/sdk`, `cohere` Python deps in `services/rag-api/pyproject.toml` if/when those providers are exercised at runtime (the factory imports them lazily).
  - Apply Alembic migrations on first `uv run alembic upgrade head`.
  - Materialise the SQL scripts as EF Core migrations (`services/dotnet-api/v2-migrations-sql/README.md`).
  - Run `pnpm install` to wire `packages/shared-ui`.
  - Wire `conversation_memory.condense_question` and `query_rewrite.rewrite_query` into `chat_service` (Phase 5.2 / 5.3) — modules exist but are not yet called from the chat path.

## Phase 1.5 — Unified auth + design system base

- 2026-05-22 — `packages/shared-ui` scaffolded with tokens (light + dark), Inter font, hooks (`useTheme`, `useShortcut`), helper (`cn`), and components (`Button`, `AppShell`, `Header`, `Sidebar`, `DarkModeToggle`, `CommandPalette`).
  - `pnpm-workspace.yaml` updated to include `packages/*`.
  - **Commit `7de2164`**: `feat(shared-ui): design system scaffold with tokens + base components`.
- **PENDING (next PC)**:
  - Apply the unified auth changes (delete `chat-token` and `viewer-exchange-*` endpoints, simplify FastAPI cookie handling — see open question OQ-001 for the chosen validation strategy).
  - Author the remaining components in `packages/shared-ui/README.md`'s pending list.
  - Wire each SPA's `<App>` shell through `<AppShell>`/`<Sidebar>`/`<Header>`.

## Phase 1.7 — UX refactor per SPA

Pending. The shared-ui scaffold is in place; per-SPA refactor (3-pane chat, docs TOC, manage-web tables/editor polish) starts on the next PC after install.

## Phase 2 — Hybrid retrieval + dimensions

- 2026-05-22 — Hybrid retrieval SQL + reranker wrapper authored (`services/rag-api/src/advanced_rag/rag/{hybrid_retrieval,rerank,query_rewrite}.py`).
- 2026-05-22 — `hybrid_retrieve` + `rerank_candidates` wired into `chat_service.py`; `filters` exposed on `POST /api/chat` (`filters.dimensionValueIds`); cache key includes `filters_hash`.
- Dimension schema authored; CRUD endpoints + UI pending.

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
