# MVP Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the MVP foundation for the Advanced RAG Instruction Platform as a sellable single-tenant Docker Compose product with secure document management, RAG chat, token-gated document viewing, audit, AI budgets, and operational controls.

**Architecture:** The system is a monorepo with three React TypeScript frontends, a .NET 8 management API, a FastAPI RAG service, PostgreSQL with `app` and `rag` schemas, Caddy same-origin routing, Docker Compose deployment, secure cookie-based browser sessions, and OpenAI-backed chat/embeddings. Implementation is split into independently verifiable phases so each service boundary is tested before dependent workflows are layered on.

**Tech Stack:** React TypeScript, Tailwind CSS, `shadcn/ui`, `lucide-react`, .NET 8, EF Core, FastAPI, `uv`, Alembic, PostgreSQL with pgvector, Docker Compose, Caddy, OpenAI `gpt-4.1-mini`, OpenAI `text-embedding-3-large` with `OPENAI_EMBEDDING_DIMENSIONS=1536`.

---

## Required References

- Architecture spec: `docs/superpowers/specs/2026-05-11-base-architecture-design.md`
- Product context: `context/project-overview.md`
- Architecture context: `context/architecture.md`
- Code standards: `context/code-standards.md`
- UI context: `context/ui-context.md`
- Workflow rules: `context/ai-workflow-rules.md`
- Progress tracker: `context/progress-tracker.md`
- Design decisions: `context/design-decisions.md`

## Execution Rules

- Use TDD for application behavior. Write the failing test first, verify it fails for the expected reason, implement minimal code, verify it passes, then refactor.
- Keep each commit scoped to one task or one coherent subtask.
- Do not store real secrets in the repo.
- Keep durable project artifacts in English.
- Update `context/progress-tracker.md` after each meaningful phase.
- Update `context/design-decisions.md` if implementation confirms or changes a significant architecture decision.
- If a requirement is ambiguous, stop, add the open question to `context/progress-tracker.md`, and ask before coding.
- Because this workspace currently has no `.git` directory, execution starts with repository initialization unless the user points to a different Git repository.

## Phase Gates

| Gate | Required Evidence |
| --- | --- |
| Repository baseline | `git status`, root lint/test commands documented, no secrets committed |
| Infrastructure baseline | Compose config validates, Caddy routes are declared, health checks defined |
| Backend foundations | .NET and FastAPI health tests pass |
| Database foundations | EF Core and Alembic migrations create owned schemas without cross-schema writes |
| Auth foundation | Cookie, CSRF, chat token, and token validation tests pass |
| Document lifecycle | Draft/review/publish/archive/restore tests pass |
| RAG foundation | Indexing, retrieval scope, audit, cost, cache, and budget tests pass |
| Frontends | Core workflows render loading, empty, error, disabled, and success states |
| End-to-end | Compose stack supports login, document creation, publish/index, chat, citation open, feedback, and budget block |

## File And Responsibility Map

### Root

- `README.md` - developer entry point and local run commands.
- `.gitignore` - excludes build output, local secrets, logs, node modules, Python virtualenvs, and IDE files.
- `.editorconfig` - shared whitespace and line ending rules.
- `.gitattributes` - text normalization.
- `package.json` - monorepo scripts for frontend workspaces.
- `pnpm-workspace.yaml` - frontend workspace registration.
- `.node-version` - Node runtime hint.
- `docs/superpowers/plans/2026-05-11-mvp-implementation-plan.md` - this plan.

### Infrastructure

- `infra/compose/compose.yaml` - MVP service orchestration.
- `infra/compose/compose.override.yaml` - local development overrides.
- `infra/compose/Caddyfile` - public host routing and same-origin `/api/*` routes.
- `infra/compose/.env.example` - non-sensitive config defaults.
- `infra/compose/secrets/README.md` - local secret file instructions without secret values.
- `infra/compose/health/` - optional health helper scripts if Compose health checks need shell wrappers.

### .NET API

- `services/dotnet-api/AdvancedRag.sln` - solution.
- `services/dotnet-api/src/AdvancedRag.Api/` - ASP.NET Core API host.
- `services/dotnet-api/src/AdvancedRag.App/` - application use cases, authorization, validation.
- `services/dotnet-api/src/AdvancedRag.Domain/` - domain entities and value objects.
- `services/dotnet-api/src/AdvancedRag.Infrastructure/` - EF Core, logging, secrets, integrations.
- `services/dotnet-api/tests/AdvancedRag.Api.Tests/` - HTTP/API behavior tests.
- `services/dotnet-api/tests/AdvancedRag.App.Tests/` - use case and domain tests.
- `services/dotnet-api/tests/AdvancedRag.Infrastructure.Tests/` - database and integration boundary tests.

### FastAPI RAG Service

- `services/rag-api/pyproject.toml` - Python package and tooling.
- `services/rag-api/uv.lock` - locked Python dependencies after dependency installation.
- `services/rag-api/alembic.ini` - Alembic config.
- `services/rag-api/alembic/` - `rag` schema migrations.
- `services/rag-api/src/advanced_rag/main.py` - FastAPI app factory.
- `services/rag-api/src/advanced_rag/api/` - HTTP routers.
- `services/rag-api/src/advanced_rag/core/` - config, logging, security, errors.
- `services/rag-api/src/advanced_rag/rag/` - retrieval, chunking, embeddings, cache, indexing.
- `services/rag-api/src/advanced_rag/audit/` - query audit, cost snapshots, citations, feedback.
- `services/rag-api/src/advanced_rag/budget/` - AI budget enforcement.
- `services/rag-api/tests/` - pytest suite.

### Frontends

- `apps/manage-web/` - management frontend.
- `apps/chat-web/` - chat frontend.
- `apps/docs-web/` - document viewer frontend.
- `apps/*/src/api/` - typed API clients.
- `apps/*/src/components/` - local UI components.
- `apps/*/src/routes/` - route-level screens.
- `apps/*/src/lib/` - auth, formatting, error helpers.
- `apps/*/src/test/` - frontend test setup.

## Task 0: Repository Baseline

**Files:**
- Create: `.gitignore`
- Create: `.editorconfig`
- Create: `.gitattributes`
- Create: `README.md`
- Create: `.node-version`
- Create: `package.json`
- Create: `pnpm-workspace.yaml`
- Modify: `context/progress-tracker.md`

- [x] **Step 1: Initialize Git if needed**

Run:

```powershell
git rev-parse --is-inside-work-tree
```

Expected if repository is missing: command fails with `fatal: not a git repository`.

If missing, run:

```powershell
git init
```

Expected: a new `.git` directory exists.

- [x] **Step 2: Create root ignore/config files**

