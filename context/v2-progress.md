# v2 Refactor Progress Tracker

Mirror of `docs/v2/03-phases.md` checklists, maintained as a journal. The MVP
`progress-tracker.md` is historical; do not use it as the active v2 tracker.

## Current State

- Current branch: `mvp-implementation`.
- `feature/v2-generic` has been merged into `mvp-implementation`.
- Merge commit at reconciliation time: `953c027 Merge branch 'feature/v2-generic' into mvp-implementation`.
- Working tree status at the 2026-05-26 Phase 0 reconciliation start: broad uncommitted
  v2 changes across unified auth, shared-ui, SPAs, docs, context, Compose, and backend tests.
- This reconciliation is closing the dirty working tree before new v2 feature work. It has
  not run Playwright or the full verification suite yet.

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
- 2026-05-26 - Phase 0 reconciliation started from the dirty working tree produced by the
  unified-auth/shared-ui work rather than from a clean checkout. Active E2E tests, .NET
  API tests, and operational secrets documentation were updated away from browser
  chat-token and viewer-exchange runtime flows. Remaining token-flow references are
  historical notes, ADR context, or explicit removal statements.
- 2026-05-26 - Phase 0 focused reconciliation verification passed:
  - `pnpm.cmd --dir packages/shared-ui test -- --run` (`27 passed`)
  - `pnpm.cmd --dir packages/shared-ui typecheck`
  - `pnpm.cmd --dir apps/chat-web test -- --run App.test.tsx` (`12 passed`)
  - `pnpm.cmd --dir apps/docs-web test -- --run App.test.tsx` (`10 passed`)
  - `pnpm.cmd --dir apps/manage-web test -- --run App.test.tsx` (`32 passed`)
  - `pnpm.cmd --dir apps/manage-web typecheck`
  - `uv run pytest -q tests/test_chat_rag.py tests/test_feedback.py tests/test_rate_limit.py tests/test_session_validation.py` (`13 passed`)
  - `dotnet test services/dotnet-api/AdvancedRag.sln --filter "Auth|Viewer|Configuration"` (`18 matching tests passed`; existing `NU1900` warnings because NuGet vulnerability metadata could not be fetched)

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
- 2026-05-26 - Phase 1.1 tenant configuration completed:
  - Added EF migration `20260526120000_AddTenantConfig` for the singleton
    `app.tenant_config` row based on the raw v2 SQL script.
  - Added `.NET` application, EF repository, entity mapping, and API models for tenant
    configuration.
  - Added public-safe `GET /api/v1/config` and admin-only `PUT /api/v1/config`.
  - Setup now passes the default tenant config draft into the same transactional
    first-admin creation path; the EF repository inserts the singleton when missing.
  - Hardened the earlier dimensions migration so partial legacy upgrade tests that do
    not yet have `app.documents` do not fail while creating dimension tables.
  - Verified with focused TDD checks and the full `.NET` solution test suite.
- Pending:
  - Materialize remaining v2 `.NET` SQL scripts as EF Core migrations.
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
- 2026-05-25 - `OQ-001` was resolved: FastAPI will validate `__Host-session` by calling
  an internal-only .NET session validation endpoint on cache miss, guarded by
  `X-Internal-Service-Token`, and cache safe claims in process for 60 seconds.
- 2026-05-25 - OQ-001 implementation started:
  - `.NET` now sets the browser session cookie as `__Host-session`.
  - `.NET` exposes `GET /internal/session/validate`, guarded by
    `X-Internal-Service-Token`, returning safe RAG claims for the active session.
  - FastAPI has `DotnetSessionValidator` with a 60-second in-process cache keyed by
    SHA-256 of the raw session cookie value.
  - FastAPI chat and feedback read the configured `SESSION_COOKIE_NAME`
    (`__Host-session` in Compose) and call `app.state.session_validator`.
  - `chat-web` no longer calls `POST /api/auth/chat-token`; chat and feedback requests
    send the in-memory CSRF header and rely on the unified session cookie.
  - Compose wires `DOTNET_SESSION_VALIDATE_URL` and `SESSION_COOKIE_NAME` for `rag-api`.
