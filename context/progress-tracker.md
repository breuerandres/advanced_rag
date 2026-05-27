# Progress Tracker

## Current Phase

- MVP implementation.

## Current Goal

- Complete Task 17.5 UI stabilization, first-run setup, and product polish before Task 18.

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
- Previously selected OpenAI MVP defaults: `gpt-4.1-mini` for chat and `text-embedding-3-large` for embeddings with configured dimension reduction to 1536 for pgvector compatibility. This was superseded on 2026-05-17 by the cost-first defaults recorded below.
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
- User confirmed Task 6 migration tooling checkpoint: .NET SDK `8.0.421`, `dotnet-ef` `8.0.27`, Docker `28.5.1`, and local `pgvector/pgvector:pg16` image.
- Added Task 6 .NET infrastructure tests for `AppDbContext` schema/table mappings and EF migration ownership against a disposable `pgvector/pgvector:pg16` Postgres container.
- Implemented the initial `.NET` `app` schema foundation with `AppDbContext`, focused persistence entities, EF mappings, and `InitialAppSchema` migration.
- Added Task 6 FastAPI Alembic migration test against a disposable `pgvector/pgvector:pg16` Postgres container.
- Implemented FastAPI Alembic foundation for the `rag` schema, including `vector` extension setup, `document_chunks.embedding vector(1536)`, RAG audit/cache/indexing/pricing tables, and lookup indexes.
- Verified Task 6 backend migration work with `dotnet test services/dotnet-api/AdvancedRag.sln`, `dotnet build services/dotnet-api/AdvancedRag.sln`, `uv run pytest -q`, `uv run ruff check .`, and `uv run mypy src tests`.
- Created the Task 6 database foundation commit with message `feat: add initial database schemas`.
- Added Task 7 .NET auth endpoint tests first and verified the expected RED state because the auth services and `AppDbContext` registration were missing.
- Implemented the .NET authentication foundation: local user login against `app.users`, PBKDF2-SHA256 password hash verification, secure host-only session cookies, logout, session status, signed CSRF cookie/header issuance and validation, RS256 chat-token issuance, and JWKS exposure.
- Added Task 7 FastAPI signed chat-token validation tests first and verified the expected RED state because `advanced_rag.auth` did not exist.
- Implemented FastAPI local RS256 chat-token validation with issuer, audience, `kid`, expiration, and required claim checks. FastAPI continues to avoid per-request .NET introspection in the MVP chat path.
- Updated `context/architecture.md`, `context/code-standards.md`, `context/code-patterns.md`, and `context/design-decisions.md` to record the Task 7 auth foundation, signed double-submit CSRF decision, `AUTH_TOKEN_INVALID`, and `CSRF_TOKEN_INVALID`.
- Verified Task 7 backend auth work with `dotnet test services/dotnet-api/AdvancedRag.sln --filter Auth`, `Set-Location services/rag-api; uv run pytest tests -k auth -q; Set-Location ..\..`, `dotnet test services/dotnet-api/AdvancedRag.sln`, `dotnet build services/dotnet-api/AdvancedRag.sln`, `uv run pytest -q`, `uv run ruff check .`, `uv run mypy src tests`, and `uv build`.
- Added Task 8 `.NET` user administration application tests for default USD 5 monthly AI budget, role assignment, group assignment, access scope hash changes, and budget validation.
- Implemented Task 8 `.NET` user administration services, EF repository, and API endpoints for listing/creating users, assigning roles/groups, activation status, per-user AI budget limits, and group listing/creation.
- Added Task 8 management UI tests for the users/budgets table, filters, non-negative budget validation, successful budget save, and safe API error display.
- Implemented the Task 8 `manage-web` users and budgets screen with Spanish UI strings, a dense table, search/status filters, budget editing dialog, CSRF-backed budget mutation, and shared API error parsing.
- Corrected `apps/manage-web` runtime React dependency drift back to React `18.3.1` so the Task 8 frontend remains aligned with the approved React 18 stack.
- Verified targeted Task 8 work with `dotnet test services\dotnet-api\tests\AdvancedRag.App.Tests\AdvancedRag.App.Tests.csproj --filter UserAdministration`, `dotnet test services\dotnet-api\tests\AdvancedRag.Api.Tests\AdvancedRag.Api.Tests.csproj --filter UserAdministration`, `dotnet build services\dotnet-api\AdvancedRag.sln`, `pnpm.cmd --dir apps\manage-web test -- --run`, `pnpm.cmd --dir apps\manage-web typecheck`, `pnpm.cmd --dir apps\manage-web build`, and `git diff --check`.
- Verified the exact Task 8 plan command after Docker Desktop was started: `docker version` succeeded and `dotnet test services\dotnet-api\AdvancedRag.sln --filter "Users|Groups|Budget"` passed. Fresh frontend verification also passed with `pnpm.cmd --dir apps\manage-web test -- --run`, `pnpm.cmd --dir apps\manage-web typecheck`, and `pnpm.cmd --dir apps\manage-web build`.
- Created the Task 8 users/groups/budget configuration commit with message `feat: add users groups and ai budget configuration`.
- Recorded the backend HTTP organization decision: `.NET` feature routes should use MVC controllers and API model folders; FastAPI should use the analogous routers, schemas, services, and infrastructure adapter structure.
- Refactored the .NET HTTP boundary from Minimal API endpoint files to MVC controllers and moved request/response DTOs into `AdvancedRag.Api/Models/<Feature>/`.
- Kept the existing routes, cookie behavior, authorization requirements, and error envelope intact during the refactor.
- Verified the refactor with `dotnet build services/dotnet-api/AdvancedRag.sln` and `dotnet test services/dotnet-api/AdvancedRag.sln --filter "UserAdministration|Health|ErrorEnvelope"`.
- Observed that `AuthEndpointTests` still depend on Docker/Testcontainers and could not be run in this environment because the Docker endpoint was unavailable.
- Added `docs/troubleshooting.md` with the local Caddy Docker HTTPS root CA import procedure and linked it from the root README.
- Fixed `AppDbContextFactory` so EF design-time commands read `ConnectionStrings__AppDatabase` from the environment instead of using the stale hardcoded local password.
- User verified the local Compose/Postgres auth and user administration path manually: `GET /api/csrf`, `POST /api/auth/login`, `POST /api/users`, and `GET /api/users`.
- Added .NET startup migration execution behind `Database__RunMigrationsOnStartup=true` in Compose so the `dotnet-api` container applies EF Core `app` schema migrations before serving requests.
- Added Task 9 document lifecycle application tests for review readiness validation, send-to-review, publish request authorization, return-to-draft comments, edit-after-publish draft creation, archive rules, restore behavior, and server-side HTML sanitization.
- Implemented Task 9 `.NET` document lifecycle use cases, EF repository, MVC documents controller, API DTOs, and `app.document_versions.indexing_status` migration column. Publish requests now mark the draft version as indexing `Pending`; the actual FastAPI indexing pipeline remains Task 10 scope.
- Added Task 9 assisted import extraction tests and adapters using `DocumentFormat.OpenXml` `3.5.1` for DOCX, `PdfPig` `0.1.14` for PDF, and `HtmlSanitizer` `9.0.892` for stored document HTML sanitization.
- Added the management document UI with document list filters, indexing status display, draft editor dirty state, import loading/success/error behavior, review validation, and archive/restore actions.
- Verified Task 9 with `dotnet test services\dotnet-api\AdvancedRag.sln --filter "Document|Import|Lifecycle"`, `pnpm.cmd --dir apps\manage-web test -- --run`, `dotnet test services\dotnet-api\AdvancedRag.sln`, `dotnet build services\dotnet-api\AdvancedRag.sln`, `pnpm.cmd --dir apps\manage-web typecheck`, and `pnpm.cmd --dir apps\manage-web build`. Verification passed; .NET commands emitted NU1900 warnings because NuGet vulnerability metadata could not be fetched from `https://api.nuget.org/v3/index.json`.
- Fixed a Task 9 EF migration discovery bug: `20260517090000_AddDocumentVersionIndexingStatus` was missing the EF migration metadata designer partial, so startup migrations did not add `app.document_versions.indexing_status` in local Compose. Added a migration test assertion for the column and verified it with `dotnet test services\dotnet-api\tests\AdvancedRag.Infrastructure.Tests\AdvancedRag.Infrastructure.Tests.csproj --filter EfMigration_CreatesOnlyAppSchemaTables`, `dotnet test services\dotnet-api\AdvancedRag.sln --filter "Migration|Document|Import|Lifecycle"`, and `dotnet build services\dotnet-api\AdvancedRag.sln`.
- Fixed a Task 9 EF query translation bug in `EfDocumentRepository.BuildAggregateAsync`: Npgsql could not translate ordering after projecting nullable `GroupId.Value`. Added a Postgres-backed repository regression test and changed the query to order by `GroupId` before projecting. Verified with `dotnet test services\dotnet-api\tests\AdvancedRag.Infrastructure.Tests\AdvancedRag.Infrastructure.Tests.csproj --filter FindAsync_LoadsAllowedGroupIdsFromPostgres`, `dotnet test services\dotnet-api\AdvancedRag.sln --filter "Document|Import|Lifecycle|Repository"`, and `dotnet build services\dotnet-api\AdvancedRag.sln`.
- Implemented Task 10 internal indexing pipeline:
  - Added `.NET` publish/index integration through `IInternalIndexingClient` and `FastApiInternalIndexingClient`.
  - Publish requests now call FastAPI's Docker-network-only `/internal/indexing-jobs` endpoint with the internal service token.
  - Successful indexing transitions the draft version to `Published`; failed indexing leaves the document `In Review` with safe `INDEXING_FAILED` behavior.
  - Added FastAPI internal indexing route, service-token validation, deterministic HTML block chunking, OpenAI embedding provider abstraction, and storage into `rag.indexing_jobs` and `rag.document_chunks`.
  - Aligned Compose environment variables for the FastAPI internal service token file.
