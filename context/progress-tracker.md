# Progress Tracker

## Current Phase

- MVP implementation.

## Current Goal

- Finish Task 3 FastAPI RAG foundation and prepare the user-owned tooling check for Task 4 frontend foundations.

## Completed

- Read the initial prompt and existing context files.
- Confirmed the project is too large for one implementation unit and selected the first sub-project: complete base architecture.
- Selected the architecture approach: Monorepo Compose Product.
- Selected API contract detail level: define MVP contracts by contract group in the architecture spec and defer exhaustive endpoint DTO details to implementation planning.
- Approved MVP API contract groups: auth/session, users/groups, documents, viewer, chat/RAG, feedback/reporting, internal indexing, and health/errors.
- Approved system architecture section.
- Approved identity, roles, access model, and viewer access token approach.
- Captured current architecture decisions in `context/architecture.md`.
- Created `context/design-decisions.md` to avoid losing brainstorming decisions.
- Persisted the rule that meaningful decisions must be written to context files during brainstorming.
- Selected OpenAI MVP defaults: `gpt-4.1-mini` for chat and `text-embedding-3-large` for embeddings with configured dimension reduction to 1536 for pgvector compatibility.
- Selected secure browser session strategy: use `HttpOnly`, `Secure`, `SameSite` cookies with CSRF protection, and prohibit credential-bearing tokens in browser storage.
- Selected browser API topology: route frontend requests through same-origin `/api/*` Caddy paths with host-only `__Host-` cookies; avoid broad parent-domain cookies in the MVP.
- Selected FastAPI auth validation strategy: locally validate short-lived signed access tokens issued by .NET for chat requests instead of introspecting .NET on every chat request.
- Selected chat token refresh strategy: .NET issues/renews short-lived chat tokens from the main secure session through same-origin chat routes; FastAPI has no separate refresh token in the MVP.
- Selected viewer link strategy: use one-time 60-second exchange codes in document URLs and set real viewer access tokens only as host-only `HttpOnly` cookies for `docs.client.com`.
- Approved initial database entity direction: .NET owns `app` schema entities; FastAPI owns `rag` schema entities; RAG chunks store embeddings in the same table; simple feedback belongs on query audit events; citations are query audit child rows.
- Selected MVP document access model: group/department and attribute-based access only; no per-user document ACL exceptions.
- Added AI usage budget requirement: admins can configure per-user monthly monetary budgets, initially USD, and chat blocks new paid AI usage when the configured budget is reached.
- Selected AI budget enforcement scope: budget exhaustion blocks only new paid chat/RAG work, not authorized document viewing or management workflows.
- Selected over-budget cache behavior: over-budget users do not receive semantic cached answers in the MVP because cache lookup requires a paid embedding call.
- Selected default AI usage budget: USD 5 per user per month, configurable by administrators.
- Selected AI budget period: customer calendar month using the deployment's configured timezone.
- Approved MVP operational defaults for request lengths, log retention, token TTLs, semantic cache, rate limits, customer timezone, and AI usage budget.
- Selected MVP UI foundation: React TypeScript with Tailwind CSS, `shadcn/ui`, and `lucide-react` across the three frontends.
- Selected implementation planning decisions: local development uses `manage.localhost`, `chat.localhost`, and `docs.localhost`; management reporting reads FastAPI-owned `rag` schema views through .NET read-only access; same-user chat feedback updates the single feedback value.
- Wrote the formal base architecture design spec at `docs/superpowers/specs/2026-05-11-base-architecture-design.md`.
- Self-reviewed the formal design spec for unresolved markers, stale terms, and obvious contradictions.
- Wrote the detailed MVP implementation plan at `docs/superpowers/plans/2026-05-11-mvp-implementation-plan.md`.
- Self-reviewed the implementation plan for forbidden unresolved markers and ambiguity.
- Added a session handoff section so the project can resume safely after clearing the chat.
- Audited the context distribution and closed technical gaps before implementation:
  - Locked backend library choices (Serilog, FluentValidation, FluentAssertions, Testcontainers, SQLAlchemy 2.0 async, structlog, official OpenAI SDK, etc.) in `code-standards.md`.
  - Locked frontend stack (Vite, TanStack Query, Zustand, React Hook Form + Zod, TipTap, Vitest, Playwright) in `code-standards.md` and `ui-context.md`.
  - Defined the language policy (UI in es-AR, code/comments/logs/commits in English).
  - Created `rag-spec.md`: chunking, retrieval, generation, streaming SSE, cache, pgvector HNSW, `access_scope_hash` algorithm, indexing pipeline, cost/audit.
  - Closed auth fine-grained gaps in `architecture.md`: CSRF via AntiForgery synchronizer tokens, local HTTPS via Caddy internal CA, RS256 JWT with kid + JWKS, internal service token strategy.
  - Added an Operations section to `architecture.md`: postgres-init container, migration ordering via `depends_on: service_completed_successfully`, reporting views grants to `app_reporting_reader`, X-Request-ID propagation, GitHub Actions CI, pg_dump backup baseline.
  - Created `code-patterns.md` with concrete examples for the shared error envelope (3 stacks), thin .NET controller + service, FastAPI endpoint with deps, EF Core and Alembic migrations, xUnit/pytest/Vitest tests, full Caddyfile, docker-compose skeleton, .env.example, postgres init SQL, Serilog/structlog setup, and the RAG system prompt.
