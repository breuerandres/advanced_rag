# V2 Closure Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reconcile the real v2 implementation state, close remaining architectural gaps, and finish the product toward a verifiable release candidate.

**Architecture:** Treat `context/v2-progress.md` and `docs/v2/03-phases.md` as the active v2 control plane, but verify every checklist item against source files, tests, migrations, and runtime behavior before marking it complete. Close work in small commits, starting with repository reconciliation before adding new product features.

**Tech Stack:** .NET 8, FastAPI with uv/Alembic, PostgreSQL/pgvector, React 18 TypeScript, Vite, pnpm, shared-ui workspace package, Docker Compose, Caddy, Playwright.

---

## Current Relevance

This plan supersedes the chat-only v2 closure notes from 2026-05-26. It does not replace the MVP plan at `docs/superpowers/plans/2026-05-11-mvp-implementation-plan.md`; that file remains historical for MVP execution. Active v2 progress should be tracked here, in `context/v2-progress.md`, and in `docs/v2/03-phases.md`.

## Human-In-The-Loop Split

### Agent-Owned

- Reconcile checklist items against current source code.
- Edit code, tests, migrations, docs, and context files.
- Run repository-safe verification commands.
- Keep commits scoped to one phase or coherent sub-phase.
- Update `context/v2-progress.md` after each completed checkpoint.
- Update `context/design-decisions.md` when decisions change architecture, security, workflow, data, deployment, or product behavior.

### User-Owned

- Keep Docker Desktop running for Compose, Testcontainers, and Playwright checkpoints.
- Decide open questions in `docs/v2/open-questions.md` before blocked implementation starts.
- Create or update local secret values only when required; the agent must not create real secrets.
- Approve destructive local environment actions, especially Compose volume removal for clean first-run tests.

## Phase 0: Reconciliation Checkpoint

**Goal:** Make the repository state coherent before adding new v2 functionality.

**Known reason:** The working tree currently contains broad uncommitted v2 changes, including shared-ui additions, unified-auth cleanup, tests, docs, context updates, and deleted legacy token-flow files. Some checklists are stale, while some E2E/docs still reference removed `chat-token` and viewer-exchange flows.

**Files:**
- Modify: `docs/v2/03-phases.md`
- Modify: `context/v2-progress.md`
- Modify: `tests/e2e/specs/*.spec.ts`
- Modify: `infra/compose/secrets/README.md`
- Modify as needed: context files containing obsolete runtime guidance

- [x] **Step 1: Inventory the dirty working tree**

Run:

```powershell
git status --short
git diff --name-status
git diff --cached --name-status
```

Expected: broad v2 changes are visible and can be grouped by topic. Do not revert user changes.

- [x] **Step 2: Remove obsolete browser token-flow references from active tests and operational docs**

Search:

```powershell
rg -n "auth/chat-token|viewer_exchange_codes|viewer_token_audit|viewer/exchange|__Host-chat-token|__Host-viewer-token" tests docs context infra services apps -g "*.ts" -g "*.md" -g "*.cs" -g "*.py" -g "*.yaml"
```

Expected before cleanup: matches remain in E2E specs, historical docs, and legacy notes. Active runtime tests and operational docs must be updated to the unified-session model. Historical MVP plan/spec references may remain if clearly historical.

- [x] **Step 3: Reconcile v2 checklists against source evidence**

Update `docs/v2/03-phases.md` and `context/v2-progress.md` so these items are marked accurately:

- Unified auth runtime cleanup: complete except any remaining obsolete tests/docs.
- Shared-ui primitives: complete but uncommitted until verified.
- Shared-ui SPA adoption: partial; remaining local duplicated controls and shell refactors stay pending.
- Dimensions schema: EF migration exists; entities, mappings, API, and UI remain pending.
- Hybrid retrieval: wired; golden-set validation and chat filter chips remain pending.
- Observability and RAGAS: scaffolds exist; runtime instrumentation and verified CI remain pending.

- [x] **Step 4: Fix visible encoding regressions in touched UI strings**

Search:

```powershell
rg -n "Ã|Â|�" apps context docs packages services tests -g "*.tsx" -g "*.ts" -g "*.md" -g "*.cs" -g "*.py"
```

Expected: active product UI strings do not contain mojibake. Historical quoted artifacts may be reviewed case by case.

- [x] **Step 5: Run focused reconciliation verification**

Run:

```powershell
pnpm.cmd --dir packages/shared-ui test -- --run
pnpm.cmd --dir packages/shared-ui typecheck
pnpm.cmd --dir apps/chat-web test -- --run App.test.tsx
pnpm.cmd --dir apps/docs-web test -- --run App.test.tsx
pnpm.cmd --dir apps/manage-web test -- --run App.test.tsx
pnpm.cmd --dir apps/manage-web typecheck
Set-Location services/rag-api; uv run pytest -q tests/test_chat_rag.py tests/test_feedback.py tests/test_rate_limit.py tests/test_session_validation.py; Set-Location ..\..
dotnet test services/dotnet-api/AdvancedRag.sln --filter "Auth|Viewer|Configuration"
```

