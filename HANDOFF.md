# Handoff - v2 Generic Refactor

This document is the entry point for continuing the v2 generic refactor of the Advanced
RAG MVP. It was updated on 2026-05-26 after the Phase 1.5 unified-auth test seam cleanup.
Playwright was intentionally not run in that session per user instruction.

## Read Order

1. `docs/v2/README.md`
2. `docs/v2/03-phases.md`
3. `context/v2-progress.md`
4. `docs/v2/open-questions.md`
5. The relevant ADR under `docs/adr/`
6. The MVP context files in `context/` for areas not superseded by v2 docs

## Current Git State

- Current branch: `mvp-implementation`.
- `feature/v2-generic` has been merged into `mvp-implementation`.
- Current merge commit at reconciliation time: `953c027 Merge branch 'feature/v2-generic' into mvp-implementation`.
- The working tree now contains the Phase 1.5 unified-auth cleanup, FastAPI auth test seam
  cleanup, and context updates. Review `git status --short` and the diff before
  continuing.
- MVP progress remains historical in `context/progress-tracker.md`. v2 progress is tracked
  in `context/v2-progress.md`.

## Current Implementation Inventory

The statuses below are based on files and code paths present in the repo.

| Area | Current status | Evidence |
|---|---|---|
| v2 docs and ADRs | Present | `docs/v2/`, `docs/adr/`, `context/v2-overview.md`, `context/v2-progress.md` |
| RAG Alembic migrations | Present as migration files | `services/rag-api/alembic/versions/20260522_*` |
| .NET v2 app-schema migrations | Only configurable dimensions are materialized as EF Core migration; the rest remain raw SQL scripts | `services/dotnet-api/src/AdvancedRag.Infrastructure/Migrations/20260522150000_AddConfigurableDimensions.cs`, `services/dotnet-api/v2-migrations-sql/` |
| Provider abstraction | Wired into FastAPI app composition, chat, and indexing; source is still `Settings`, not `app.tenant_config` | `services/rag-api/src/advanced_rag/main.py`, `providers/`, `rag/chat_service.py`, `rag/indexing_service.py` |
| Hybrid retrieval | Wired into `ChatService.answer`; cache/audit use `filters_hash` | `services/rag-api/src/advanced_rag/rag/chat_service.py`, `hybrid_retrieval.py`, `rerank.py` |
| Conversation memory and query rewrite | Modules exist but are not called by `ChatService.answer` | `rag/conversation_memory.py`, `rag/query_rewrite.py` |
| Multilingual prompts | System, condenser, and rewriter prompts exist for `es-AR`, `en-US`, and `pt-BR`; answer generation loads `system_<locale>.md` | `services/rag-api/src/advanced_rag/rag/prompts/`, `rag/answer_generator.py` |
| Token counting | Still uses whitespace splitting, not `tiktoken` | `services/rag-api/src/advanced_rag/rag/chunking.py` |
| Retry wrapper | Implemented and imported by provider implementations; no dedicated retry tests were found | `services/rag-api/src/advanced_rag/providers/_retry.py`, `providers/*.py`, `services/rag-api/tests/` |
| Unified auth | Runtime cleanup complete for Phase 1.5: `.NET` sets `__Host-session`, exposes internal session validation, FastAPI chat/feedback read that session and validate CSRF locally, `chat-web` no longer renews chat tokens, and docs viewer links use session-authenticated `documentId` locators. Legacy chat-token and viewer exchange browser endpoints are removed. FastAPI browser-path tests now inject `session_validator` fakes instead of `chat_token_validator` fakes. | `Program.cs`, `InternalSessionController.cs`, `session_validation.py`, `api/routers/chat.py`, `core/csrf.py`, `services/rag-api/tests/test_chat_rag.py`, `services/rag-api/tests/test_feedback.py`, `services/rag-api/tests/test_rate_limit.py`, `apps/chat-web/src/api/chat.ts`, `AuthController.cs`, `ViewerController.cs`, `apps/docs-web/src/api/viewer.ts` |
| Shared UI | Package exists with tokens, fonts, globals, hooks, layout controls, form primitives, overlay primitives, TanStack-backed data primitives, feedback primitives, markdown, and chat/citation primitives. Colocated tests cover the new primitives. | `packages/shared-ui/` |
| Shared UI usage | All three SPAs import shared styles and use shared shell controls; management uses `Sidebar`; chat/docs do not use the shared global `Header` | `apps/*/src/main.tsx`, `apps/*/src/App.tsx` |
| i18n | Runtime dependencies and locale catalogs exist in all three SPAs; full literal extraction is incomplete | `apps/*/package.json`, `apps/*/src/i18n/`, `apps/*/src/App.tsx` |
| MinIO and TEI | Compose overlay and MinIO bucket script exist; application storage integration is not implemented | `infra/compose/compose.v2-extras.yaml`, `infra/compose/minio/init-bucket.sh` |
| Dimensions | EF migration and FastAPI read-grant support exist; CRUD endpoints and UI are pending | `AddConfigurableDimensions.cs`; no `DimensionsController.cs` in API controllers |
| CdA views/reactions/favorites | Raw SQL scripts only; no controllers/UI | `services/dotnet-api/v2-migrations-sql/004_*`, `005_*`, `006_*` |
| API keys | Raw SQL script only; no controller/middleware/UI | `services/dotnet-api/v2-migrations-sql/007_*` |
| Webhooks | Raw SQL script only; no controller/worker | `services/dotnet-api/v2-migrations-sql/008_*` |
| Setup wizard | MVP first-run setup exists; it does not write v2 `tenant_config` | `SetupController.cs`, `SetupService.cs`, `v2-migrations-sql/001_*` |
| RAGAS evals | Golden set, runner, requirements, baseline metrics, and GitHub Actions workflow exist | `evals/`, `.github/workflows/eval.yml` |
| OpenTelemetry | Compose overlay exists; .NET/FastAPI SDK instrumentation is not implemented | `infra/compose/compose.observability.yaml`, `infra/compose/observability/` |