- Verified Task 10 with `dotnet test services/dotnet-api/AdvancedRag.sln --filter Indexing`, `Set-Location services/rag-api; uv run pytest tests -k indexing -q; Set-Location ..\..`, `dotnet test services/dotnet-api/AdvancedRag.sln`, `dotnet build services/dotnet-api/AdvancedRag.sln`, `uv run pytest -q`, `uv run ruff check .`, `uv run mypy src tests`, and `docker compose --env-file infra/compose/.env.example -f infra/compose/compose.yaml config --no-path-resolution --no-consistency -q`.
- Created the Task 10 internal indexing pipeline commit with message `feat: add internal indexing pipeline`.
- Fixed a Task 10 local Compose startup bug reported during manual Postman verification: the `rag-api` container started `uvicorn` directly and did not run Alembic migrations, so publishing failed with `relation "rag.indexing_jobs" does not exist`. Added a container entrypoint that runs `alembic upgrade head` before starting `uvicorn`, copied Alembic files into the image, and updated Alembic runtime URL resolution to use container environment settings.
- Verified the Task 10 startup fix with `uv run pytest tests/test_container_startup.py -q`, `uv run pytest -q`, `uv run ruff check .`, `uv run mypy src tests`, `docker build -f services/rag-api/Dockerfile services/rag-api`, and `docker compose --env-file infra/compose/.env.example -f infra/compose/compose.yaml config --no-path-resolution --no-consistency -q`.
- Fixed a second Task 10 RAG startup migration bug reported during manual Compose verification: Alembic was running as `rag_owner` but still tried to create the `rag` schema, which requires database-level `CREATE` privilege. Moved schema/extension ownership fully to `postgres-init` as intended and kept Alembic responsible only for objects inside the existing `rag` schema.
- Added a regression test that runs Alembic as a runtime `rag_owner` role without database create privilege after bootstrapping the schema like `postgres-init`. Verified with `uv run pytest tests/test_migrations.py -q`, `uv run pytest -q`, `uv run ruff check .`, `uv run mypy src tests`, `docker build -f services/rag-api/Dockerfile services/rag-api`, and `docker compose --env-file infra/compose/.env.example -f infra/compose/compose.yaml config --no-path-resolution --no-consistency -q`.
- Implemented Task 11 FastAPI public chat/RAG core:
  - Added `/api/chat` SSE response flow with signed chat-token claims, published-corpus enforcement for viewers, retrieval, citations, usage events, and shared error behavior.
  - Added SQL-level retrieval filtering against `.NET`-owned `app.document_permissions` using signed group claims; `access_scope_hash` is used for cache partitioning and audit, not authorization.
  - Added query audit writes to `rag.query_audit_events` and `rag.query_audit_citations`, including model IDs, embedding dimensions, token counts, pricing snapshot, estimated cost, latency, request ID, corpus, prompt version, and chunker version.
  - Added semantic cache lookup/write keyed by `(corpus, access_scope_hash)` plus question embedding similarity, source tracking in `rag.semantic_cache_sources`, and internal cache invalidation at `/internal/cache-invalidations`.
  - Added AI budget enforcement before paid embedding/chat provider calls by reading `app.user_ai_budget_limits` and current-period spend from `rag.query_audit_events`.
  - Added FastAPI configuration for semantic cache defaults, customer timezone, default monthly AI budget, chat-token validation settings, and injectable providers for testability.