- Created `context/README.md` as the entry point with reading order and source-of-truth precedence.
- Removed duplication from `progress-tracker.md` (Architecture Decisions list now references the canonical files).
- User approved starting implementation from `docs/superpowers/plans/2026-05-11-mvp-implementation-plan.md`.
- User approved documenting a human-in-the-loop implementation protocol in `AGENTS.md`, `context/ai-workflow-rules.md`, `context/progress-tracker.md`, and `context/design-decisions.md`.
- User initialized Git with `git init`.
- Created `AGENTS.md` with project operating rules, source-of-truth pointers, and the human-in-the-loop implementation protocol.
- Added the human-in-the-loop implementation protocol to `context/ai-workflow-rules.md`.
- Asked the user to create and confirm the implementation branch with `git switch -c mvp-implementation` before Task 0 baseline edits.
- Verified that the active branch is still `master`; Task 0 implementation remains blocked until the user creates and confirms `mvp-implementation`.
- Clarified that the implementation branch name is `mvp-implementation` and gave the user the exact confirmation commands.
- Verified the active branch is now `mvp-implementation`.
- Created Task 0 root baseline files: `.gitignore`, `.editorconfig`, `.gitattributes`, `README.md`, `.node-version`, `package.json`, and `pnpm-workspace.yaml`.
- Fixed the implementation plan's documentation marker scan so it excludes the plan file itself and avoids self-matching its own verification command.
- Verified Task 0 baseline status with `git status --short`.
- Verified documentation marker scan with the corrected command; no unresolved marker matches were returned.
- Created the repository baseline commit with message `chore: establish repository baseline`.
- Marked Task 0 steps complete in the MVP implementation plan.
- User confirmed local Docker tooling: Docker `28.5.1` and Docker Compose `v2.40.3-desktop.1`.
- Created Task 1 infrastructure files under `infra/compose`: Compose file, Compose override, Caddyfile, non-sensitive environment example, local secrets README, and Postgres initialization scripts.
- Updated architecture and design decisions to record separate Compose secret files for PostgreSQL admin, app, RAG, and reporting role passwords.
- Updated `context/code-patterns.md` and the Task 1 plan notes so future sessions use the current Compose, Caddy, and Postgres secret patterns instead of stale skeleton snippets.
- Ran a lightweight Compose parse with `docker compose --env-file infra/compose/.env.example -f infra/compose/compose.yaml config --no-path-resolution --no-consistency -q`; it exited successfully.
- User confirmed local secret placeholders were created for Task 1 validation.
- Verified full Compose syntax with `docker compose --env-file infra/compose/.env.example -f infra/compose/compose.yaml config`; it rendered successfully.
- Verified local secret files are ignored by Git via `git check-ignore -v`.
- Marked Task 1 steps complete in the MVP implementation plan.
- Created the Task 1 infrastructure baseline commit with message `chore: add compose and caddy baseline`.
- User reported local .NET SDK check output: `dotnet --version` returned `9.0.311` and `dotnet --list-sdks` listed only `9.0.311`.
- Verified locally that only .NET SDK `9.0.311` is installed.
- Checked WinGet availability for .NET 8 SDK; `Microsoft.DotNet.SDK.8` is available as version `8.0.421`.
- Confirmed Task 2 is blocked until .NET 8 SDK is installed because the project is pinned to .NET 8.
- User installed .NET SDK 8.0.421 side by side with .NET SDK 9.0.311.
- Added repository-root `global.json` to select .NET SDK 8.0.421 with `rollForward: latestFeature`.
- Updated `context/code-standards.md` and `context/design-decisions.md` to record the .NET SDK selection rule.
- Scaffolded the Task 2 .NET solution at `services/dotnet-api/AdvancedRag.sln` with six projects: API host, application layer, domain layer, infrastructure layer, API tests, and application tests.
- Added project references preserving the intended dependency direction: API depends on App and Infrastructure; Infrastructure depends on App and Domain; App depends on Domain; tests reference only the layers they verify.
- Added API health endpoint tests first and verified the expected RED state before wiring `/health/live` and `/health/ready`.
- Implemented minimal .NET health endpoints returning `{ "status": "ok" }`; database readiness remains deferred until EF Core is configured.
- Removed scaffold placeholder classes/tests and added a small application/domain assembly load test to keep the foundation test project non-empty.
- Added `services/dotnet-api/Dockerfile` as a multi-stage .NET 8 publish/runtime image for `AdvancedRag.Api` on port `8080`.
- Added `services/dotnet-api/.dockerignore` after Docker build exposed that local Windows `bin/obj` output can overwrite Linux restore artifacts inside the container build context.
- Verified Task 2 with `dotnet test services/dotnet-api/AdvancedRag.sln`, `dotnet build services/dotnet-api/AdvancedRag.sln`, and `docker build -f services/dotnet-api/Dockerfile services/dotnet-api`.
- User confirmed Task 3 local Python tooling: Python `3.12.5` and `uv` `0.9.22`.
- Scaffolded the FastAPI RAG service at `services/rag-api` with `uv`, exact dependency pins in `pyproject.toml`, and a committed `uv.lock`.
- Corrected the initial `uv` scaffold from Python 3.13 to Python 3.12 by adding `.python-version` and constraining `requires-python` to `>=3.12,<3.13`.
- Configured the uv build backend to package the import module `advanced_rag` while keeping the distribution name `advanced-rag-rag-api`.
- Added FastAPI health tests first, verified the expected RED state with 404 responses, then implemented `/health/live` and `/health/ready`.
- Added tested base configuration and shared error-envelope models for the future RAG API routes.
- Added `services/rag-api/Dockerfile` using the `ghcr.io/astral-sh/uv:0.9.22-python3.12-bookworm-slim` base image and `services/rag-api/.dockerignore` to keep generated local artifacts out of the container context.
- Updated root `.gitignore` to ignore Visual Studio `.vs/` working folders after local verification surfaced generated IDE indexes.
- Verified Task 3 with `uv run pytest -q`, `uv run ruff check .`, `uv run mypy src tests`, `uv build`, and `docker build -f services/rag-api/Dockerfile services/rag-api`.
- User confirmed Task 4 local Node tooling: Node `25.6.1`; `pnpm` was missing and was installed globally as `10.33.4`.
- Scaffolded the three frontend apps with Vite React TypeScript templates under `apps/manage-web`, `apps/chat-web`, and `apps/docs-web`.
- Corrected the scaffolds back to React 18.3.1 so the frontend stack matches the approved MVP code-standards decision.
- Installed shared frontend dependencies: `lucide-react`, Tailwind/Vite support, Vitest, jsdom, and Testing Library packages.
- Added root `packageManager` metadata for `pnpm@10.33.4` so the workspace resolves to the installed pnpm line without warnings.
- Added frontend shell smoke tests first, verified the expected RED state, then implemented minimal management/chat/viewer shells that render the required product labels.
- Added local shadcn-compatible support files in each app: `components.json`, `src/lib/utils.ts`, and `src/components/ui/button.tsx`.
- Added `typecheck` scripts and app-local Vitest setup files so the workspace can be verified consistently.
- Added Dockerfiles for each frontend app that build from the repo root using `pnpm` inside a Node build stage and serve the static output with nginx.
- Verified Task 4 with `pnpm -r typecheck`, `pnpm -r test -- --run`, `pnpm -r build`, and Docker builds for `apps/manage-web`, `apps/chat-web`, and `apps/docs-web`.
- Created Task 5 shared error and request ID contract tests for .NET, FastAPI, and all three frontend apps, verified the expected RED failures, then implemented the minimal shared contracts.
- Added .NET request ID propagation and safe `NOT_FOUND`/`INTERNAL_ERROR` envelope responses through API middleware.
- Added FastAPI request ID middleware plus shared handlers for `ApiException`, HTTP errors, and request validation errors.
- Added `parseApiError()` and `ApiError` helpers to the three frontend apps with tests for valid shared envelopes and malformed response fallbacks.
- Verified Task 5 with `dotnet test services/dotnet-api/AdvancedRag.sln`, `uv run pytest -q`, `uv run ruff check .`, `uv run mypy src tests`, `pnpm -r test -- --run`, and `pnpm -r typecheck`.