Create `.gitignore` with entries for:

```gitignore
# Dependencies
node_modules/
.venv/
__pycache__/

# Build outputs
dist/
build/
bin/
obj/
coverage/
TestResults/

# Local config and secrets
.env
.env.*
!.env.example
infra/compose/secrets/*
!infra/compose/secrets/README.md

# Logs
logs/
*.log

# IDE and OS
.vscode/
.idea/
.DS_Store
Thumbs.db
```

Create `.editorconfig`:

```editorconfig
root = true

[*]
charset = utf-8
end_of_line = crlf
insert_final_newline = true
indent_style = space
indent_size = 2
trim_trailing_whitespace = true

[*.cs]
indent_size = 4

[*.py]
indent_size = 4

[*.md]
trim_trailing_whitespace = false
```

Create `.gitattributes`:

```gitattributes
* text=auto
*.sh text eol=lf
*.ps1 text eol=crlf
```

- [x] **Step 3: Create root workspace metadata**

Create `.node-version`:

```text
22
```

Create `pnpm-workspace.yaml`:

```yaml
packages:
  - "apps/*"
```

Create root `package.json`:

```json
{
  "name": "advanced-rag-instruction-platform",
  "private": true,
  "packageManager": "pnpm@10",
  "scripts": {
    "lint": "pnpm -r lint",
    "test": "pnpm -r test",
    "typecheck": "pnpm -r typecheck",
    "build": "pnpm -r build"
  }
}
```

- [x] **Step 4: Create README baseline**

Create `README.md` with:

```markdown
# Advanced RAG Instruction Platform

Single-tenant corporate instruction management and RAG platform.

## Current Status

The project is in implementation planning. The approved base architecture spec lives at:

- `docs/superpowers/specs/2026-05-11-base-architecture-design.md`

## Planned Services

- `apps/manage-web` - management frontend
- `apps/chat-web` - chat frontend
- `apps/docs-web` - instruction viewer frontend
- `services/dotnet-api` - .NET 8 management API
- `services/rag-api` - FastAPI RAG service
- `infra/compose` - Docker Compose deployment

## Local Development

Local setup commands are added as services are scaffolded.
Real secrets must not be committed.
```

- [x] **Step 5: Verify baseline**

Run:

```powershell
git status --short
```

Expected: only planned baseline files and existing context/docs files are listed.

- [x] **Step 6: Commit baseline**

Run:

```powershell
git add .gitignore .editorconfig .gitattributes README.md .node-version package.json pnpm-workspace.yaml docs context
git commit -m "chore: establish repository baseline"
```

Expected: commit succeeds.

## Task 1: Infrastructure And Configuration Baseline

**Files:**
- Create: `infra/compose/compose.yaml`
- Create: `infra/compose/compose.override.yaml`
- Create: `infra/compose/Caddyfile`
- Create: `infra/compose/.env.example`
- Create: `infra/compose/secrets/README.md`
- Create: `infra/compose/postgres-init/init.sh`
- Create: `infra/compose/postgres-init/init.sql`

**Implementation note:** Task 1 follows the current `context/architecture.md` and `context/code-patterns.md` rules, which supersede the older inline skeleton below where they differ. The implemented baseline includes `postgres-init`, `csrf_signing_key`, `jwt_signing_keys.json`, and separate Postgres role password secrets.

- [x] **Step 1: Create non-sensitive environment defaults**

Create `infra/compose/.env.example`:

```dotenv
COMPOSE_PROJECT_NAME=advanced-rag
CUSTOMER_TIMEZONE=UTC
POSTGRES_DB=advanced_rag
POSTGRES_APP_USER=advanced_rag_app
POSTGRES_RAG_USER=advanced_rag_rag
OPENAI_CHAT_MODEL=gpt-4.1-mini
OPENAI_EMBEDDING_MODEL=text-embedding-3-large
OPENAI_EMBEDDING_DIMENSIONS=1536
RAG_SEMANTIC_CACHE_TTL_HOURS=24
RAG_SEMANTIC_CACHE_SIMILARITY_THRESHOLD=0.90
AI_DEFAULT_MONTHLY_BUDGET_USD=5.00
TECH_LOG_RETENTION_DAYS=30
CHAT_MAX_QUESTION_CHARS=4000
CHAT_FEEDBACK_COMMENT_MAX_CHARS=1000
REVIEW_COMMENT_MAX_CHARS=2000
```

- [x] **Step 2: Create secret instructions**

Create `infra/compose/secrets/README.md`:

```markdown
# Local Compose Secrets

Create these files locally before validating or running the Compose stack:

- `postgres_admin_password.txt`
- `postgres_app_password.txt`
- `postgres_rag_password.txt`
- `postgres_reporting_password.txt`
- `openai_api_key.txt`
- `jwt_signing_keys.json`
- `csrf_signing_key.txt`
- `internal_service_token.txt`

Do not commit secret values. This directory is ignored except for this README.
```

- [x] **Step 3: Create Compose skeleton**

Create `infra/compose/compose.yaml` with services for `postgres`, `postgres-init`, `dotnet-api`, `rag-api`, `manage-web`, `chat-web`, `docs-web`, and `caddy`.

The implemented file keeps later build contexts declared, uses `postgres-init` gated by Postgres health, mounts Compose secrets as files, and wires service-specific database password file paths instead of putting database passwords in environment variables.

- [x] **Step 4: Create Caddy route skeleton**

Create `infra/compose/Caddyfile` for `manage.localhost`, `chat.localhost`, and `docs.localhost`.

The implemented Caddyfile uses `tls internal` for local HTTPS and preserves `/api/*` path prefixes when proxying to the backends.

- [x] **Step 5: Validate Compose syntax**

Run:

```powershell
docker compose --env-file infra/compose/.env.example -f infra/compose/compose.yaml config
```

Expected: Compose renders configuration. Build contexts may not exist until later tasks; syntax must still be valid.

- [x] **Step 6: Commit infrastructure baseline**

Run:

```powershell
git add infra/compose
git commit -m "chore: add compose and caddy baseline"
```

Expected: commit succeeds.

## Task 2: .NET API Foundation

**Files:**
- Create: `global.json`
- Create: `services/dotnet-api/AdvancedRag.sln`
- Create: `services/dotnet-api/src/AdvancedRag.Api/`
- Create: `services/dotnet-api/src/AdvancedRag.App/`
- Create: `services/dotnet-api/src/AdvancedRag.Domain/`
- Create: `services/dotnet-api/src/AdvancedRag.Infrastructure/`
- Create: `services/dotnet-api/tests/AdvancedRag.Api.Tests/`
- Create: `services/dotnet-api/tests/AdvancedRag.App.Tests/`
- Create: `services/dotnet-api/Dockerfile`
- Create: `services/dotnet-api/.dockerignore`