- Verified Task 11 with `uv run pytest tests/test_chat_rag.py -q` (`3 passed`), `uv run ruff check .` (`All checks passed!`), `uv run mypy src tests` (`Success: no issues found in 28 source files`), and `uv run pytest -q` (`21 passed`).
- Diagnosed local Postman `/api/auth/chat-token` failure: the request is routed to `.NET`, not FastAPI, and `.NET` logged `No current JWT signing key is configured.` The local `jwt_signing_keys.json` secret still needs a valid `current` RS256 key.
- Added `infra/compose/New-LocalDevSecrets.ps1` to generate valid ignored local Compose secrets for development, including Postgres role passwords, CSRF signing key, internal service token, RS256 JWT signing keys, and optional OpenAI API key. Updated `infra/compose/secrets/README.md` with the recommended local setup and JWT signing key format.
- Verified the local secrets generator syntax without writing secrets using `[scriptblock]::Create(...)`, which returned `PowerShell syntax OK`.
- Corrected the local secret generator so it never creates, overwrites, or rotates `openai_api_key.txt`; the OpenAI API key remains a developer/operator-managed external secret. The script now only warns if the file is missing.
- Fixed the local secret generator for Windows PowerShell 5.1 compatibility by replacing `RandomNumberGenerator.Fill(...)` with `RandomNumberGenerator.Create().GetBytes(...)`. Verified parser syntax and random secret generation without writing local secret files.
- Diagnosed a local `postgres-init` failure after secret overwrite: `postgres_admin_password.txt` was rotated while the existing `postgres-data` volume still had the previous `postgres` password. Updated the local secret generator so it creates the Postgres admin password only when missing and never overwrites it automatically.
- Diagnosed local Postman `/api/chat` failure after successful chat-token issuance: FastAPI returned `AUTH_TOKEN_INVALID_KEY` because the RAG service accepted `DOTNET_JWKS_URL` in Compose but did not use it to load `.NET` public signing keys.
- Implemented FastAPI JWKS-backed chat token validation using PyJWT's `PyJWKClient` when `DOTNET_JWKS_URL` is configured, while preserving the static public-key validator for tests and non-Compose configuration.
- Verified the JWKS fix with `uv run pytest tests/test_auth_tokens.py -q` (`8 passed`), `uv run pytest tests/test_chat_rag.py tests/test_auth_tokens.py -q` (`11 passed`), `uv run ruff check .` (`All checks passed!`), `uv run mypy src tests` (`Success: no issues found in 28 source files`), and `uv run pytest -q` (`23 passed`).
- Diagnosed local Postman `/api/chat` failure after JWKS fix: FastAPI connected as `rag_owner` and received `permission denied for schema app` when reading `.NET`-owned permission/budget tables. Added `postgres-init` grants for `rag_owner` to use schema `app` and select only `app.document_permissions` and `app.user_ai_budget_limits`.
- Verified the RAG read-grant fix with `uv run pytest tests/test_migrations.py -q` (`3 passed`), `uv run pytest tests/test_chat_rag.py tests/test_migrations.py -q` (`6 passed`), `uv run ruff check .` (`All checks passed!`), `uv run mypy src tests` (`Success: no issues found in 28 source files`), and `uv run pytest -q` (`24 passed`).
- Diagnosed local document publication failure during pre-publication indexing: `.NET` received `401` from FastAPI's internal indexing endpoint because Windows PowerShell 5 wrote `internal_service_token.txt` with a UTF-8 BOM. .NET consumed the BOM while FastAPI preserved it, so the logical token values differed. Updated FastAPI secret reading to ignore a leading UTF-8 BOM and updated `New-LocalDevSecrets.ps1` to write future generated secret files as UTF-8 without BOM.
- Verified the internal service token BOM fix with `uv run pytest tests/test_config.py -q` (`2 passed`), `uv run pytest -q` (`25 passed`), `uv run ruff check .` (`All checks passed!`), `uv run mypy src tests` (`Success: no issues found in 28 source files`), and PowerShell parser validation for `infra/compose/New-LocalDevSecrets.ps1`.
- Recorded the pre-Task 12 backend readability rules in `context/code-standards.md` and `context/design-decisions.md`: prefer explicit `.NET` local variable types when they improve readability, keep `var` allowed when clearer or necessary, require Pydantic for FastAPI boundary/contracts/config, allow simple internal Python classes or dataclasses only for private implementation details, and include a concise Postman checklist in final task messages when API endpoints are added or changed.
- Added Task 11.5 to `docs/superpowers/plans/2026-05-11-mvp-implementation-plan.md` as a controlled backend readability refactor audit before Task 12. The task inventories `.NET var` usage and FastAPI dataclasses, classifies what should be refactored, avoids broad mechanical churn, verifies touched backend modules, and records deferred cases.
- Completed Task 11.5 backend readability refactor audit:
  - Refactored selected `.NET` controller and document lifecycle locals to explicit concrete types where the type improves route/use-case readability.
  - Updated `context/code-patterns.md` examples to reflect the explicit-type preference for future copies.
  - Converted FastAPI cross-module value objects to frozen Pydantic models: `ChatTokenValidationSettings`, `ChatTokenClaims`, `ChatCompletionResult`, `DocumentChunk`, `RetrievedChunk`, `Citation`, `ChatAnswer`, `PricingSnapshot`, and `IndexingJobResult`.
  - Removed the test-only chat completion dataclass by returning the production `ChatCompletionResult` in the fake provider.
  - Deferred broad `.NET var` cleanup in tests, EF/LINQ projections, `using var` disposables, tuple/deconstruction cases, initializer-obvious locals, and unrelated infrastructure files to avoid style-only churn.
  - Left only the private FastAPI `_FallbackCompletion` dataclass; private parser blocks now use a simple private class and do not cross a boundary.
- Verified Task 11.5 with `dotnet build services/dotnet-api/AdvancedRag.sln`, `dotnet test services/dotnet-api/AdvancedRag.sln --filter "Chat|Indexing|Document|UserAdministration|Auth|ErrorEnvelope|Health"` (`31 passed` across matching test projects), `uv run pytest -q` (`25 passed`), `uv run ruff check .` (`All checks passed!`), `uv run mypy src tests` (`Success: no issues found in 28 source files`), and `git diff --check`. The `.NET` commands emitted NU1900 warnings because NuGet vulnerability metadata could not be fetched from `https://api.nuget.org/v3/index.json`; build and tests still passed.
- Implemented Task 12 feedback and management reporting:
  - Added FastAPI `POST /api/feedback/{query_audit_event_id}` for authenticated chat users to submit or update one thumbs up/down value and optional sanitized comment on their own `rag.query_audit_events` row.
  - Added `query_audit_event_id` to the chat SSE citations payload so the chat frontend can attach feedback to the audited answer.
  - Added FastAPI-owned Alembic reporting views `rag.v_query_audit_with_citations` and `rag.v_feedback_summary`, with conditional `GRANT SELECT` to `app_reporting_reader`.
  - Added `.NET` management reporting endpoint `GET /api/reporting/feedback` for `Admin` and `DocumentManager`, backed by a read-only Npgsql reporting service over the RAG reporting views.
  - Added chat feedback controls in `apps/chat-web` with submit/update states and optional comments.
  - Added management feedback review in `apps/manage-web` with empty state, negative filter, result table, comments, citations, user, and request id.
- Verified Task 12 with `uv run pytest tests -k feedback -q` (`3 passed`), `dotnet test services/dotnet-api/AdvancedRag.sln --filter Reporting` (`2 passed` in API tests), `pnpm.cmd --dir apps/chat-web test -- --run` (`4 passed`), and `pnpm.cmd --dir apps/manage-web test -- --run` (`13 passed`).
- Additional Task 12 verification passed: `uv run pytest -q` (`28 passed`), `uv run ruff check .`, `uv run mypy src tests`, `dotnet build services/dotnet-api/AdvancedRag.sln`, `pnpm.cmd --dir apps/chat-web typecheck`, `pnpm.cmd --dir apps/manage-web typecheck`, `pnpm.cmd --dir apps/chat-web build`, and `pnpm.cmd --dir apps/manage-web build`. `.NET` restore/build/test commands still emit NU1900 warnings because NuGet vulnerability metadata cannot be fetched from `https://api.nuget.org/v3/index.json`; build and tests passed.
- Created the Task 12 feedback/reporting commit with message `feat: add chat feedback and management reporting`.
- Implemented Task 13 viewer exchange and document viewer:
  - Added `.NET` viewer link, exchange, token, and document endpoints under `/api/viewer/*`.
  - Added `ViewerAccessService`, EF persistence over `app.viewer_exchange_codes` and `app.viewer_token_audit`, and RS256 viewer token issuance/validation through the existing JWT key store.
  - Enforced 60-second single-use exchange codes, chat links limited to `Published`, management links for `Draft`, `In Review`, and `Published` for authorized management users, and reusable 15-minute host-only `HttpOnly` viewer-token cookies.
  - Built `docs-web` exchange-code loading, safe expired/used/unauthorized/token-expired/not-found states, and successful document rendering.
  - Updated `chat-web` citation buttons to request viewer exchange links before navigation.
  - Updated `manage-web` document rows to request management viewer links before opening docs.
- Verified Task 13 with `dotnet test services\dotnet-api\AdvancedRag.sln --filter Viewer`, `pnpm.cmd --dir apps\docs-web test -- --run`, `pnpm.cmd --dir apps\chat-web test -- --run`, `pnpm.cmd --dir apps\manage-web test -- --run`, frontend typechecks and builds for docs/chat/manage, and `dotnet build services\dotnet-api\AdvancedRag.sln`. `.NET` commands emitted NU1900 warnings because NuGet vulnerability metadata could not be fetched from `https://api.nuget.org/v3/index.json`; build and tests passed.
- Created the Task 13 viewer exchange commit with message `feat: add secure viewer exchange flow`.
- Implemented Task 14 chat frontend workflow:
  - Added chat UI behavior tests for empty state, submitting state, successful answers with citations, semantic cache hit indication, feedback controls, monthly budget exhaustion, chat token renewal after `AUTH_TOKEN_EXPIRED`, and generic safe errors with request IDs.
  - Extended the typed chat API client to parse `cache-hit`, `request-id`, `usage`, answer, citations, and query audit event SSE payloads.
  - Added same-origin chat-token renewal through `/api/auth/chat-token` and retry-once behavior when the FastAPI chat token expires.
  - Updated `apps/chat-web` to a compact Spanish chat workflow with disabled empty submission, loading state, answer panel, citation actions, feedback update controls, budget-limited messaging, and safe error states.