## In Progress

- Preparing the Task 5 scoped commit.

## Next Up

- Start Task 6 database migrations and schema ownership after the Task 5 commit.

## Open Questions

- None for the current checkpoint.

## Architecture Decisions

See `context/architecture.md`, `context/code-standards.md`, `context/rag-spec.md`, `context/ui-context.md`, and `context/design-decisions.md`. This tracker no longer duplicates the decisions; those files are the source of truth.

## Session Notes

- Conversation can continue in Spanish, but project artifacts must stay in English.
- The formal design has been written to `docs/superpowers/specs/2026-05-11-base-architecture-design.md`.
- The implementation plan has been written to `docs/superpowers/plans/2026-05-11-mvp-implementation-plan.md`.
- The spec and plan are not committed yet because Git was initialized after they were written.
- The visual companion is running at `http://localhost:55187` in `.superpowers/brainstorm/32180-1778467523`.
- The next brainstorming section will close API/data boundaries before the dedicated UI visual pass because backend contracts constrain frontend workflow design.

## Handoff For Next Session

Start by reading `context/README.md`. It defines reading order and source-of-truth precedence. Then read, in order:

- `context/project-overview.md`
- `context/architecture.md`
- `context/code-standards.md`
- `context/rag-spec.md`
- `context/ui-context.md`
- `context/code-patterns.md`
- `context/progress-tracker.md` (this file)
- `context/design-decisions.md` (history; read on demand)
- `context/ai-workflow-rules.md`

