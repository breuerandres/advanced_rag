# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Context System (read this first)

This repo keeps its durable design memory in `context/*.md`, which is the **source of truth** for product behavior, technical rules, and current state. Anything in chat that contradicts those files is wrong until the files are updated.

- Start every session by reading `context/README.md`. It defines the reading order and the precedence rules between context files.
- Precedence when files seem to conflict: `architecture.md` (system/technical invariants) > `code-standards.md` (library/version/language choices) > `rag-spec.md` (RAG specifics) > `ui-context.md` (UI) > `project-overview.md` (product scope) > `code-patterns.md` (code shapes). `design-decisions.md` and `progress-tracker.md` are history, not authority.
- `AGENTS.md` is the agent operating guide (working protocol). `context/code-patterns.md` holds copy-from reference patterns (error envelope, thin controllers, EF/Alembic migrations, tests) — copy and adapt these rather than inventing new shapes.
- When you make a significant architecture/workflow/stack/data/security decision, add an entry to `context/design-decisions.md`. Update `context/progress-tracker.md` (status only) after each meaningful phase.

## Language Rules

- Converse with the user in **Spanish** unless they ask otherwise.
- All code, comments, commits, PRs, logs, and project docs are in **English** — no exceptions.
- End-user product UI strings are bilingual **es-AR (default) and en-US** via `i18next`/`react-i18next` (resources under each app's `src/i18n/`). Do not scatter raw user-facing literals; map stable error codes to localized messages.

## Human-in-the-Loop Protocol

The user wants active participation during implementation; this is not a fully autonomous background task. For each task: split work into **Agent-owned** (code/docs/config/safe verification) and **User-owned** (local setup, Docker, dependency install, service startup, secret creation, destructive DB resets) steps. Give the user concrete commands plus the expected result to report back, and stop at checkpoints when a user-owned result is required. Never create or commit real secrets.

## Architecture

Single-tenant corporate document-management + RAG platform. Each customer gets an isolated Docker Compose stack (Postgres, secrets, logs). The big-picture split that requires reading multiple files:

**Two backends own two Postgres schemas — never cross-write:**
- `services/dotnet-api` (.NET 8) owns the **`app`** schema: auth/session, users, roles, groups, organizational units, document lifecycle, assisted PDF/DOCX import, document images, viewer session handoff, management audit, read-only reporting.
- `services/rag-api` (FastAPI, Python 3.12) owns the **`rag`** schema: chat, retrieval, embeddings, semantic cache, RAG query audit, model pricing, indexing jobs.
- The only cross-schema interaction: FastAPI creates read-only reporting views in `rag` and grants SELECT to `.NET`'s reporting role; `.NET` EF migrations grant `rag_owner` SELECT on a couple of `app` tables. FastAPI **never** writes `app`; `.NET` **never** writes `rag`.

**Three React frontends (`apps/manage-web`, `apps/chat-web`, `apps/docs-web`)** call only their assigned backend via **same-origin `/api/*` routes** through Caddy — never cross-origin. manage/docs → `.NET`; chat → FastAPI for chat/feedback and `.NET` for auth/session/viewer-link. Shared components live in `packages/shared-ui` (`@helpcenter/shared-ui`).

**Service-to-service:** `.NET → FastAPI` internal indexing/cache calls go over the Docker network with an `X-Internal-Service-Token` (Compose secret), not exposed through Caddy. FastAPI validates the browser `__Host-session` cookie by calling `.NET` `/internal/session/validate` on every request (no caching of access claims).

**Invariants** (full list in `context/architecture.md`): public chat retrieves only `Published` content; `docs.client.com` revalidates document access server-side (links are locators, not authorization); main session JWT never goes in a URL; cached answers reuse only for matching `access_scope_hash`; audit persists to Postgres, not only logs.

Key infra/auth/RAG details (CSRF double-submit, RS256 JWT rotation, semantic cache keying, AI budgets, document lifecycle states `Draft`/`In Review`/`Published`/`Archived`) are specified in `context/architecture.md` and `context/rag-spec.md` — consult them before touching those areas.

### In-flight refactor

An access-model refactor is underway (see `context/progress-tracker.md`): moving from flat group-only access to **hierarchical organizational units + transverse groups**, with `Admin` (global), `DocumentEditor`, and `DocumentPublisher` replacing the legacy all-purpose `DocumentManager` role. Document access rules use AND within a rule / OR between rules. Legacy `DocumentManager` references and group-only retrieval SQL still exist in places and are being replaced — check the tracker for current task status before assuming behavior.

## Common Commands

Three toolchains. Frontends use `pnpm` (workspace), .NET uses `dotnet`, FastAPI uses `uv`. Backend integration tests use **Testcontainers**, so **Docker must be running** to run them.

**Monorepo-wide (frontends + packages):**
```powershell
pnpm install              # install workspace deps
pnpm lint                 # eslint across apps/packages (pnpm -r lint)
pnpm test                 # vitest across apps/packages
pnpm typecheck            # tsc -b across apps/packages
pnpm build                # build all apps
pnpm test:e2e             # Playwright E2E (runs against the full Compose stack via Caddy)
```

**Single frontend app / single test:**
```powershell
pnpm.cmd --dir apps\manage-web typecheck
pnpm.cmd --dir apps\manage-web build
pnpm.cmd --dir apps\chat-web test -- --run src/App.test.tsx          # one test file, single run
```

**.NET API (run from repo root; `global.json` pins the .NET 8 SDK line):**
```powershell
dotnet build services\dotnet-api\AdvancedRag.sln --no-restore
dotnet test services\dotnet-api\AdvancedRag.sln
dotnet test services\dotnet-api\AdvancedRag.sln --filter "FullyQualifiedName~DocumentLifecycleServiceTests"   # single test class
```
A NuGet vulnerability-index warning when nuget.org is unreachable is expected and harmless.

**FastAPI RAG service (run from `services\rag-api`):**
```powershell
uv run pytest -q
uv run pytest tests/test_chat_rag.py -q        # single test file
uv run ruff check .
uv run mypy src tests
```

**Local Compose stack (HTTPS via Caddy internal CA, hosts `*.localhost`):**
```powershell
.\infra\compose\Start-Local.ps1 -TrustCaddyCertificate     # start stack + trust the Docker Caddy root CA
.\infra\compose\Seed-LocalDemoData.ps1                      # seed demo hierarchy/users/documents
docker compose --env-file infra/compose/.env.example -f infra/compose/compose.yaml config   # validate Compose
```
First-run seeded admin is `admin@admin.com` / `admin` (change immediately outside throwaway testing). Migrations run automatically on service startup (EF Core for `app`, Alembic for `rag`).

## Library & Convention Guardrails

- Do not introduce new libraries (HTTP client, ORM, logging, validation, testing, AI SDK) without recording the decision in `context/design-decisions.md`. The MVP stack is locked in `context/code-standards.md`.
- Keep controllers/routers thin; put lifecycle, auth, import, cache, budget, and audit logic in testable services. .NET DTOs go under `AdvancedRag.Api/Models/<Feature>/`; FastAPI uses Pydantic models (not stdlib `@dataclass`) for anything crossing a boundary.
- Use the shared error envelope `{ "error": { "code", "message", "details", "requestId" } }` and the stable UPPER_SNAKE_CASE error-code catalog in `context/code-patterns.md`. Add new codes to that catalog.
- All AI provider calls live in FastAPI via the official `openai` SDK; .NET never calls OpenAI. Model IDs/dimensions are config (`OPENAI_CHAT_MODEL`, etc.), never hardcoded in business logic.
- Frontend: all API calls go through the per-app typed `apiClient` (never raw `fetch` in components); server state via `@tanstack/react-query`; forms/validation via `react-hook-form` + `zod`. Document HTML is sanitized server-side (`Ganss.Xss`) and on render (`DOMPurify`); only `color` and `text-align` inline CSS plus `<mark>` are preserved.
- When a task adds/changes API endpoints, end the summary with a concise Postman checklist (method, path, auth/CSRF, body, expected status). Do not persist those checklists in docs unless asked.

## Knowledge Graph (graphify)

This project has a knowledge graph at `graphify-out/` with god nodes, community structure, and cross-file relationships.

- For codebase questions, first run `graphify query "<question>"` when `graphify-out/graph.json` exists. Use `graphify path "<A>" "<B>"` for relationships and `graphify explain "<concept>"` for focused concepts. These return a scoped subgraph, usually much smaller than GRAPH_REPORT.md or raw grep output.
- If `graphify-out/wiki/index.md` exists, use it for broad navigation instead of raw source browsing.
- Read `graphify-out/GRAPH_REPORT.md` only for broad architecture review or when query/path/explain do not surface enough context.
- After modifying code, run `graphify update .` to keep the graph current (AST-only, no API cost).