## Phase Snapshot

| Phase | Current status |
|---|---|
| Phase 0 - Preparation | Done and merged into `mvp-implementation` |
| Phase 1 - Foundations generic | Partial: provider wiring, prompts, i18n scaffolds, retry wrapper, and 1024-d RAG schema exist; tenant config endpoints, reindex tooling, token-counting refactor, and branding setup are pending |
| Phase 1.5 - Unified auth + shared UI | Partial: browser token flows are removed, FastAPI CSRF validation is implemented, FastAPI browser-path tests use the unified `session_validator` seam, deprecated viewer exchange tables were removed from the current EF model/migration, and shared-ui primitives are implemented. Remaining cleanup is applying primitives to SPAs. |
| Phase 1.7 - UX refactor per SPA | Partial: current apps use some shared shell controls; target v2 chat/docs/manage UX remains pending |
| Phase 2 - Hybrid retrieval + dimensions | Partial: hybrid retrieval is wired; dimensions CRUD/UI and retrieval quality validation are pending |
| Phase 3 - Object storage + bulk import | Scaffolded: Compose overlay exists; app integration is pending |
| Phase 4 - CdA features | Raw SQL only; controllers/UI/middleware are pending |
| Phase 5 - Quality | Partial: RAGAS and OTel overlay exist; memory/rewrite/citation span behavior is not wired end-to-end |
| Phase 6 - Ship readiness | Pending |

## Recommended Next Work

Continue Phase 1.5.6 shared-ui application work, then Phase 1.7 UI polish.

Do this next:

1. Apply the completed `packages/shared-ui` data/overlay/form primitives to the SPAs where
   existing local components duplicate them.

Do not run Playwright unless the user explicitly asks for browser/E2E verification.

## Verification From 2026-05-26

- `uv run pytest -q tests/test_chat_rag.py tests/test_feedback.py tests/test_rate_limit.py` (`11 passed`)
- `uv run ruff check .`
- `uv run mypy src tests`
- `uv run pytest -q` (`39 passed`)
- `uv run ruff check .`
- `uv run mypy src tests`
- `dotnet test services\dotnet-api\tests\AdvancedRag.App.Tests\AdvancedRag.App.Tests.csproj --filter ViewerAccessServiceTests` (`4 passed`)
- `dotnet test services\dotnet-api\tests\AdvancedRag.Api.Tests\AdvancedRag.Api.Tests.csproj --filter "AuthEndpointTests|ViewerEndpointTests|RateLimitEndpointTests"` (`14 passed`)
- `pnpm.cmd --dir packages/shared-ui test` (`25 passed`)
- `pnpm.cmd --dir packages/shared-ui typecheck`
- `dotnet build services\dotnet-api\AdvancedRag.sln --no-restore`
- `pnpm.cmd --dir apps\chat-web test -- --run App.test.tsx`
- `pnpm.cmd --dir apps\manage-web test -- --run App.test.tsx`
- `pnpm.cmd --dir apps\docs-web test -- --run App.test.tsx`
- `pnpm.cmd --dir apps\chat-web typecheck`
- `pnpm.cmd --dir apps\manage-web typecheck`
- `pnpm.cmd --dir apps\docs-web typecheck`
- `pnpm.cmd --dir apps\docs-web build`
- `docker compose --env-file infra/compose/.env.example -f infra/compose/compose.yaml config`

Known verification notes:

- Playwright was not run by request.
- .NET commands still show existing `NU1900` vulnerability metadata warnings when NuGet
  vulnerability data cannot be fetched.
- Vitest still prints the existing `--localstorage-file` warning in frontend runs.

## Open Questions

See `docs/v2/open-questions.md`. `OQ-001` is resolved; `OQ-002` through `OQ-010` remain
open.

## Precision Rule

When updating docs:

- Mark an item done only when the matching code, migration, configuration, or test exists
  in the repository.
- Mark an item partial when files exist but the runtime path is not fully wired.
- Keep product-intent ADRs separate from implementation status.
- Do not document planned behavior as implemented behavior.