Implementation artifacts (formal spec and 18-task plan) live at `docs/superpowers/specs/` and `docs/superpowers/plans/`.

Current state:

- The project is entering implementation kickoff.
- The formal architecture spec and MVP implementation plan have been written.
- The implementation plan has been approved for execution.
- Git has been initialized by the user.
- Human-in-the-loop implementation is now required for MVP execution.
- The implementation branch is `mvp-implementation`.
- Task 0 repository baseline is complete and committed.
- Task 1 infrastructure and Caddy baseline is complete and committed.
- Task 2 .NET API foundation is complete and committed with the scoped foundation changes.
- Task 3 FastAPI RAG foundation is complete and committed with the scoped foundation changes.
- Task 4 frontend foundation is complete and committed with the scoped foundation changes.
- Task 5 shared error, request ID, and frontend error parsing contracts are implemented and ready to commit with the scoped foundation changes.
- The base architecture formal spec is written and approved as the basis for implementation: monorepo, Docker Compose, Caddy same-origin API routing, three React frontends, .NET management API, FastAPI RAG API, PostgreSQL with `app` and `rag` schemas, secure cookies, chat token flow, viewer exchange codes, document lifecycle, assisted imports, publishing blocked on successful indexing, semantic cache, AI usage budgets, audit, logs, secrets, health checks, operational defaults, UI foundation, and OpenAI model defaults.

Next safe implementation work:

- Commit Task 5, then begin Task 6 database migrations and schema ownership.