- 2026-05-26 - Phase 1.5 unified auth runtime cleanup completed without Playwright:
  - Removed `.NET` `POST /api/auth/chat-token`, `ChatTokenIssuer`, and the
    `__Host-chat-token` browser cookie runtime path.
  - Removed `.NET` `/api/viewer/exchange`, `ViewerTokenService`, and the
    `__Host-viewer-token` runtime path.
  - Changed viewer links to `https://docs.<domain>/open?documentId=<id>` and
    `GET /api/viewer/document?documentId=<id>` now revalidates the authenticated
    `.NET` session and role before returning document content.
  - `apps/docs-web` opens document-id links with the unified session and no longer calls
    `/api/viewer/exchange`.
  - Added FastAPI local CSRF validation for `POST /api/chat` and
    `POST /api/feedback/{query_audit_event_id}` before session validation.
  - Corrected Compose wiring so `.NET` receives `Csrf__SigningKeyFile` and FastAPI
    receives `CSRF_SIGNING_KEY_FILE`.
  - Deprecated viewer exchange/audit tables were removed from the current EF model and
    initial app-schema migration, and the unused raw v2 drop script was retired.
- 2026-05-26 - FastAPI auth test seams were aligned with unified session validation:
  - `test_chat_rag.py`, `test_feedback.py`, and `test_rate_limit.py` now inject
    `session_validator` fakes instead of `chat_token_validator` fakes for browser
    chat/feedback paths.
  - `rg` no longer finds `chat_token_validator=` or `FakeChatTokenValidator` under
    `services/rag-api/tests`.
  - Verified with `uv run pytest -q tests/test_chat_rag.py tests/test_feedback.py
    tests/test_rate_limit.py` (`11 passed`), `uv run ruff check .`, and
    `uv run mypy src tests`.
- 2026-05-26 - Shared UI primitives were completed:
  - Added form primitives: `Input`, `Textarea`, `Select`, `Checkbox`, `RadioGroup`,
    and `Switch`.
  - Added overlay primitives: `Dialog`, `Drawer`, `HoverCard`, `Tooltip`, `Popover`,
    and `DropdownMenu`.
  - Added data, feedback, content, chat, and citation primitives: `DataTable`
    backed by TanStack Table, `Pagination`, `Badge`, `Avatar`,
    `ToastViewport`/`notify`, `Skeleton`, `EmptyState`, `Markdown`,
    `ChatMessage`, `ChatComposer`, `ConversationList`, `CitationCard`, and
    `CitationDrawer`.
  - Added colocated Vitest/Testing Library tests for all new primitives and a
    package-local Vitest setup.
  - Refreshed `pnpm-lock.yaml` for the ADR-0007-approved UI dependencies used by
    these primitives.
  - Verified with `pnpm.cmd --dir packages/shared-ui test` (`25 passed`) and
    `pnpm.cmd --dir packages/shared-ui typecheck`.
- 2026-05-26 - Phase 1.5.6 first SPA primitive adoption pass completed:
  - Extended `ChatComposer` with max-length, character-count, submit-label, and
    pending-label support, and localized `ChatMessage` author badges.
  - Applied shared primitives in `chat-web`: `ChatComposer`, `ChatMessage`,
    `CitationCard`, `EmptyState`, `Input`, `Textarea`, and shared `Button`.
  - Applied shared primitives in `docs-web`: `EmptyState`, `Input`, and shared `Button`
    for login, search, loading/error, and empty portal states.
  - Applied shared data primitives in `manage-web` by replacing local audit and
    feedback tables with `DataTable`, and the audit empty state with `EmptyState`.
  - Verified targeted red/green coverage with shared-ui, chat, docs, and management
    Vitest suites plus package/app typechecks.
- 2026-05-26 - Phase 1.5.6 management-heavy primitive adoption sub-batch completed:
  - Extended `DataTable` with an explicit `rowHeader` column option so migrated
    management tables preserve accessible row headers.
  - Migrated management users/groups tables and document tables to the shared
    `DataTable` primitive.
  - Migrated users/groups budget, user, and group dialogs to the shared `Dialog`
    primitive with action descriptions, while preserving footer-only cancel/save
    controls.
  - Migrated document editor text fields and access-group checkboxes to shared
    `Input` and `Checkbox` primitives, including `aria-invalid` coverage for
    review validation errors.
  - Verified with focused red/green tests, `pnpm.cmd --dir apps/manage-web test --
    --run App.test.tsx`, `pnpm.cmd --dir apps/manage-web typecheck`,
    `pnpm.cmd --dir apps/manage-web build`, `pnpm.cmd --dir packages/shared-ui test
    -- --run`, and `pnpm.cmd --dir packages/shared-ui typecheck`.