- Verified Task 14 with `pnpm.cmd --dir apps\chat-web test -- --run` (`10 passed`), `pnpm.cmd --dir apps\chat-web typecheck`, and `pnpm.cmd --dir apps\chat-web build`.
- Implemented Task 15 management frontend workflow:
  - Added a persistent management shell navigation covering documents, users/groups, audit, feedback, AI budgets, and configuration.
  - Added management navigation tests for the full section set.
  - Extended the document workflow UI with failed-indexing retry through the `.NET` `POST /api/documents/{id}/request-publish` endpoint.
  - Added `.NET` `GET /api/configuration` and configuration UI tests for a read-only operational configuration screen that displays non-sensitive defaults, model configuration, budget defaults, cache settings, import limits, and secret-backed setting statuses without exposing secret values.
  - Added a basic audit workspace with filters and an empty state to preserve the management information architecture for Task 16+ read models.
- Verified Task 15 with `dotnet test services/dotnet-api/AdvancedRag.sln --filter Configuration` (`1 passed` in API tests), `pnpm.cmd --dir apps/manage-web test -- --run` (`18 passed`), `pnpm.cmd --dir apps/manage-web typecheck`, and `pnpm.cmd --dir apps/manage-web build`.
- Fixed the Task 15 configuration endpoint secret status detection so `.NET` recognizes Compose-style environment variables such as `OPENAI_API_KEY_FILE` in addition to hierarchical `.NET` keys. This corrected the management configuration view reporting the OpenAI API key as `Missing` when the Compose secret file is configured.
- Verified the configuration secret-status fix with `dotnet test services/dotnet-api/AdvancedRag.sln --filter Configuration` (`1 passed` in API tests).
- Diagnosed a remaining local Compose secret-status issue: `dotnet-api` could only report secrets mounted into its own container. OpenAI was mounted only into `rag-api`, and the internal service token used the legacy `InternalServiceTokenFile` key while the configuration endpoint checked Compose-style and hierarchical keys. Updated Compose wiring so `.NET` receives read-only secret file paths for status checks without exposing secret values.
- Implemented Task 16 operational hardening:
  - Added per-process fixed-window technical rate limits for login by IP, login by user/email, chat by user, assisted import extraction by user, and viewer exchange attempts.
  - Added stable rate-limit error codes: `LOGIN_IP_RATE_LIMITED`, `LOGIN_USER_RATE_LIMITED`, `CHAT_RATE_LIMITED`, `IMPORT_RATE_LIMITED`, and `VIEWER_EXCHANGE_RATE_LIMITED`.
  - Kept technical rate limits separate from monetary AI budget enforcement.
  - Added `.NET` readiness checks for database connectivity, CSRF signing secret, internal service token, and current JWT signing key load.
  - Added FastAPI readiness checks for database connectivity, pgvector extension presence, OpenAI API key, and internal service token.
  - Preserved `/health/live` as process-only liveness in both backends.
  - Added daily JSON request logs for `.NET` and FastAPI with timestamp, service, request id, origin IP, route, method, response status, safe error code, and elapsed milliseconds.
  - Added Compose health checks for `.NET`, FastAPI, all three frontend containers, and Caddy, with Caddy gated on healthy upstream services.
  - Added `docs/operations/operational-hardening.md` and linked it from the root README.
- Verified Task 16 with `dotnet test services/dotnet-api/AdvancedRag.sln --filter "RateLimit|Health|Logging"` (`9 passed` in API tests), `uv run pytest tests -k "rate_limit or health or logging" -q` (`6 passed, 26 deselected`), and `docker compose --env-file infra/compose/.env.example -f infra/compose/compose.yaml config`.
- Additional Task 16 verification passed with `dotnet build services/dotnet-api/AdvancedRag.sln`, `uv run ruff check .`, `uv run mypy src tests`, and `git diff --check`. `.NET` restore/build/test commands still emit NU1900 warnings because NuGet vulnerability metadata cannot be fetched from `https://api.nuget.org/v3/index.json`; build and tests passed.
- Implemented Task 17 end-to-end MVP verification:
  - Added a dedicated Playwright package under `tests/e2e` with TypeScript config, Chromium project settings, and a wrapper that skips E2E during recursive unit-test runs.
  - Added the MVP happy-path E2E test covering admin setup, viewer/group creation, document manager draft/review, admin publish with indexing, viewer chat with citations, secure viewer exchange, feedback submission, management feedback reporting, AI budget exhaustion, and preserved document access.
  - Seeded deterministic E2E bootstrap users, roles, and pricing through Compose PostgreSQL because the MVP has no first-admin bootstrap UI yet.
  - Fixed `.NET` operational request logging to serialize concurrent appends to the same daily JSON log file on Windows.
  - Fixed `.NET` feedback reporting SQL generation for no-filter requests so management feedback review works in the full stack.
  - Fixed local Compose viewer links by setting `Viewer__DocsBaseUrl=https://docs.${PUBLIC_DOMAIN}` for `.NET`.
  - Added shared nginx SPA fallback config for the three frontend containers, including IPv4 and IPv6 localhost listeners for Compose health checks and deep-link support.
  - Updated `docs-web` to remove one-time viewer exchange codes from the browser URL after successful exchange so reloads use the viewer cookie rather than re-consuming the code.
- Verified Task 17 with `pnpm --dir tests/e2e test` against the Compose stack (`1 passed`), `dotnet test services/dotnet-api/AdvancedRag.sln` (`62 passed` across .NET test projects), `uv run pytest -q` (`32 passed`), `pnpm -r test -- --run` (`37 frontend tests passed; E2E package intentionally skipped recursive unit run`), `pnpm -r typecheck`, `pnpm -r build`, `docker compose --env-file infra/compose/.env.example -f infra/compose/compose.yaml config`, and `git diff --check`. `.NET` commands still emit NU1900 warnings because NuGet vulnerability metadata cannot be fetched from `https://api.nuget.org/v3/index.json`; tests and builds passed.
- User review after Task 17 identified a product usability gap that must be fixed before documentation/handoff: the database migrates and services are healthy, but a clean deployment has no human first-run admin setup, no complete login/register surfaces, no user-friendly bootstrap path, and the current UI is too scaffold-like for MVP demo quality.
- Created Stitch design reference for Task 17.5:
  - Project: `projects/544909270556047969`.
  - Design system: `assets/df5cbb6e08e34c07abdf928b24898e57`.
  - Generated screens cover login/first-run bootstrap, management console, chat app, and document viewer states.