- [x] **Step 1: Scaffold solution and projects**

Run:

```powershell
New-Item -ItemType Directory -Force -Path services/dotnet-api/src,services/dotnet-api/tests
dotnet new sln -o services/dotnet-api -n AdvancedRag
dotnet new webapi -o services/dotnet-api/src/AdvancedRag.Api -n AdvancedRag.Api
dotnet new classlib -o services/dotnet-api/src/AdvancedRag.App -n AdvancedRag.App
dotnet new classlib -o services/dotnet-api/src/AdvancedRag.Domain -n AdvancedRag.Domain
dotnet new classlib -o services/dotnet-api/src/AdvancedRag.Infrastructure -n AdvancedRag.Infrastructure
dotnet new xunit -o services/dotnet-api/tests/AdvancedRag.Api.Tests -n AdvancedRag.Api.Tests
dotnet new xunit -o services/dotnet-api/tests/AdvancedRag.App.Tests -n AdvancedRag.App.Tests
dotnet sln services/dotnet-api/AdvancedRag.sln add services/dotnet-api/src/AdvancedRag.Api/AdvancedRag.Api.csproj
dotnet sln services/dotnet-api/AdvancedRag.sln add services/dotnet-api/src/AdvancedRag.App/AdvancedRag.App.csproj
dotnet sln services/dotnet-api/AdvancedRag.sln add services/dotnet-api/src/AdvancedRag.Domain/AdvancedRag.Domain.csproj
dotnet sln services/dotnet-api/AdvancedRag.sln add services/dotnet-api/src/AdvancedRag.Infrastructure/AdvancedRag.Infrastructure.csproj
dotnet sln services/dotnet-api/AdvancedRag.sln add services/dotnet-api/tests/AdvancedRag.Api.Tests/AdvancedRag.Api.Tests.csproj
dotnet sln services/dotnet-api/AdvancedRag.sln add services/dotnet-api/tests/AdvancedRag.App.Tests/AdvancedRag.App.Tests.csproj
```

Expected: solution exists and `dotnet sln list` shows six projects.

- [x] **Step 2: Add project references**

Run:

```powershell
dotnet add services/dotnet-api/src/AdvancedRag.App/AdvancedRag.App.csproj reference services/dotnet-api/src/AdvancedRag.Domain/AdvancedRag.Domain.csproj
dotnet add services/dotnet-api/src/AdvancedRag.Infrastructure/AdvancedRag.Infrastructure.csproj reference services/dotnet-api/src/AdvancedRag.App/AdvancedRag.App.csproj services/dotnet-api/src/AdvancedRag.Domain/AdvancedRag.Domain.csproj
dotnet add services/dotnet-api/src/AdvancedRag.Api/AdvancedRag.Api.csproj reference services/dotnet-api/src/AdvancedRag.App/AdvancedRag.App.csproj services/dotnet-api/src/AdvancedRag.Infrastructure/AdvancedRag.Infrastructure.csproj
dotnet add services/dotnet-api/tests/AdvancedRag.Api.Tests/AdvancedRag.Api.Tests.csproj reference services/dotnet-api/src/AdvancedRag.Api/AdvancedRag.Api.csproj
dotnet add services/dotnet-api/tests/AdvancedRag.App.Tests/AdvancedRag.App.Tests.csproj reference services/dotnet-api/src/AdvancedRag.App/AdvancedRag.App.csproj services/dotnet-api/src/AdvancedRag.Domain/AdvancedRag.Domain.csproj
```

Expected: references are added without circular dependencies.

- [x] **Step 3: Add health endpoint failing test**

Create an API test that calls `/health/live` and expects HTTP 200 with a JSON body containing `status: "ok"`.

Run:

```powershell
dotnet test services/dotnet-api/AdvancedRag.sln --filter Health
```

Expected before implementation: test fails because `/health/live` is not wired.

- [x] **Step 4: Add minimal health endpoint**

Modify `services/dotnet-api/src/AdvancedRag.Api/Program.cs` to expose `/health/live` and `/health/ready`. `/health/live` returns process liveness. `/health/ready` initially returns ready without DB checks; DB readiness is added after EF Core is configured.

- [x] **Step 5: Verify .NET foundation**

Run:

```powershell
dotnet test services/dotnet-api/AdvancedRag.sln
dotnet build services/dotnet-api/AdvancedRag.sln
```

Expected: tests and build pass.

- [x] **Step 6: Add .NET Dockerfile**

Create `services/dotnet-api/Dockerfile` as a multi-stage .NET 8 build that publishes `AdvancedRag.Api` and runs it on port `8080`.

- [x] **Step 7: Commit .NET foundation**

Run:

```powershell
git add services/dotnet-api
git commit -m "feat: scaffold dotnet api foundation"
```

Expected: commit succeeds.

## Task 3: FastAPI RAG Foundation

**Files:**
- Create: `services/rag-api/.python-version`
- Create: `services/rag-api/.dockerignore`
- Create: `services/rag-api/pyproject.toml`
- Create: `services/rag-api/uv.lock`
- Create: `services/rag-api/README.md`
- Create: `services/rag-api/src/advanced_rag/main.py`
- Create: `services/rag-api/src/advanced_rag/core/config.py`
- Create: `services/rag-api/src/advanced_rag/core/errors.py`
- Create: `services/rag-api/tests/test_health.py`
- Create: `services/rag-api/tests/test_config.py`
- Create: `services/rag-api/tests/test_errors.py`
- Create: `services/rag-api/Dockerfile`
- Modify: `.gitignore`

- [x] **Step 1: Scaffold Python package with uv**

Run:

```powershell
New-Item -ItemType Directory -Force -Path services/rag-api/src/advanced_rag/core,services/rag-api/tests
Set-Location services/rag-api
uv init --package --name advanced-rag-rag-api
uv add fastapi uvicorn pydantic-settings sqlalchemy asyncpg alembic openai pyjwt structlog
uv add --dev pytest pytest-asyncio httpx ruff mypy
Set-Location ..\..
```

Expected: `pyproject.toml` and `uv.lock` exist.

- [x] **Step 2: Write failing health test**

Create `services/rag-api/tests/test_health.py` with tests for `/health/live` and `/health/ready`.

Run:

```powershell
Set-Location services/rag-api
uv run pytest tests/test_health.py -q
Set-Location ..\..
```

Expected before implementation: tests fail because the app is not implemented.

- [x] **Step 3: Implement app factory and health endpoints**

Create `services/rag-api/src/advanced_rag/main.py` with a FastAPI app exposing `/health/live` and `/health/ready`. Both return JSON `{ "status": "ok" }` until database readiness is added.