Expected: focused tests pass or failures are documented in `context/v2-progress.md` with exact next actions.

- [x] **Step 6: Commit reconciliation**

Run:

```powershell
git add docs/v2 context tests infra apps packages services pnpm-lock.yaml
git commit -m "chore: reconcile v2 implementation status"
```

Expected: commit succeeds with only reconciliation/auth/shared-ui/status cleanup, not new feature work.

## Phase 1: Close V2 Foundations

**Goal:** Finish the configuration, provider, role, locale, token-counting, and audit foundations that other phases depend on.

**User-owned decisions required before or during this phase:**

- Resolve `OQ-002` embedding model default.
- Resolve `OQ-004` reindex-on-embedding-change strategy.
- Resolve `OQ-007` i18n locale strategy.
- Resolve `OQ-008` Anthropic provider initial model.

### Task 1.1: Tenant Config

**Files:**
- Modify/Create: `.NET` EF migration for `app.tenant_config`
- Modify/Create: `.NET` tenant config domain/application/infrastructure/API files
- Modify: setup service so first-run setup writes tenant config in the same transaction
- Modify: `apps/manage-web` configuration API/UI as needed
- Modify: `context/v2-progress.md`

- [ ] Add EF migration for `app.tenant_config` from `services/dotnet-api/v2-migrations-sql/001_add_tenant_config.up.sql`.
- [ ] Add entity and DbContext mapping.
- [ ] Add public-safe `GET /api/v1/config`.
- [ ] Add admin-only `PUT /api/v1/config`.
- [ ] Ensure setup wizard creates the initial singleton config row.
- [ ] Add tests proving secrets are never returned.
- [ ] Commit with `feat: add tenant configuration api`.

### Task 1.2: Provider Factory Reads Tenant Config

**Files:**
- Modify: `services/rag-api/src/advanced_rag/providers/factory.py`
- Modify/Create: FastAPI config loading module for tenant config
- Modify: FastAPI app composition
- Test: provider factory and chat/indexing tests

- [ ] Add a DB-backed tenant config reader for provider settings.
- [ ] Keep environment fallback for local/dev startup before DB is reachable.
- [ ] Promote optional provider dependencies only for providers enabled in shipped config.
- [ ] Add tests for OpenAI default, TEI embedding, disabled reranker, and invalid provider config.
- [ ] Commit with `feat: load rag providers from tenant config`.

### Task 1.3: Roles V2 Migration

**Files:**
- Modify/Create: `.NET` EF migration for `app.users.role`
- Modify: auth/session models and authorization policies
- Modify: user management UI/API role handling
- Modify: FastAPI session claim role handling

- [ ] Decide compatibility window for `roles` and `user_roles`.
- [ ] Add migration that backfills `users.role` from current `user_roles`.
- [ ] Update .NET authorization to v2 role names: `admin`, `editor`, `viewer`.
- [ ] Add FastAPI role dependency for chat/feedback where needed.
- [ ] Record `role_at_request` in RAG audit.
- [ ] Commit with `feat: migrate to v2 role model`.

### Task 1.4: Locale, Prompt Audit, Token Counting, Retry Tests

**Files:**
- Modify: frontend i18n catalogs and TSX literals
- Modify: `services/rag-api/src/advanced_rag/rag/chunking.py`
- Modify: RAG audit migration/code for `prompt_locale`
- Test: known token fixtures, retry behavior, translated output

- [ ] Extract active TSX literals into i18n catalogs where user-visible.
- [ ] Update frontend tests to assert translated output intentionally.
- [ ] Store `prompt_locale` in audit rows.
- [ ] Replace whitespace chunk token counting with model-aware tokenization.
- [ ] Add retry tests for 429, 503, timeout, and connection errors.
- [ ] Commit with `feat: close locale token and retry foundations`.

## Phase 2: Product UX V2

**Goal:** Bring all three SPAs to the target v2 UX using shared-ui primitives.

### Task 2.1: Final Shared-UI Adoption Sweep

- [x] Audit remaining low-risk local duplicated inputs, selects, textareas, empty/loading states, overlays, markdown rendering, chat, and citation surfaces; replace the safe cases and defer native controls whose browser semantics belong to Phase 1.7 UX work.
- [x] Keep file-upload inputs custom where browser semantics require it.
- [x] Run app-local tests and typechecks.
- [x] Commit with `refactor: complete shared ui adoption sweep`.

### Task 2.2: Chat UX

- [x] Add three-pane layout.
- [x] Wire `ConversationList`.
- [x] Add citation drawer/preview.
- [x] Add command palette.
- [ ] Add dimension filter chips and URL deep-link parsing after dimensions API exists.
- [ ] Commit with `feat: add v2 chat workspace`.

### Task 2.3: Docs UX

- [ ] Add dimension-grouped sidebar tree.
- [ ] Add right TOC with scroll-spy.
- [ ] Add breadcrumbs and previous/next navigation.
- [ ] Replace raw `dangerouslySetInnerHTML` document rendering with sanitized rendering behavior.
- [ ] Add global search.
- [ ] Commit with `feat: add v2 docs portal`.

### Task 2.4: Manage UX