- Inserted Task 17.5 into `docs/superpowers/plans/2026-05-11-mvp-implementation-plan.md` and user approved implementing it before Task 18 when token budget is available.
- Created local Codex skill `advanced-rag-product-ui-polish` to adapt Cult UI `components-build` and `fixing-motion-performance` guidance into a Task 17.5 UI quality gate.
- Updated `context/ui-context.md` and `context/design-decisions.md` so the effective Task 17.5 UI polish rules are project memory, not only local skill state.
- Reviewed `cyxzdev/Uncodixfy` and `hursh-shah/codex-design-skill` as additional Task 17.5 UI references. Integrated Uncodixfy anti-generic-UI guardrails and the codex-design-skill direction/validation pattern into the local skill and project UI context; rejected their direct installation as project dependencies.
- Started Task 17.5 implementation:
  - Added .NET first-run setup application tests and API integration tests first, observed the expected RED failures for missing setup types/routes, then implemented `GET /api/setup/status` and `POST /api/setup/admin`.
  - The setup API creates the first active `Admin`, ensures base roles (`Admin`, `DocumentManager`, `Viewer`) exist, assigns the default USD 5 monthly AI budget, supports login after setup, and blocks later setup attempts with stable code `SETUP_ALREADY_COMPLETED`.
  - Verified targeted backend setup work with `dotnet test services/dotnet-api/tests/AdvancedRag.App.Tests/AdvancedRag.App.Tests.csproj --filter SetupService` (`4 passed`) and `dotnet test services/dotnet-api/tests/AdvancedRag.Api.Tests/AdvancedRag.Api.Tests.csproj --filter SetupEndpoint` (`2 passed`). The commands emitted NU1900 warnings because NuGet vulnerability metadata could not be fetched from `https://api.nuget.org/v3/index.json`; tests passed.
  - Added `manage-web` first-run setup, login/session handling, logout, and authenticated shell tests before implementation.
  - Added management UI creation flows for groups and users with CSRF-backed API calls, compact dialogs, local validation, and Spanish user-facing messages.
  - Verified the management UI checkpoint with `pnpm.cmd --dir apps\manage-web test -- --run App.test.tsx` (`23 passed`) and `pnpm.cmd --dir apps\manage-web typecheck`.
  - Added `infra/compose/Seed-LocalDemoData.ps1` for local demo seeding with deterministic demo users, group, model pricing, budgets, and optional sample document data. The script prints demo credentials only at runtime and does not create or commit secrets.
  - Added `tests/e2e/specs/first-run-product-flow.spec.ts` to reset the local E2E data state, complete first-run admin setup through the browser, create a group and viewer from management UI, publish a document through product APIs, and verify chat/viewer authenticated and no-session states.
  - Polished `chat-web` and `docs-web` to align with the Stitch direction: warm operational background, teal action/status accents, compact state strips, chat character count and usage/cost display, and viewer token-expiry context in a metadata side rail.
  - Verified local non-Compose checks: `pnpm.cmd -r test -- --run` (`42 frontend tests passed; E2E package skipped recursive unit run by design`), `pnpm.cmd -r typecheck`, `pnpm.cmd -r build`, `dotnet test services\dotnet-api\AdvancedRag.sln` (`68 passed`; NU1900 warnings because NuGet vulnerability metadata could not be fetched), `Set-Location services\rag-api; uv run pytest -q; Set-Location ..\..` (`32 passed`), `pnpm.cmd --dir tests\e2e typecheck`, `docker compose --env-file infra/compose/.env.example -f infra/compose/compose.yaml config`, and `git diff --check` (only LF/CRLF warnings, no whitespace errors).
  - Docker Compose services are not currently running (`docker compose ... ps` returned no running services), so Playwright E2E and Browser visual verification are pending a user-owned stack startup checkpoint.
- Addressed 2026-05-20 management app review feedback within Task 17.5:
  - Removed duplicate `Feedback` and `Presupuestos IA` management navigation entries.
  - Kept AI budget controls in `Usuarios y grupos` and embedded feedback review inside `Auditoria`.
  - Added document creation from the management document list.
  - Expanded the document list and `.NET` document summary contract with document type, audience, and allowed group ids for list-level filtering.
  - Added document filters for search text, lifecycle state, indexing state, document type, audience, and access-group coverage.
  - Replaced the plain textarea with a local dependency-free HTML editor toolbar as an interim Task 17.5 implementation until the approved TipTap dependencies are installed.
  - Verified the checkpoint with `pnpm.cmd --dir apps\manage-web test -- --run App.test.tsx` (`24 passed`), `pnpm.cmd --dir apps\manage-web typecheck`, `pnpm.cmd --dir apps\manage-web build`, `dotnet build services\dotnet-api\AdvancedRag.sln`, and `dotnet test services\dotnet-api\AdvancedRag.sln --filter Document` (`22 passed`). `.NET` commands emitted the existing NU1900 warnings because NuGet vulnerability metadata could not be fetched; build and tests passed.
- Addressed the follow-up 2026-05-20 management app review:
  - Converted document creation/editing from a modal into full `Documentos` workspace tabs for `Listado` and `Editor`.
  - Installed TipTap dependencies in `apps/manage-web` and replaced the interim HTML editor with a TipTap editor supporting headings, bold, italic, underline, lists, code blocks, links, images, and tables.
  - Added logical user deactivation/reactivation from `Usuarios y grupos` through the existing `.NET` user status endpoint.
  - Moved active session identity and logout controls to the bottom of the management sidebar.
  - Clarified `Auditoria` by separating functional management events from embedded audited chat feedback.
  - Added Vitest jsdom geometry polyfills required by ProseMirror/TipTap component tests.
  - Verified this checkpoint with `pnpm.cmd --dir apps\manage-web test -- --run App.test.tsx` (`26 passed`), `pnpm.cmd --dir apps\manage-web typecheck`, and `pnpm.cmd --dir apps\manage-web build`. The build passed with the expected Vite chunk-size warning after adding TipTap.
- Added management action-button tooltips:
  - Icon-only action buttons now derive native `title` and visual hover/focus tooltip text from their accessible `aria-label`.
  - Covered user actions, document row actions, refresh actions, configuration refresh, and TipTap editor toolbar actions through the shared local `Button` component.
  - Verified with `pnpm.cmd --dir apps\manage-web test -- --run App.test.tsx` (`28 passed`).
- Separated feedback from functional audit again after user review:
  - Restored `Feedback` as a standalone management navigation entry and route.
  - Removed embedded feedback review from `Auditoria` so audit remains focused on functional activity.
  - Documented that the audit workspace still needs a real functional event read model before it can show activity rows.
  - Verified with `pnpm.cmd --dir apps\manage-web test -- --run App.test.tsx` (`29 passed`).
- Addressed the next 2026-05-20 management review pass:
  - Removed native browser `title` attributes from icon action buttons so only the custom visual tooltip appears.
  - Reduced management workspace horizontal overflow by removing global workspace/table minimum horizontal scrolling and allowing table/filter wrapping.
  - Replaced the default document import file input with an accessible styled file picker that shows the PDF/DOCX 10 MB limit and validates oversized files client-side.
  - Added `.NET` `GET /api/audit/events` over `app.audit_events`, a management frontend audit table with search/type filters, and a demo seed `document.created` audit event when `Seed-LocalDemoData.ps1 -WithSampleDocument` is used.
  - Verified with `pnpm.cmd --dir apps\manage-web test -- --run App.test.tsx` (`29 passed`), `pnpm.cmd --dir apps\manage-web typecheck`, `pnpm.cmd --dir apps\manage-web build`, `dotnet test services\dotnet-api\AdvancedRag.sln --filter ManagementAudit` (`2 passed`), and `dotnet test services\dotnet-api\AdvancedRag.sln --filter "ManagementAudit|Document"` (`24 passed`). `.NET` commands emitted the existing `NU1900` warnings because NuGet vulnerability metadata could not be fetched; tests passed.
- Addressed the 2026-05-20 document lifecycle action visibility bug:
  - Passed authenticated session roles into the management document workspace.
  - Added a role/state action matrix so `Draft` documents show `Enviar a revision`, `In Review` documents show `Publicar` only for `Admin`, and `DocumentManager` users no longer see invalid review/publish actions.
  - Restricted failed-indexing retry to `Admin` users on `In Review` documents and aligned archive/restore/viewer row actions with existing lifecycle authority rules.
  - Added regression coverage for `Admin` publish visibility and `DocumentManager` in-review action hiding.
  - Verified with `pnpm.cmd --dir apps\manage-web test -- --run App.test.tsx` (`31 passed`), `pnpm.cmd --dir apps\manage-web typecheck`, `pnpm.cmd --dir apps\manage-web build`, and `git diff --check -- apps\manage-web\src\App.tsx apps\manage-web\src\App.test.tsx apps\manage-web\src\features\documents\DocumentsPage.tsx`. The Vite build still emits the known TipTap chunk-size warning; build succeeded.