- [x] **Step 4: Verify FastAPI foundation**

Run:

```powershell
Set-Location services/rag-api
uv run pytest -q
uv run ruff check .
Set-Location ..\..
```

Expected: tests and lint pass.

- [x] **Step 5: Add FastAPI Dockerfile**

Create `services/rag-api/Dockerfile` using `uv` to sync dependencies and run `uvicorn advanced_rag.main:app --host 0.0.0.0 --port 8000`.

- [x] **Step 6: Commit FastAPI foundation**

Run:

```powershell
git add services/rag-api
git commit -m "feat: scaffold fastapi rag foundation"
```

Expected: commit succeeds.

## Task 4: Frontend Foundations

**Files:**
- Create: `apps/manage-web/`
- Create: `apps/chat-web/`
- Create: `apps/docs-web/`
- Create: each app's `Dockerfile`
- Create: each app's `src/lib/api-error.ts`
- Create: each app's `src/test/setup.ts`
- Create: each app's `components.json`
- Create: each app's `src/lib/utils.ts`
- Create: each app's `src/components/ui/button.tsx`

- [x] **Step 1: Scaffold Vite apps**

Run:

```powershell
pnpm create vite apps/manage-web --template react-ts
pnpm create vite apps/chat-web --template react-ts
pnpm create vite apps/docs-web --template react-ts
```

Expected: all three apps contain React TypeScript Vite scaffolds.

- [x] **Step 2: Install shared frontend dependencies**

Run:

```powershell
pnpm --dir apps/manage-web add lucide-react
pnpm --dir apps/chat-web add lucide-react
pnpm --dir apps/docs-web add lucide-react
pnpm --dir apps/manage-web add -D tailwindcss @tailwindcss/vite vitest jsdom @testing-library/react @testing-library/user-event @testing-library/jest-dom
pnpm --dir apps/chat-web add -D tailwindcss @tailwindcss/vite vitest jsdom @testing-library/react @testing-library/user-event @testing-library/jest-dom
pnpm --dir apps/docs-web add -D tailwindcss @tailwindcss/vite vitest jsdom @testing-library/react @testing-library/user-event @testing-library/jest-dom
```

Expected: package manifests update and installs complete.

- [x] **Step 3: Add shadcn/ui to each app**

Run the shadcn init command in each app and choose Tailwind CSS, TypeScript, CSS variables, and the app-local component path `src/components/ui`.

Expected: each app has `components.json`, base CSS variables, and `src/components/ui` support files.

- [x] **Step 4: Write failing smoke tests**

For each app, create a test that renders the root app and expects a product-specific shell label:

- Management: `Instruction Management`
- Chat: `Instruction Chat`
- Docs: `Instruction Viewer`

Run:

```powershell
pnpm -r test -- --run
```

Expected before implementation: tests fail because shells are not rendered.

- [x] **Step 5: Implement minimal shells**

Replace default Vite content with minimal shells using Tailwind utility classes and no marketing hero sections.

- [x] **Step 6: Add frontend Dockerfiles**

Each frontend Dockerfile builds the Vite app and serves static output with nginx or Caddy on port `80`.

- [x] **Step 7: Verify frontend foundation**

Run:

```powershell
pnpm -r typecheck
pnpm -r test -- --run
pnpm -r build
```

Expected: typecheck, tests, and builds pass.

- [x] **Step 8: Commit frontend foundation**

Run:

```powershell
git add apps package.json pnpm-workspace.yaml
git commit -m "feat: scaffold frontend foundations"
```

Expected: commit succeeds.

## Task 5: Shared Error, Config, And Logging Contracts

**Files:**
- Modify: `services/dotnet-api/src/AdvancedRag.Api/Program.cs`
- Create: `services/dotnet-api/src/AdvancedRag.Api/Errors/ErrorResponse.cs`
- Create: `services/dotnet-api/src/AdvancedRag.Api/Middleware/RequestIdMiddleware.cs`
- Create: `services/rag-api/src/advanced_rag/core/errors.py`
- Create: `services/rag-api/src/advanced_rag/core/request_id.py`
- Create: `apps/*/src/lib/api-error.ts`

- [x] **Step 1: Write .NET error envelope tests**

Test that an unknown route or forced validation endpoint returns:

```json
{
  "error": {
    "code": "NOT_FOUND",
    "message": "Resource not found.",
    "details": {},
    "requestId": "non-empty"
  }
}
```

Run:

```powershell
dotnet test services/dotnet-api/AdvancedRag.sln --filter ErrorEnvelope
```

Expected before implementation: tests fail.

- [x] **Step 2: Implement .NET shared error envelope and request IDs**

Add middleware that creates or propagates `X-Request-Id`, adds it to responses, and uses it in safe error responses.

- [x] **Step 3: Write FastAPI error envelope tests**

Test that invalid routes and explicit validation errors use the same envelope shape.

Run:

```powershell
Set-Location services/rag-api
uv run pytest tests -k error_envelope -q
Set-Location ..\..
```

Expected before implementation: tests fail.

- [x] **Step 4: Implement FastAPI error handlers and request ID propagation**

Add exception handlers for HTTP errors and validation errors. Add middleware for `X-Request-Id`.

- [x] **Step 5: Add frontend error parser tests**

For each app, test that `parseApiError()` extracts `code`, `message`, `details`, and `requestId` from the shared envelope and returns a safe fallback for malformed responses.

- [x] **Step 6: Verify shared error behavior**

Run:

```powershell
dotnet test services/dotnet-api/AdvancedRag.sln
Set-Location services/rag-api; uv run pytest -q; Set-Location ..\..
pnpm -r test -- --run
```

Expected: all tests pass.

- [x] **Step 7: Commit shared contracts**

Run:

```powershell
git add services apps
git commit -m "feat: add shared error and request id contracts"
```

Expected: commit succeeds.

## Task 6: Database Migrations And Schema Ownership

**Files:**
- Modify: `services/dotnet-api/src/AdvancedRag.Infrastructure/`
- Create: `services/dotnet-api/src/AdvancedRag.Infrastructure/Persistence/AppDbContext.cs`
- Create: `services/rag-api/alembic/`
- Create: `services/rag-api/src/advanced_rag/db/`
- Create: EF Core migrations for `app`
- Create: Alembic migrations for `rag`

- [ ] **Step 1: Add app schema migration tests**

Write .NET tests that verify EF Core maps these `app` entities and schema names:

- `users`
- `roles`
- `user_roles`
- `groups`
- `user_groups`
- `instructions`
- `instruction_versions`
- `instruction_permissions`
- `instruction_tags`
- `review_comments`
- `import_metadata`
- `viewer_exchange_codes`
- `viewer_token_audit`
- `user_ai_budget_limits`
- `audit_events`