- 2026-05-27 - Phase 1.5.6 final shared primitive sweep completed:
  - Replaced the remaining local `components/ui/button` copies with
    `@helpcenter/shared-ui` `Button` in management setup/login, account, audit,
    configuration, documents, rich-text toolbar, feedback, and users/groups surfaces.
  - Removed the unused local button component copies from `manage-web`, `chat-web`, and
    `docs-web`.
  - Extended shared `Button` so icon buttons preserve the existing accessible
    `data-tooltip` convention without duplicating the browser `title` attribute.
  - Migrated remaining low-risk management text/search/date inputs to shared `Input`.
    Native file upload, raw HTML source textarea, and native selects remain intentionally
    custom until their UX refactors justify replacing browser semantics.
  - Added regression coverage for shared Button tooltip metadata and management shared UI
    adoption.
  - Verified with `pnpm.cmd --dir apps/manage-web test -- --run App.test.tsx
    sharedUiAdoption.test.ts`, `pnpm.cmd --dir apps/manage-web typecheck`,
    `pnpm.cmd --dir apps/manage-web build`, `pnpm.cmd --dir packages/shared-ui test
    -- --run`, `pnpm.cmd --dir packages/shared-ui typecheck`,
    `pnpm.cmd --dir apps/chat-web typecheck`, `pnpm.cmd --dir apps/chat-web build`,
    `pnpm.cmd --dir apps/docs-web typecheck`, `pnpm.cmd --dir apps/docs-web build`,
    and `git diff --check`.

## Phase 1.7 - UX Refactor Per SPA

- Current state is partial, not complete:
  - All three SPAs use shared shell controls and visible language/theme controls.
  - Phase 1.5.6 shared primitive adoption is complete enough to begin Phase 1.7.
  - The target v2 UX items such as three-pane chat, docs TOC, analytics
    dashboard, and dimensions editor are still pending.
- 2026-05-27 - Phase 1.7 login consistency pass completed:
  - Added shared `AuthShell` and `AuthCardHeader` primitives to `packages/shared-ui`
    using the management login frame as the source of truth.
  - Reused the shared auth frame in `manage-web`, `chat-web`, and `docs-web` so all
    login/loading/error auth surfaces share the same product panel, card structure,
    responsive layout, and dark-mode styling.
  - Removed the unused local `chat-auth-*` and `docs-auth-*` CSS rules to prevent
    future visual drift.
  - Added app regression coverage that asserts chat/docs login pages render the shared
    `auth-shell` and `auth-card` structure.
  - Verified with focused app/shared-ui tests, package/app typechecks, and builds for
    all three SPAs.
- 2026-05-27 - Phase 1.7 chat UX pass completed:
  - Converted `chat-web` from a single-column chat surface to a three-pane workspace
    with a local conversation rail, central question/answer panel, and right context
    rail for citations.
  - Wired shared `ConversationList`, `CitationDrawer`, and `CommandPalette` primitives.
  - Added the shared `ChatMessage` pending cursor and rendered it in `chat-web` while
    a response is being prepared.
  - Conversation history remains local to the browser session; no persistence or new API
    contract was introduced.
  - Dimension filter chips remain pending until the dimensions API/UI work is available.
  - Verified the cursor addition with
    `pnpm.cmd --dir packages/shared-ui test -- --run ChatMessage.test.tsx`,
    `pnpm.cmd --dir packages/shared-ui typecheck`,
    `pnpm.cmd --dir apps\chat-web test -- --run App.test.tsx`, and
    `pnpm.cmd --dir apps\chat-web typecheck`, and
    `pnpm.cmd --dir apps\chat-web build`.

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

Use `docs/superpowers/plans/2026-05-26-v2-closure-plan.md` as the active closure plan.
After the Phase 0 reconciliation commit, continue with Phase 1 foundations, starting
with tenant configuration and the remaining v2 foundation gaps.