- Completed the 2026-05-20 structural vocabulary rename from the legacy `instruction` domain to `documents`:
  - Renamed .NET application and persistence identifiers to `Document*`, including document lifecycle commands, viewer access types, HTML sanitizer naming, EF entity mappings, and API DTO fields.
  - Renamed app schema mappings to `app.documents`, `app.document_versions`, `app.document_permissions`, `app.document_tags`, `document_id`, `document_version_id`, and `document_type`.
  - Added a compatibility EF migration for existing local databases that still have the old `instruction_*` objects.
  - Renamed FastAPI RAG request schemas, chat/citation/cache/indexing code, Alembic schema columns/indexes, reporting views, and invalidation payloads to `document*`.
  - Added a compatibility Alembic migration for existing RAG databases with old `instruction_id` and `instruction_version_id` columns.
  - Updated frontend API types, tests, E2E SQL setup/cleanup, Compose demo seed script, project context, specs, and plan vocabulary.
  - Verified with `dotnet test services\dotnet-api\AdvancedRag.sln` (`70 passed`; existing NU1900 warnings), `uv run pytest -q` (`32 passed`), `uv run ruff check .`, `uv run mypy src tests`, `pnpm.cmd -r test -- --run` (`50 frontend tests passed; E2E package skipped by design), `pnpm.cmd -r typecheck`, `pnpm.cmd -r build` (known TipTap chunk-size warning), `docker compose --env-file infra/compose/.env.example -f infra/compose/compose.yaml config`, and `git diff --check` (CRLF warnings only).
- Diagnosed the 2026-05-20 `postgres-init` exit 3 failure after the documents rename: `postgres-init` was granting table-level SELECT on `app.document_permissions` before `.NET` EF migrations could create or rename that table.
- Moved `.NET` app-table read grants for `rag_owner` out of `postgres-init` and into an EF migration that runs after `app.document_permissions` and `app.user_ai_budget_limits` exist, while keeping `postgres-init` responsible for roles, schemas, and schema USAGE.
- Verified the focused fix with `uv run pytest tests/test_migrations.py -q` (`3 passed`) and `dotnet test services\dotnet-api\tests\AdvancedRag.Infrastructure.Tests\AdvancedRag.Infrastructure.Tests.csproj --filter EfMigration_CreatesOnlyAppSchemaTables` (`1 passed`; existing NU1900 warnings).
- Diagnosed the follow-up `.NET` migration failure where `20260517090000_AddDocumentVersionIndexingStatus` attempted to alter `app.document_versions` before the compatibility rename could convert an existing local `app.instruction_versions` table.
- Made the indexing-status migration idempotent across both legacy and renamed table names so existing local databases can upgrade without manual `ALTER TABLE` or volume deletion.
- Verified with a new legacy-upgrade regression test plus `dotnet test services\dotnet-api\AdvancedRag.sln` (`71 passed`; existing NU1900 warnings) and `docker compose --env-file infra/compose/.env.example -f infra/compose/compose.yaml config`.
- Tightened management text input vertical spacing by removing vertical padding from `input[type='text']` in `apps/manage-web/src/App.css`, while preserving textarea padding.
- Verified the focused CSS change with `pnpm.cmd --dir apps\manage-web typecheck` and `git diff --check -- apps/manage-web/src/App.css`. `pnpm.cmd --dir apps\manage-web test -- --run App.test.tsx` is still blocked by the pre-existing uncommitted `DocumentsPage.tsx` change that removed the expected `PDF o DOCX, maximo 10 MB.` text.
- Diagnosed the 2026-05-20 feedback/audit regression after the documents rename:
  - FastAPI's Alembic rename migration dropped and recreated `rag.v_query_audit_with_citations` and `rag.v_feedback_summary` without restoring `app_reporting_reader` SELECT grants, so `.NET` management reporting could not read feedback.
  - Existing `.NET` audit rows retained legacy `instruction.*` event types and `instruction` entity types, so the audit UI displayed old vocabulary.
- Added a follow-up Alembic migration to restore reporting-view SELECT grants after the documents rename.
- Added a follow-up EF migration to convert existing `app.audit_events` rows from `instruction.*` / `instruction` / `instructionId` to `document.*` / `document` / `documentId`.
- Verified with `dotnet test services\dotnet-api\AdvancedRag.sln` (`72 passed`; existing NU1900 warnings), `uv run pytest -q` (`32 passed`), `uv run ruff check .`, and `docker compose --env-file infra/compose/.env.example -f infra/compose/compose.yaml config`.
- Addressed the 2026-05-20 management usability review for users/groups, manage-side chat context, feedback, audit filters, and document status scanning:
  - Added visible group management inside `Usuarios y grupos`, including a group table and `.NET` `PUT /api/groups/{id}` rename support.
  - Added user role/group editing from the users table using the existing role/group assignment endpoints, while keeping AI budget editing as a separate action.
  - Bounded users/audit search controls and constrained audit `Tipo` filter width to avoid horizontal overflow.
  - Removed request ID and citations from the normal feedback table and added an Excel-compatible CSV export that includes all available feedback/reporting fields.
  - Added semantic color badges for `Draft`, `In Review`, `Published`, and `Archived` document states.
  - Deferred embedding chat directly inside `manage-web` because the current architecture assigns management browser APIs to `.NET` and public chat APIs to FastAPI on `chat.localhost`; a safe implementation needs an approved management preview contract or `.NET`-controlled proxy rather than a direct manage-to-FastAPI call or iframe.
  - Verified the focused checkpoint with `pnpm.cmd --dir apps\manage-web typecheck`, `pnpm.cmd --dir apps\manage-web test -- --run App.test.tsx` (`31 passed`), `dotnet test services\dotnet-api\AdvancedRag.sln --filter UserAdministration` (`8 passed across matching app/api tests; existing NU1900 warnings`), and `dotnet build services\dotnet-api\AdvancedRag.sln` (build passed; existing NU1900 warnings).
- Re-verified the 2026-05-20 management usability checkpoint on 2026-05-21 after user review:
  - Confirmed the current workspace includes user role/group editing, visible group management with rename support, compact users/audit filters, feedback table column reduction with Excel-compatible CSV export, and semantic document lifecycle badges.
  - Verified with `pnpm.cmd --dir apps\manage-web test -- --run App.test.tsx` (`31 passed`), `pnpm.cmd --dir apps\manage-web typecheck`, and `dotnet test services\dotnet-api\AdvancedRag.sln --filter UserAdministration` (`8 matching tests passed`; existing NU1900 warnings because NuGet vulnerability metadata could not be fetched from `https://api.nuget.org/v3/index.json`).
- Ran the first desktop-only management visual audit against the local Compose stack on 2026-05-21:
  - Added `tests/e2e/specs/manage-visual-audit.spec.ts` to authenticate against `https://manage.localhost`, capture normal desktop screenshots, wait for management data to load, and report table/text overflow issues without mobile findings.
  - Polished management table rendering by giving the users, documents, audit, feedback, and groups tables explicit classes, container-relative widths, no clipped status/action text, and horizontal overflow detection based on rendered table width rather than hidden icon-tooltip accessible text.
  - Tightened the Feedback desktop layout after visual review: the filter controls now use consistent heights and bounded columns, and the feedback table uses explicit column sizing so `Feedback`, `Comentario`, `Cache`, and `Fecha` headers do not wrap awkwardly.
  - Rebuilt `manage-web`/Caddy through Compose so `manage.localhost` served the updated bundle.
  - Verified with `pnpm.cmd --dir tests\e2e exec playwright test manage-visual-audit.spec.ts --project chromium` (`1 passed`, `tests/e2e/artifacts/manage-visual-audit/summary.md` reported `Issues found: 0`), `pnpm.cmd --dir tests\e2e typecheck`, and `pnpm.cmd --dir apps\manage-web typecheck`.
- Fixed the 2026-05-22 v2 `rag-api` Docker build failure caused by `services/rag-api/pyproject.toml` declaring `tenacity==9.1.2` while `services/rag-api/uv.lock` had not been refreshed.
  - Updated `services/rag-api/uv.lock` with the missing locked `tenacity` package.
  - Verified with `uv lock --check`, `uv sync --locked --no-dev --no-install-project`, and `docker compose --env-file infra/compose/.env.example -f infra/compose/compose.yaml -f infra/compose/compose.override.yaml build rag-api`.
- Fixed the 2026-05-22 v2 `docs-web` Docker build failure caused by the i18n scaffold importing `i18next`, `react-i18next`, and `i18next-browser-languagedetector` before those runtime dependencies were declared.
  - Added the i18n dependencies to `apps/manage-web`, `apps/chat-web`, and `apps/docs-web`, then refreshed `pnpm-lock.yaml`.
  - Verified with `pnpm --dir apps/docs-web typecheck`, `pnpm --dir apps/docs-web build`, `pnpm --dir apps/chat-web typecheck`, `pnpm --dir apps/chat-web build`, `pnpm --dir apps/manage-web typecheck`, `pnpm --dir apps/manage-web build`, and `docker compose --env-file infra/compose/.env.example -f infra/compose/compose.yaml -f infra/compose/compose.override.yaml build docs-web`.
- Fixed the 2026-05-22 v2 Alembic upgrade failure in `20260522_120000_v2_change_embedding_dimensions.py`.
  - The migration now preserves historical `rag.document_chunks` rows and their citation references by marking chunks inactive, dropping invalid 1536-dimensional embeddings to `NULL`, and resizing the column to `vector(1024)`.
  - Added regression coverage for upgrading with an existing chunk, semantic cache entry, and query-audit citation.
  - Verified with `uv run pytest tests/test_migrations.py -q`, `uv run pytest -q`, `uv run ruff check .`, `uv run mypy src tests`, and `docker compose --env-file infra/compose/.env.example -f infra/compose/compose.yaml -f infra/compose/compose.override.yaml build rag-api`.
- Fixed the 2026-05-22 full Compose startup failure where `rag-api` exited during `20260522_120100_v2_add_bm25_columns.py` because `public.unaccent` was missing from the persistent Postgres database.
  - Updated `postgres-init` to install `pg_trgm` and `unaccent` alongside `vector`.
  - Tightened the BM25 migration's `unaccent` wrapper with an explicit `regdictionary` cast.
  - Verified with `uv run pytest tests/test_migrations.py -q`, `uv run ruff check .`, `uv run mypy src tests`, and `docker compose --env-file infra/compose/.env.example -f infra/compose/compose.yaml -f infra/compose/compose.override.yaml up -d --build --force-recreate`; all Compose services reached healthy/running state.

- Executed the 2026-05-22 v2 shared-ui wiring phase for the three React SPAs.
  - Added `@helpcenter/shared-ui` as a workspace dependency for `manage-web`, `chat-web`, and `docs-web`.
  - Imported shared Inter/tokens/globals CSS in each app entrypoint, enabled the Tailwind CSS v4 Vite plugin, and added `@source "../../../packages/shared-ui/src"` so shared component classes are generated in each app bundle.
  - Replaced each authenticated/product shell with shared `AppShell`, `Header`, `Sidebar`, and visible `DarkModeToggle` while keeping existing screen workflows intact.
  - Updated frontend Dockerfiles to copy `packages/shared-ui` into the build context before frozen installs/builds.
  - Aligned frontend React type packages to React 18 and hardened `useTheme` for test/browser environments where storage or media APIs are unavailable.
  - Verified with shared-ui/app typechecks, all three app Vitest suites, all three app builds, all three app lint commands, Docker image builds for `manage-web`, `chat-web`, and `docs-web`, Compose recreation of those services, `docker compose ps`, and HTTPS 200 checks for `manage.localhost`, `chat.localhost`, and `docs.localhost`.
- Added clean Compose startup defaults on 2026-05-22:
  - Added a `.NET` EF migration that creates `admin@admin.com` with initial password `admin`, assigns the `Admin` role, and creates the default monthly AI budget.
  - Added a FastAPI Alembic migration that seeds active pricing rows for `gpt-4.1-nano` and `text-embedding-3-small`.
  - Documented the default admin password rotation requirement in `README.md`, `docs/operations/operational-hardening.md`, `context/architecture.md`, and `context/design-decisions.md`.
  - Verified the focused migration behavior with `dotnet test services\dotnet-api\tests\AdvancedRag.Infrastructure.Tests\AdvancedRag.Infrastructure.Tests.csproj --filter "EfMigration_SeedsDefaultAdminUser"`, `dotnet test services\dotnet-api\tests\AdvancedRag.Infrastructure.Tests\AdvancedRag.Infrastructure.Tests.csproj --filter "EfMigration"`, `uv run pytest tests/test_migrations.py -q`, `uv run ruff check .`, `docker compose --env-file infra/compose/.env.example -f infra/compose/compose.yaml -f infra/compose/compose.override.yaml config`, and `git diff --check`.
- Added `infra/compose/Start-Local.ps1` on 2026-05-22:
  - The script verifies required local secret files, starts the local Compose stack, and optionally imports the Docker Compose Caddy internal CA into `Cert:\CurrentUser\Root` with `-TrustCaddyCertificate`.
  - Removed the generated root `caddy-local-root.crt` from Git tracking and added it to `.gitignore` so manual root-certificate exports are not committed accidentally.
  - Documented the recommended first-start command in `README.md`, `infra/compose/secrets/README.md`, `docs/troubleshooting.md`, `docs/operations/operational-hardening.md`, `context/architecture.md`, and `context/design-decisions.md`.
  - Verified with PowerShell script parsing, `docker compose --env-file infra/compose/.env.example -f infra/compose/compose.yaml -f infra/compose/compose.override.yaml config`, and `git diff --check`.
- Addressed the 2026-05-22 post-shared-ui product review for manage, chat, and docs:
  - Removed the shared global top header from the three product SPAs and kept navigation/workflow controls inside each app's own layout.
  - Fixed chat bootstrap so `chat.localhost` first validates the .NET session and then renews a chat token through same-origin auth routes before allowing questions.
  - Turned the `docs.localhost` root into an independent authenticated document portal with search, group/category filters, and role-aware document visibility; exchange-code URLs still preserve the focused viewer flow.
  - Added visible language selectors to manage, chat, and docs, while keeping Spanish as the default unless the user explicitly chooses a persisted language.
  - Added a management `Mi cuenta` screen for changing the current user's email and password through new account endpoints.
  - Removed the modal header close buttons from users/groups budget dialogs; save actions close the dialog and cancel remains in the footer.
  - Reworked dark-mode surfaces across manage, chat, and docs to use the shared token palette instead of leaving white cards/forms inside black pages.
  - Verified with `pnpm.cmd --dir apps\chat-web test -- --run App.test.tsx`, `pnpm.cmd --dir apps\docs-web test -- --run App.test.tsx`, `pnpm.cmd --dir apps\manage-web test -- --run App.test.tsx`, app builds for all three SPAs, `dotnet test services\dotnet-api\tests\AdvancedRag.App.Tests\AdvancedRag.App.Tests.csproj --filter "UserAccount|ViewerDocumentCatalog"`, `dotnet test services\dotnet-api\tests\AdvancedRag.App.Tests\AdvancedRag.App.Tests.csproj --no-build`, `dotnet test services\dotnet-api\tests\AdvancedRag.Api.Tests\AdvancedRag.Api.Tests.csproj --no-build --filter "FullyQualifiedName~ViewerEndpointTests"`, and `dotnet build services\dotnet-api\AdvancedRag.sln --no-restore`.
  - A broader API filter run, `dotnet test services\dotnet-api\tests\AdvancedRag.Api.Tests\AdvancedRag.Api.Tests.csproj --filter "Viewer|Auth|User"`, exposed existing duplicate role seed failures in auth/rate-limit fixtures; the focused viewer API tests and full application-layer tests pass.

## In Progress

- V2 Phase 1.5 unified auth cleanup and shared-ui primitive implementation are complete without Playwright. The browser runtime no longer exposes the `.NET` chat-token endpoint or viewer exchange endpoint, FastAPI validates CSRF locally before chat/feedback session validation, docs links now use session-authenticated `documentId` locators, and `packages/shared-ui` now provides the Phase 1.5.5 form, overlay, data, feedback, markdown, chat, and citation primitives with colocated tests.

## Next Up

- Continue V2 Phase 1.5.6 by applying the completed shared-ui primitives to the SPAs where local duplicated form, overlay, table, empty/loading, markdown, chat, and citation components still exist. Then proceed to Phase 1.7 UX refactors.
  The management-heavy sub-batch for users/groups dialogs, document forms/editor, and
  document tables is now complete; the remaining Phase 1.5.6 work should be a final
  low-risk shared-primitive sweep before Phase 1.7.

## Next Implementation Checkpoint

### Checkpoint Name

- Task 17.5 UI stabilization, first-run setup, and product polish.

### Why This Comes Next

- Task 17 end-to-end MVP verification is implemented and verified.
- User acceptance review found that the current MVP is technically verifiable but not usable from a clean browser session.
- The next safe step is Task 17.5, not Task 18, because documentation should not freeze a product flow that still requires hidden SQL seeding and has incomplete login/register UI.

### Scope

- Implement Task 17.5 only.
- Add first-run setup API, real login/bootstrap UI, local demo seed support, product UI polish based on the Stitch design reference, and first-run E2E verification.

### User-Owned Steps

- Keep Docker Desktop running when implementation resumes.
- If a clean first-run test is needed, decide whether local Compose volumes may be removed with `docker compose --env-file infra/compose/.env.example -f infra/compose/compose.yaml -f infra/compose/compose.override.yaml down -v`.

### Required Verification

- `dotnet test services/dotnet-api/AdvancedRag.sln`
- `Set-Location services/rag-api; uv run pytest -q; Set-Location ..\..`
- `pnpm -r test -- --run`
- `pnpm -r typecheck`
- `pnpm -r build`
- `docker compose --env-file infra/compose/.env.example -f infra/compose/compose.yaml config`
- `docker compose --env-file infra/compose/.env.example -f infra/compose/compose.yaml -f infra/compose/compose.override.yaml up -d --build`
- `pnpm --dir tests/e2e test`

Expected result:

- Existing unit/integration/E2E checks remain green, a clean deployment can create the first admin from the browser, and the UI reaches credible MVP demo quality.

## Open Questions

- Management chat preview needs product/architecture approval: either add a `.NET`-controlled management preview endpoint that calls FastAPI with authorized preview scope, or explicitly allow a same-origin manage route to FastAPI for preview-only chat. Direct `manage-web` calls to FastAPI are not allowed under the current service-boundary rules.

## Architecture Decisions

See `context/architecture.md`, `context/code-standards.md`, `context/rag-spec.md`, `context/ui-context.md`, and `context/design-decisions.md`. This tracker no longer duplicates the decisions; those files are the source of truth.

## Session Notes

- Conversation can continue in Spanish, but project artifacts must stay in English.
- 2026-05-27 v2 note: Runtime locale support was narrowed to `es-AR` and `en-US`
  only. Portuguese frontend resources, selector options, and RAG prompt files were
  removed; unsupported Portuguese locale requests now fall back to English prompts
  and English no-results text. Focused SPA tests verify ES/EN switching plus the
  absence of PT. Remaining feature-level hardcoded literals should be migrated
  into `src/i18n/` as each surface is touched.
- 2026-05-26 v2 note: FastAPI browser-path tests were aligned with the unified
  `session_validator` seam. Active v2 status remains in `context/v2-progress.md`; this
  MVP tracker is historical.
- 2026-05-26 v2 note: Deprecated viewer exchange/audit tables were removed from the
  current EF model and initial app-schema migration after the user decided unused tables
  should not remain in the migration. Verified with focused .NET build, viewer service,
  EF mapping, and EF migration tests. This note was superseded by the shared-ui
  primitive completion note below.
- 2026-05-26 v2 note: Phase 1.5 shared-ui primitives were completed in
  `packages/shared-ui` with colocated Vitest tests for form, overlay, data, feedback,
  markdown, chat, and citation components. Verified with package-local shared-ui tests
  and typecheck. Active v2 next work is applying these primitives to the SPAs.
- 2026-05-26 v2 note: Phase 1.5.6 first SPA primitive adoption pass applied shared
  primitives to `chat-web`, `docs-web`, and the lower-risk `manage-web` audit/feedback
  tables. Remaining Phase 1.5.6 work is the management-heavy user/group dialogs,
  document list/editor forms, and document tables before Phase 1.7 UX refactors.
- 2026-05-26 v2 note: The next Phase 1.5.6 sub-batch is intentionally limited to
  users/groups dialogs, document forms/editor, and document tables because these surfaces
  are more stateful and should be migrated with focused tests for dialog behavior,
  validation/submission, editor state, and table rendering.
- 2026-05-26 v2 note: Phase 1.5.6 management-heavy primitive adoption completed:
  users/groups budget, user, and group dialogs now use shared `Dialog`; users/groups and
  document tables use shared `DataTable`; document editor text fields and group access
  checkboxes use shared `Input` and `Checkbox`; `DataTable` now supports accessible
  row-header columns. Verified with focused manage-web/shared-ui tests, typechecks, and
  manage-web build.
- 2026-05-26 v2 note: Phase 0 reconciliation for the active v2 closure plan removed
  obsolete browser chat-token and viewer-exchange references from active E2E/API tests
  and operational secrets docs, reconciled `docs/v2/03-phases.md` and
  `context/v2-progress.md`, fixed active mojibake regressions, and passed focused
  shared-ui, SPA, FastAPI, and .NET verification. Active v2 status remains in
  `context/v2-progress.md`.
- 2026-05-27 v2 note: Phase 1.7 login consistency pass extracted the management login
  frame into shared `AuthShell`/`AuthCardHeader` primitives and reused it in
  manage/chat/docs auth surfaces. Active v2 status remains in `context/v2-progress.md`.
- 2026-05-27 v2 note: Phase 1.7 chat UX pass added the three-pane chat workspace with
  local conversations, citation rail/drawer, and command palette. Dimension filter chips
  remain pending until dimensions API/UI work is available. Active v2 status remains in
  `context/v2-progress.md`.
- 2026-05-27 v2 note: Chat streaming cursor animation is complete in the shared
  `ChatMessage` pending state and is rendered by `chat-web` while a response is being
  prepared. Dimension filter chips remain blocked on dimensions API/UI.
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

- Human-in-the-loop implementation is now required for MVP execution.
- The implementation branch is `mvp-implementation`.
- Task 0 repository baseline is complete and committed.
- Task 1 infrastructure and Caddy baseline is complete and committed.
- Task 2 .NET API foundation is complete and committed with the scoped foundation changes.
- Task 3 FastAPI RAG foundation is complete and committed with the scoped foundation changes.
- Task 4 frontend foundation is complete and committed with the scoped foundation changes.
- Task 5 shared error, request ID, and frontend error parsing contracts are complete and committed with the scoped foundation changes.
- Task 6 initial database schemas are complete and committed with the scoped foundation changes.
- Task 7 authentication foundation is complete and committed with the scoped foundation changes.
- Task 8 users, groups, access scope, and AI budget configuration is complete and committed with the scoped foundation changes.
- Task 9 document lifecycle and assisted imports is complete and committed.
- Task 10 internal indexing pipeline is complete and committed.
- Task 11 chat/RAG core is complete and committed.
- Task 11.5 backend readability refactor audit is complete and committed.
- Task 12 feedback and management reporting is complete and committed.
- Task 13 viewer exchange and document viewer is complete and committed.
- Task 14 chat frontend workflow is complete and committed.
- Task 15 management frontend workflow is complete and committed.
- Task 16 operational hardening is complete and committed.
- Task 17 end-to-end MVP verification is complete and committed.
- Task 17.5 UI stabilization is partially implemented and locally verified: first-run setup API, management setup/login/user/group UI, local demo seed script, first-run Playwright spec, chat/docs polish, and the 2026-05-20 management navigation/document/audit refinements are in place.
- Task 17.5 still needs the user-owned Compose startup checkpoint, then Playwright E2E and browser visual verification.

Next safe implementation work:

- Do not start Task 18 until Task 17.5 Playwright E2E and browser verification pass against the local Compose stack.