Expected before implementation: tests fail because mappings do not exist.

- [ ] **Step 2: Implement .NET domain entities and EF mappings**

Create focused domain entities and EF configurations. Enforce `app` schema in mappings. Do not map any `rag` table as writable.

- [ ] **Step 3: Generate EF migration**

Run:

```powershell
dotnet ef migrations add InitialAppSchema --project services/dotnet-api/src/AdvancedRag.Infrastructure --startup-project services/dotnet-api/src/AdvancedRag.Api --context AppDbContext
```

Expected: migration creates only `app` schema objects.

- [ ] **Step 4: Add rag schema migration tests**

Write FastAPI/Alembic tests or migration assertions that verify these `rag` tables:

- `indexing_jobs`
- `document_chunks` with `embedding vector(1536)`
- `semantic_cache_entries`
- `semantic_cache_sources`
- `query_audit_events`
- `query_audit_citations`
- `model_pricing`

Expected before migration: tests fail because tables do not exist.

- [ ] **Step 5: Implement Alembic migration**

Initialize Alembic and create an initial migration that:

- Creates `rag` schema.
- Enables `vector` extension.
- Creates `document_chunks.embedding` as `vector(1536)`.
- Adds indexes needed for document version lookup, corpus lookup, access scope lookup, audit date filtering, budget user/month filtering, and citation document filtering.

- [ ] **Step 6: Verify migration ownership**

Run migration tests against a disposable Postgres container.

Expected:

- .NET migration writes only `app`.
- Alembic migration writes only `rag` plus required extension setup.
- Cross-schema access is read-only or through explicit contracts.

- [ ] **Step 7: Commit database foundations**

Run:

```powershell
git add services/dotnet-api services/rag-api
git commit -m "feat: add initial database schemas"
```

Expected: commit succeeds.

## Task 7: Auth, Cookies, CSRF, And Chat Token Flow

**Files:**
- Modify: `.NET` auth/session modules
- Modify: FastAPI security modules
- Modify: Caddy route support if needed
- Test: `.NET` API tests and FastAPI auth tests

- [ ] **Step 1: Write .NET auth tests**

Cover:

- Login sets `HttpOnly`, `Secure`, `SameSite`, host-only-compatible cookie attributes.
- Logout clears session cookie.
- Mutating requests without CSRF are rejected.
- Session status returns current user role and groups.
- `/api/auth/chat-token` requires main session and sets a chat token cookie for `chat.client.com` route context.

Expected before implementation: tests fail.

- [ ] **Step 2: Implement local user auth and secure sessions**

Implement password hashing, login, logout, current session, CSRF token support, and session cookie options. Keep controllers thin and move behavior into app services.

- [ ] **Step 3: Write FastAPI token validation tests**

Cover:

- Valid signed chat token is accepted.
- Expired token is rejected.
- Wrong issuer/audience is rejected.
- Missing `access_scope_hash` is rejected.
- Disabled or stale user state is not introspected on every chat request in MVP.

Expected before implementation: tests fail.

- [ ] **Step 4: Implement FastAPI local signed token validation**

Implement validation using configured issuer, audience, signing key/public key, and expected claims.

- [ ] **Step 5: Verify auth flow**

Run:

```powershell
dotnet test services/dotnet-api/AdvancedRag.sln --filter Auth
Set-Location services/rag-api; uv run pytest tests -k auth -q; Set-Location ..\..
```

Expected: all auth tests pass.

- [ ] **Step 6: Commit auth foundation**

Run:

```powershell
git add services/dotnet-api services/rag-api infra/compose/Caddyfile
git commit -m "feat: add secure auth and chat token flow"
```

Expected: commit succeeds.

## Task 8: Users, Groups, Access Scope, And AI Budget Configuration

**Files:**
- Modify: `.NET` users/groups modules
- Modify: `.NET` budget configuration modules
- Modify: `apps/manage-web` users/groups/budget screens
- Test: `.NET` app/API tests and management UI tests

- [ ] **Step 1: Write .NET use case tests for users/groups**

Cover:

- Admin creates user with default USD 5 monthly AI budget.
- Admin assigns roles.
- Admin assigns groups/departments.
- Deactivated user cannot get a new session.
- Effective access scope hash changes when groups change.

Expected before implementation: tests fail.

- [ ] **Step 2: Implement users/groups/budget domain**

Implement use cases for user creation, role assignment, group assignment, activation/deactivation, and budget configuration. Persist budget in `app.user_ai_budget_limits`.

- [ ] **Step 3: Write management UI tests**

Cover:

- Users table shows user, roles, groups, active status, monthly budget, current spend, remaining budget.
- Budget edit dialog validates non-negative monetary values.
- Saving a budget shows success state.
- API error envelope shows safe error state.

Expected before implementation: tests fail.

- [ ] **Step 4: Implement users/groups/budget management UI**

Build dense table/list layout with filters and edit dialogs using `shadcn/ui`.

- [ ] **Step 5: Verify users/groups/budget**

Run:

```powershell
dotnet test services/dotnet-api/AdvancedRag.sln --filter "Users|Groups|Budget"
pnpm --dir apps/manage-web test -- --run
```

Expected: tests pass.

- [ ] **Step 6: Commit users/groups/budget configuration**

Run:

```powershell
git add services/dotnet-api apps/manage-web
git commit -m "feat: add users groups and ai budget configuration"
```

Expected: commit succeeds.

## Task 9: Document Lifecycle And Assisted Imports

**Files:**
- Modify: `.NET` document modules
- Modify: `apps/manage-web` document screens
- Test: `.NET` lifecycle/import tests and management UI tests

- [ ] **Step 1: Write lifecycle domain tests**

Cover:

- Draft requires title, instruction type, allowed groups/departments, audience/user type, sanitized non-empty HTML before `In Review`.
- `DocumentManager` can send to review but cannot publish.
- `Admin` can request publish from `In Review`.
- Returning/rejecting from review requires comment.
- Editing a published instruction creates a new draft version.
- Archive/restore rules match the architecture spec.

Expected before implementation: tests fail.

- [ ] **Step 2: Implement lifecycle use cases**

Implement state transitions in application services with authorization checks, validation errors, audit events, and request IDs.

- [ ] **Step 3: Write import extraction tests**

Cover:

- DOCX extraction returns text and safe metadata.
- PDF extraction returns text and safe metadata.
- Files over 10 MB are rejected.
- Empty/scanned/no-text extraction returns `IMPORT_TEXT_NOT_EXTRACTABLE`.
- Unsaved extraction is not persisted as business data.