- [ ] Add dashboard KPI cards/charts.
- [ ] Add dimensions editor.
- [ ] Add users role dropdown and bulk CSV import.
- [ ] Refactor high-risk forms to React Hook Form + Zod only where validation complexity justifies it.
- [ ] Add analytics page after metrics endpoints exist.
- [ ] Commit with `feat: add v2 management workspace`.

## Phase 3: Retrieval And Dimensions

**Goal:** Make hybrid retrieval and flexible categorization complete, testable, and operator-visible.

- [ ] Add EF Core entities and mappings for dimensions.
- [ ] Add dimensions CRUD controller and validation for unique keys and parent cycles.
- [ ] Add document dimension assignment UI.
- [ ] Add reindex script and admin-triggered reindex job.
- [ ] Add `docs/operations/reindex.md`.
- [ ] Add golden-set retrieval metric validation.
- [ ] Add per-query `rerank=false` flag and mocked-reranker tests.
- [ ] Commit with `feat: complete dimensions and retrieval controls`.

## Phase 4: Object Storage And Bulk Import

**User-owned decisions required:**

- Resolve `OQ-003` MinIO vs Garage.
- Resolve `OQ-009` VLM-described images opt-in.

**Goal:** Move binary assets out of Postgres and support bulk onboarding.

- [ ] Add tenant storage config.
- [ ] Add Caddy `/storage/*` route with signed URL behavior.
- [ ] Add .NET object storage abstraction.
- [ ] Add FastAPI object storage abstraction.
- [ ] Add filesystem fallback for development.
- [ ] Add TipTap image upload, drag/drop, and paste handlers.
- [ ] Add PDF/DOCX table extraction and image preservation.
- [ ] Add ZIP import endpoint, per-file indexing enqueue, and progress UI.
- [ ] Commit with `feat: add object storage and bulk import`.

## Phase 5: CdA Features And Analytics

**Goal:** Add selected help-center features on top of the stable v2 foundation.

- [ ] Materialize raw SQL scripts for document views, reactions, favorites, API keys, webhooks, and metrics as EF migrations.
- [ ] Define materialized metrics refresh strategy.
- [ ] Add document view tracking endpoint.
- [ ] Add document reactions endpoint.
- [ ] Add favorites endpoints.
- [ ] Add analytics endpoints.
- [ ] Add API key CRUD and middleware.
- [ ] Add favorite/reaction UI, My Favorites, top-read landing, and analytics dashboard.
- [ ] Commit with `feat: add help center analytics features`.

## Phase 6: Quality And Observability

**User-owned decisions required:**

- Resolve `OQ-006` conversational memory default.
- Resolve `OQ-010` webhook delivery worker.

**Goal:** Make RAG quality and operational visibility release-grade.

- [ ] Verify RAGAS workflow runs in CI or document required secrets and current limitation.
- [ ] Wire conversational memory into `ChatService.answer`.
- [ ] Include condensed standalone question in the cache key when memory is enabled.
- [ ] Add query rewrite tenant toggle and chat path integration.
- [ ] Generate citation `text_quote` and `page_number` at runtime.
- [ ] Add viewer citation highlighting.
- [ ] Add .NET OpenTelemetry SDK instrumentation.
- [ ] Add FastAPI OpenTelemetry SDK instrumentation.
- [ ] Add Grafana dashboard JSON.
- [ ] Add unresolved-question clustering job and dashboard.
- [ ] Add webhook CRUD, delivery worker, HMAC signature, and retry behavior.
- [ ] Commit with `feat: add rag quality and observability`.

## Phase 7: Ship Readiness

**Goal:** Produce a release candidate that can be installed, tested, operated, and restored.

- [ ] Run Lighthouse audit and fix blocking accessibility/performance issues.
- [ ] Run load test for management, chat, docs, and indexing paths.
- [ ] Write install guide.
- [ ] Write admin guide.
- [ ] Write API reference.
- [ ] Polish backup/restore scripts and documentation.
- [ ] Run full verification:

```powershell
dotnet test services/dotnet-api/AdvancedRag.sln
Set-Location services/rag-api; uv run pytest -q; uv run ruff check .; uv run mypy src tests; Set-Location ..\..
pnpm -r test -- --run
pnpm -r typecheck
pnpm -r build
docker compose --env-file infra/compose/.env.example -f infra/compose/compose.yaml config
docker compose --env-file infra/compose/.env.example -f infra/compose/compose.yaml -f infra/compose/compose.override.yaml up -d --build
pnpm --dir tests/e2e test
rg -n "NEEDS_DECISION|UNRESOLVED|FOLLOWUP_REQUIRED" README.md docs context --glob "!docs/superpowers/plans/2026-05-11-mvp-implementation-plan.md"
```

Expected: all tests pass, Compose starts, E2E passes, docs have no unresolved markers except explicitly open questions.

- [ ] Commit with `docs: add v2 release handoff`.

## Recommended Starting Point

Start with Phase 0 only. Do not begin new feature implementation until the current working tree is reconciled, obsolete token-flow references are removed from active tests/docs, focused verification passes, and the reconciliation commit exists.