Expected before implementation: tests fail.

- [ ] **Step 4: Implement assisted import adapters**

Use `DocumentFormat.OpenXml` for DOCX and `PdfPig` for PDF. Return extracted text and safe metadata. Do not persist file bytes.

- [ ] **Step 5: Write management document UI tests**

Cover:

- Document list filters.
- Editor dirty state.
- Import loading/error/success states.
- Review validation errors.
- Indexing status display.
- Archive/restore actions.

Expected before implementation: tests fail.

- [ ] **Step 6: Implement management document UI**

Build document list, detail, editor, import control, lifecycle actions, and audit/status panels.

- [ ] **Step 7: Verify document lifecycle**

Run:

```powershell
dotnet test services/dotnet-api/AdvancedRag.sln --filter "Document|Import|Lifecycle"
pnpm --dir apps/manage-web test -- --run
```

Expected: tests pass.

- [ ] **Step 8: Commit document lifecycle**

Run:

```powershell
git add services/dotnet-api apps/manage-web
git commit -m "feat: add document lifecycle and assisted imports"
```

Expected: commit succeeds.

## Task 10: Internal Indexing Pipeline

**Files:**
- Modify: `.NET` publish/index integration
- Modify: FastAPI internal indexing modules
- Modify: `rag.indexing_jobs`, `rag.document_chunks`
- Test: .NET integration tests and FastAPI indexing tests

- [ ] **Step 1: Write .NET publish integration tests**

Cover:

- Publish request calls internal FastAPI indexing endpoint with internal service token.
- Document remains `In Review` while indexing is pending/running.
- Document transitions to `Published` only after success callback/status.
- Indexing failure leaves document unpublished and exposes safe management error.
- Manual retry creates or restarts an indexing job.

Expected before implementation: tests fail.

- [ ] **Step 2: Write FastAPI internal indexing tests**

Cover:

- Missing/invalid internal service token is rejected.
- Valid indexing request creates `rag.indexing_jobs`.
- Chunking stores chunks with `embedding vector(1536)`.
- Embedding requests pass configured `dimensions=1536`.
- Job failure stores safe technical status without exposing provider details publicly.

Expected before implementation: tests fail.

- [ ] **Step 3: Implement indexing contract**

Implement Docker-network-only endpoint in FastAPI and .NET client. Use idempotent document/version references so retry does not duplicate active chunks.

- [ ] **Step 4: Implement chunking and embedding storage**

Implement deterministic chunking, embedding call abstraction, and storage in `rag.document_chunks`. Tests should use fake embedding provider returning 1536-dimensional vectors.

- [ ] **Step 5: Verify indexing pipeline**

Run:

```powershell
dotnet test services/dotnet-api/AdvancedRag.sln --filter Indexing
Set-Location services/rag-api; uv run pytest tests -k indexing -q; Set-Location ..\..
```

Expected: tests pass.

- [ ] **Step 6: Commit indexing pipeline**

Run:

```powershell
git add services/dotnet-api services/rag-api
git commit -m "feat: add internal indexing pipeline"
```

Expected: commit succeeds.

## Task 11: Chat, Retrieval, Audit, Cache, Cost, And Budget Enforcement

**Files:**
- Modify: FastAPI chat/RAG modules
- Modify: `rag.query_audit_events`
- Modify: `rag.query_audit_citations`
- Modify: `rag.semantic_cache_entries`
- Modify: `rag.semantic_cache_sources`
- Test: FastAPI RAG tests

- [ ] **Step 1: Write retrieval access tests**

Cover:

- Public chat retrieves only `Published` corpus.
- Retrieval filters by `access_scope_hash`.
- Cache reuse requires matching `access_scope_hash`.
- Preview corpus is not used for normal viewer chat.

Expected before implementation: tests fail.

- [ ] **Step 2: Implement retrieval service**

Implement vector similarity retrieval over `rag.document_chunks` with corpus and access-scope filters.

- [ ] **Step 3: Write query audit tests**

Cover:

- Generated answer creates `rag.query_audit_events`.
- Citations create `rag.query_audit_citations`.
- Audit stores model IDs, embedding dimensions, token usage, price snapshot, estimated cost, latency, request ID, cache hit, and access scope hash.

Expected before implementation: tests fail.

- [ ] **Step 4: Implement audit and pricing**

Implement versioned `rag.model_pricing`, price snapshot selection, and query audit writes in a transaction.

- [ ] **Step 5: Write semantic cache tests**

Cover:

- Similar question above threshold returns cached answer with citations.
- Different access scope does not reuse cache.
- Source document invalidation deletes cache entries citing changed documents.
- New document creation does not invalidate existing cache.

Expected before implementation: tests fail.

- [ ] **Step 6: Implement semantic cache**

Implement cache lookup, write, TTL handling, source tracking, and invalidation hooks.

- [ ] **Step 7: Write AI budget tests**

Cover:

- Default monthly budget is USD 5.
- Current spend is calculated by customer calendar month using configured timezone.
- Over-budget user receives `AI_BUDGET_EXCEEDED`.
- Budget block happens before paid embedding or LLM calls when possible.
- Document viewer access is unaffected by budget exhaustion.

Expected before implementation: tests fail.

- [ ] **Step 8: Implement AI budget enforcement**

Implement budget check in FastAPI chat flow. Use `.NET`-owned budget configuration through an approved read path or internal budget snapshot contract. Do not create a FastAPI-owned budget configuration table.

- [ ] **Step 9: Verify chat/RAG behavior**

Run:

```powershell
Set-Location services/rag-api
uv run pytest tests -k "chat or retrieval or audit or cache or budget" -q
Set-Location ..\..
```

Expected: tests pass.

- [ ] **Step 10: Commit chat/RAG core**

Run:

```powershell
git add services/rag-api services/dotnet-api
git commit -m "feat: add chat rag audit cache and budget enforcement"
```

Expected: commit succeeds.

## Task 12: Feedback And Management Reporting

**Files:**
- Modify: FastAPI feedback endpoint
- Modify: .NET reporting read models
- Modify: `apps/chat-web` feedback UI
- Modify: `apps/manage-web` feedback review UI
- Test: FastAPI, .NET, and frontend tests

- [ ] **Step 1: Write feedback submission tests**

Cover:

- Authenticated user can submit thumbs up/down once per query audit event.
- Optional comment is sanitized and limited to 1000 characters.
- Duplicate feedback from the same user updates the same audit row's feedback value/comment and `feedback_updated_at`.
- Feedback cannot be submitted for another user's query.

Expected before implementation: tests fail. Reporting data must come from FastAPI-owned read-only views in the `rag` schema, exposed through the .NET API.

- [ ] **Step 2: Implement FastAPI feedback endpoint**

Store one feedback value/comment on `rag.query_audit_events`.

- [ ] **Step 3: Write .NET reporting tests**

Cover filters:

- Negative feedback.
- Cited document.
- User.
- Date range.

Expected before implementation: tests fail.

- [ ] **Step 4: Implement .NET read-only reporting**

Expose management reporting through .NET. Use FastAPI-owned read-only database views in the `rag` schema. Management frontend must not call FastAPI directly, and .NET must not write to `rag` tables.

- [ ] **Step 5: Write frontend tests**

Cover chat feedback states and management feedback review empty/filter/result states.

- [ ] **Step 6: Implement feedback UIs**

Build chat answer feedback controls and management feedback review table.

- [ ] **Step 7: Verify feedback/reporting**

Run:

```powershell
Set-Location services/rag-api; uv run pytest tests -k feedback -q; Set-Location ..\..
dotnet test services/dotnet-api/AdvancedRag.sln --filter Reporting
pnpm --dir apps/chat-web test -- --run
pnpm --dir apps/manage-web test -- --run
```

Expected: tests pass.

- [ ] **Step 8: Commit feedback and reporting**

Run:

```powershell
git add services/rag-api services/dotnet-api apps/chat-web apps/manage-web
git commit -m "feat: add chat feedback and management reporting"
```

Expected: commit succeeds.

## Task 13: Viewer Exchange And Document Viewer

**Files:**
- Modify: .NET viewer link/token modules
- Modify: `apps/docs-web`
- Modify: `apps/chat-web` citation link behavior
- Modify: `apps/manage-web` document open link behavior
- Test: .NET and frontend viewer tests

- [ ] **Step 1: Write .NET viewer exchange tests**

Cover:

- Link creation returns `https://docs.client.com/open?code=...` shape for production config.
- Exchange code expires after 60 seconds.
- Exchange code is single-use.
- Chat-created links allow only `Published`.
- Management-created links can allow `Draft`, `In Review`, and `Published` for authorized management users.
- Real viewer token is set only as `HttpOnly`, `Secure`, `SameSite`, host-only cookie.
- Viewer token is reusable for 15 minutes.

Expected before implementation: tests fail.

- [ ] **Step 2: Implement viewer link and token services**

Persist exchange code state in `app.viewer_exchange_codes` and audit viewer token issuance in `app.viewer_token_audit`.

- [ ] **Step 3: Write docs frontend tests**

Cover:

- Loading exchange state.
- Expired code.
- Already-used code.
- Unauthorized code.
- Token expired.
- Document not found.
- Successful document rendering.

Expected before implementation: tests fail.

- [ ] **Step 4: Implement docs frontend**

Build viewer route `/open?code=...`, exchange flow, safe error states, and document rendering.

- [ ] **Step 5: Verify viewer flow**

Run:

```powershell
dotnet test services/dotnet-api/AdvancedRag.sln --filter Viewer
pnpm --dir apps/docs-web test -- --run
pnpm --dir apps/chat-web test -- --run
pnpm --dir apps/manage-web test -- --run
```

Expected: tests pass.

- [ ] **Step 6: Commit viewer flow**

Run:

```powershell
git add services/dotnet-api apps/docs-web apps/chat-web apps/manage-web
git commit -m "feat: add secure viewer exchange flow"
```

Expected: commit succeeds.

## Task 14: Chat Frontend Workflow

**Files:**
- Modify: `apps/chat-web/src/api/`
- Modify: `apps/chat-web/src/routes/`
- Modify: `apps/chat-web/src/components/`
- Test: chat frontend tests

- [ ] **Step 1: Write chat UI behavior tests**

Cover:

- Empty initial state.
- Question input disabled while submitting.
- Successful answer with citations.
- Cache-hit indicator when API returns cache hit.
- Feedback controls after answer.
- `AI_BUDGET_EXCEEDED` state.
- Auth expired state triggers chat token renewal flow.
- Safe generic error state includes request ID.

Expected before implementation: tests fail.

- [ ] **Step 2: Implement typed chat API client**

Create request/response interfaces for chat query, citation link request, feedback submission, and error envelope.

- [ ] **Step 3: Implement chat screen**

Build compact chat layout focused on question entry, answer reading, citations, feedback, and budget/auth states.

- [ ] **Step 4: Verify chat UI**

Run:

```powershell
pnpm --dir apps/chat-web typecheck
pnpm --dir apps/chat-web test -- --run
pnpm --dir apps/chat-web build
```

Expected: typecheck, tests, and build pass.

- [ ] **Step 5: Commit chat frontend**

Run:

```powershell
git add apps/chat-web
git commit -m "feat: add chat frontend workflow"
```

Expected: commit succeeds.

## Task 15: Management Frontend Workflow

**Files:**
- Modify: `apps/manage-web/src/api/`
- Modify: `apps/manage-web/src/routes/`
- Modify: `apps/manage-web/src/components/`
- Test: management frontend tests

- [ ] **Step 1: Write management navigation tests**

Cover navigation sections:

- Documents.
- Users/groups.
- Audit.
- Feedback review.
- AI budgets.
- Configuration.

Expected before implementation: tests fail.

- [ ] **Step 2: Implement persistent management shell**

Use dense operational layout with persistent navigation, table-heavy screens, filters, detail panels, dialogs, and status badges.

- [ ] **Step 3: Write document workflow UI tests**

Cover list filters, editor state, import states, review validation, indexing retry, archive/restore, and audit panel.

- [ ] **Step 4: Implement document workflow UI**

Connect to .NET API client and show loading, empty, error, disabled, and success states.

- [ ] **Step 5: Write configuration UI tests**

Cover operational defaults display, customer timezone, model configuration, budget defaults, and safe read-only display for secret-backed values.

- [ ] **Step 6: Implement configuration UI**

Expose configurable non-sensitive settings through management views. Do not expose secret values.

- [ ] **Step 7: Verify management UI**

Run:

```powershell
pnpm --dir apps/manage-web typecheck
pnpm --dir apps/manage-web test -- --run
pnpm --dir apps/manage-web build
```

Expected: typecheck, tests, and build pass.

- [ ] **Step 8: Commit management frontend**

Run:

```powershell
git add apps/manage-web
git commit -m "feat: add management frontend workflows"
```

Expected: commit succeeds.

## Task 16: Operational Hardening

**Files:**
- Modify: .NET logging, rate limit, health, readiness modules
- Modify: FastAPI logging, rate limit, health, readiness modules
- Modify: Compose health checks
- Modify: docs runbooks
- Test: backend and Compose checks

- [ ] **Step 1: Write rate limit tests**

Cover:

- Login IP limit: 5 attempts per minute.
- Login user limit: 10 attempts per 15 minutes.
- Chat limit: 30 questions per minute per user.
- Import extraction limit: 10 imports per hour per user.
- Viewer exchange limit: 30 exchanges per minute per user.

Expected before implementation: tests fail.

- [ ] **Step 2: Implement rate limits**

Implement stable safe error codes for each rate-limited workflow. Keep monetary budget enforcement separate from technical rate limits.

- [ ] **Step 3: Write readiness tests**

Cover:

- DB unavailable makes readiness fail.
- Missing required secret makes readiness fail.
- Liveness remains independent of downstream dependencies.

Expected before implementation: tests fail.

- [ ] **Step 4: Implement readiness checks**

Add DB and secret checks to readiness endpoints for .NET and FastAPI.

- [ ] **Step 5: Write logging tests**

Cover request ID, origin IP where available, timestamp, route, response status, safe error code, and daily JSON sink configuration.

- [ ] **Step 6: Implement structured logging**

Configure daily JSON logs per service on mounted volumes with default retention policy of 30 days.

- [ ] **Step 7: Verify operational hardening**

Run:

```powershell
dotnet test services/dotnet-api/AdvancedRag.sln --filter "RateLimit|Health|Logging"
Set-Location services/rag-api; uv run pytest tests -k "rate_limit or health or logging" -q; Set-Location ..\..
docker compose --env-file infra/compose/.env.example -f infra/compose/compose.yaml config
```

Expected: tests pass and Compose config renders.

- [ ] **Step 8: Commit operational hardening**

Run:

```powershell
git add services infra docs
git commit -m "feat: add operational hardening"
```

Expected: commit succeeds.

## Task 17: End-To-End MVP Verification

**Files:**
- Create: `tests/e2e/`
- Create: `tests/e2e/package.json`
- Create: `tests/e2e/playwright.config.ts`
- Create: `tests/e2e/specs/mvp-happy-path.spec.ts`
- Modify: root `package.json`

- [ ] **Step 1: Add E2E test package**

Create Playwright tests for the full MVP happy path.

- [ ] **Step 2: Write happy-path E2E test**

Cover:

1. Admin logs in.
2. Admin creates user/group and assigns Viewer.
3. DocumentManager creates instruction.
4. DocumentManager sends to review.
5. Admin requests publish.
6. Indexing succeeds.
7. Viewer asks chat question.
8. Chat returns answer with citation.
9. Viewer opens citation through exchange code.
10. Viewer submits feedback.
11. Admin sees feedback in management reporting.
12. Admin lowers user AI budget.
13. Viewer receives budget-limited chat state without losing document access.

Expected before all workflows are wired: E2E test fails at the first missing workflow.

- [ ] **Step 3: Run full verification**

Run:

```powershell
dotnet test services/dotnet-api/AdvancedRag.sln
Set-Location services/rag-api; uv run pytest -q; Set-Location ..\..
pnpm -r test -- --run
pnpm -r typecheck
pnpm -r build
docker compose --env-file infra/compose/.env.example -f infra/compose/compose.yaml config
```

Expected: all unit/integration tests pass and Compose config renders.

- [ ] **Step 4: Run E2E tests against Compose stack**

Run:

```powershell
docker compose --env-file infra/compose/.env.example -f infra/compose/compose.yaml -f infra/compose/compose.override.yaml up --build
pnpm --dir tests/e2e test
```

Expected: E2E happy path passes. Stop the stack after verification.

- [ ] **Step 5: Commit E2E verification**

Run:

```powershell
git add tests package.json pnpm-workspace.yaml
git commit -m "test: add mvp end-to-end verification"
```

Expected: commit succeeds.

## Task 18: Documentation And Handoff

**Files:**
- Modify: `README.md`
- Create: `docs/operations/local-development.md`
- Create: `docs/operations/configuration.md`
- Create: `docs/operations/security.md`
- Modify: `context/progress-tracker.md`

- [ ] **Step 1: Document local development**

Create `docs/operations/local-development.md` with:

- Required tools.
- Secret file setup.
- Database startup.
- Backend test commands.
- Frontend test commands.
- Compose startup and shutdown.

- [ ] **Step 2: Document configuration**

Create `docs/operations/configuration.md` with every non-sensitive environment variable from `infra/compose/.env.example`, its default, and its owning service.

- [ ] **Step 3: Document security model**

Create `docs/operations/security.md` covering:

- Secure cookies.
- CSRF.
- Chat token flow.
- Viewer exchange codes.
- No browser storage for credentials.
- Service-token-protected internal indexing.
- AI budget behavior.
- Secret handling.

- [ ] **Step 4: Update progress tracker**

Set current phase to implementation complete for the MVP slice that was executed, record verification commands and results, and list remaining risks.

- [ ] **Step 5: Final verification**

Run:

```powershell
rg -n "NEEDS_DECISION|UNRESOLVED|FOLLOWUP_REQUIRED" README.md docs context --glob "!docs/superpowers/plans/2026-05-11-mvp-implementation-plan.md"
git status --short
```

Expected: no unresolved planning markers in docs/context; git status shows only intentional uncommitted files or is clean after final commit.

- [ ] **Step 6: Commit docs and handoff**

Run:

```powershell
git add README.md docs context
git commit -m "docs: add mvp operations handoff"
```

Expected: commit succeeds.

## Resolved Technical Decisions For Execution

These decisions are fixed for the MVP implementation:

1. The .NET API reads RAG audit/reporting data through FastAPI-owned read-only database views in the `rag` schema.
2. Duplicate chat feedback from the same user updates the same audit row's single feedback value/comment and `feedback_updated_at`.
3. Over-budget users do not receive semantic cached answers in the MVP because semantic cache lookup requires a paid embedding call.
4. Local development uses `manage.localhost`, `chat.localhost`, and `docs.localhost`.

If any of these decisions changes during implementation, update `context/progress-tracker.md` and `context/design-decisions.md` before coding the change.

## Final Verification Command Set

Run these before declaring the MVP implementation complete:

```powershell
dotnet test services/dotnet-api/AdvancedRag.sln
Set-Location services/rag-api; uv run pytest -q; uv run ruff check .; Set-Location ..\..
pnpm -r typecheck
pnpm -r test -- --run
pnpm -r build
docker compose --env-file infra/compose/.env.example -f infra/compose/compose.yaml config
rg -n "NEEDS_DECISION|UNRESOLVED|FOLLOWUP_REQUIRED" README.md docs context --glob "!docs/superpowers/plans/2026-05-11-mvp-implementation-plan.md"
```

Expected: all tests pass, builds pass, Compose config renders, and the documentation scan returns no unresolved planning markers.
