# Code Standards

## General

- Keep modules small and scoped to one product boundary.
- Do not mix management, viewer, and RAG responsibilities in the same service module.
- Validate external input at the boundary before calling business logic.
- Return safe user-facing errors and log technical details with request IDs.

## .NET API

- .NET owns identity, users, roles, groups/departments, document lifecycle, assisted import extraction, viewer access tokens, management audit, and read-only management reporting.
- Use MVC controllers as the default HTTP boundary for application routes. Minimal APIs are allowed only for very small infrastructure endpoints when they materially reduce ceremony, such as health checks, and should not be used for feature modules.
- Keep controllers thin and move lifecycle, authorization, import extraction, and token logic into testable services/use cases.
- Put browser-facing request/response DTOs under `AdvancedRag.Api/Models/<Feature>/`. Do not define feature DTOs inside controller files.
- Keep application service interfaces beside their feature use cases in `AdvancedRag.App/<Feature>/` instead of a global `Interfaces` folder.
- Prefer explicit local variable types in new or refactored `.NET` code when the concrete type is clear and improves readability. Do not ban `var`: use `var` when the explicit type is unavailable or noisier than the initializer, such as anonymous types, LINQ projections, deconstruction, pattern-driven code, or cases where the initializer already makes the type obvious and an explicit type would reduce clarity.
- Apply the explicit-type preference to production code and to tests that are newly written or actively refactored. Do not perform unrelated mechanical `var` churn inside existing files unless that refactor is the task.
- Use EF Core migrations for the `app` schema.
- Use `DocumentFormat.OpenXml` for DOCX text extraction.
- Use `PdfPig` for PDF text extraction.
- Import extraction adapters must return extracted text plus safe extraction metadata, not final publishable HTML.
- Do not persist imported file bytes in the MVP.
- Enforce the 10 MB PDF/DOCX import limit server-side.
- Return stable code `IMPORT_TEXT_NOT_EXTRACTABLE` when a PDF/DOCX has no extractable text.
- Issue viewer links using one-time short-lived exchange codes and set real viewer access tokens only as host-only `HttpOnly` cookies for `docs.client.com`.
- Do not accept viewer access tokens from URL parameters.

## FastAPI RAG Service

- FastAPI owns chat, retrieval, embeddings, semantic cache, RAG audit, model pricing, and indexing jobs.
- Organize FastAPI HTTP boundaries with an MVC-like separation: `api/routers` for route/controller logic, `schemas` for Pydantic request/response models, feature services for business logic, and infrastructure adapters/repositories for database or provider access.
- Keep FastAPI routers thin. Routers parse HTTP input, bind dependencies, call services, and return schemas; retrieval, budget, indexing, cache, and audit behavior belongs in testable service modules.
- Use Pydantic `BaseModel` for request schemas, response schemas, API contracts, provider payload contracts, persisted/read-model DTOs that cross module boundaries, and configuration through `pydantic-settings`. Use `typing` annotations precisely, including `Protocol`, `TypedDict`, `Literal`, `Annotated`, and `Self` when they improve the contract.
- Do not use standard-library `@dataclass` for FastAPI boundary data, configuration, or validated contracts. Internal simple classes or dataclasses are allowed only for private implementation details that do not cross service/module boundaries and do not need validation, serialization, aliases, or OpenAPI/schema behavior. Prefer a frozen Pydantic model when an internal value object leaves a module or benefits from validation.
- The FastAPI service targets Python 3.12. `services/rag-api/.python-version` must stay on the Python 3.12 line and `pyproject.toml` must constrain `requires-python` to `>=3.12,<3.13` unless the stack decision is updated.
- FastAPI must not parse PDF/DOCX imports in the MVP.
- Use Alembic migrations for the `rag` schema.
- Store chunk text/metadata and its vector embedding together in `rag.document_chunks`. The current v2 migration resizes the default embedding column to pgvector `vector(1024)` and the embeddings request must pass the configured `OPENAI_EMBEDDING_DIMENSIONS`.
- Store citations as `rag.query_audit_citations` child rows of `rag.query_audit_events`.
- Store one simple thumbs feedback value and optional sanitized comment on `rag.query_audit_events` for the MVP. Allow the same user to update feedback on the same answer by overwriting the single feedback value/comment and updating `feedback_updated_at`; split feedback into a child table only if multi-feedback/history requirements are introduced.
- Enforce per-user AI usage budgets before new paid chat work whenever possible. Return stable error code `AI_BUDGET_EXCEEDED` when a user has reached the configured budget. Do not treat budget exhaustion as a general authorization failure for document viewing or management workflows.
- Block over-budget semantic cache lookups in the MVP because lookup requires paid embedding generation. Exact no-cost cache lookup is out of scope.
- Validate short-lived signed access tokens issued by the .NET API locally for public chat requests; do not add a .NET introspection call to the normal chat path.
- Do not implement a separate FastAPI refresh token for the MVP. Chat token renewal is a .NET-owned route based on the main secure session.
- Read OpenAI model IDs and embedding dimensions from configuration. Do not hardcode `gpt-4.1-nano`, `text-embedding-3-small`, embedding dimensions, or future model IDs in business logic.
- Persist the actual chat model, embedding model, token usage, latency, estimated cost, and model pricing snapshot used for each audited RAG query.

## TypeScript Frontends

- Use strict TypeScript.
- Avoid `any`; use explicit interfaces for API requests and responses.
- Use Tailwind CSS, `shadcn/ui`, and `lucide-react` as the MVP UI foundation.
- Frontends must call only their assigned backend APIs: management and docs workflows use .NET; chat uses FastAPI.
- Show loading, empty, error, disabled, and success states for all core workflows.

## API Contracts

- Use explicit DTOs/schemas for all request and response bodies.
- When an implementation task adds or changes API endpoints, the final task summary to the user must include a concise Postman checklist with method, path, auth/CSRF requirement, representative body when relevant, and expected status/result. Do not persist those checklists in project docs unless the user asks.
- Enforce authentication and authorization before mutation.
- Use the shared error envelope: `{ "error": { "code", "message", "details", "requestId" } }`.
- Stable validation error codes are required for oversized imports, non-extractable imports, invalid lifecycle transitions, unauthorized access, and indexing failures.
- Stable error codes are required for invalid, expired, already-used, and unauthorized viewer exchange codes.
- Stable rate-limit error codes are required for login, chat, import extraction, and viewer exchange rate limits.
- Browser-facing authentication must use `HttpOnly`, `Secure`, `SameSite` cookies with CSRF protection for mutating requests.
- Browser frontends should call same-origin `/api/*` routes through Caddy instead of cross-origin backend hosts.
- Use host-only `__Host-` prefixed cookies for browser sessions when set through frontend hosts; do not use broad parent-domain cookies for the MVP.
- Do not store JWTs, refresh tokens, session IDs, or other credential-bearing tokens in `localStorage` or `sessionStorage`.

## Data and Storage

- `.NET` writes the `app` schema; FastAPI writes the `rag` schema.
- Cross-schema writes are not allowed except through explicit API/internal contracts.
- Store canonical normalized document HTML and metadata in Postgres.
- Model MVP document access through groups/departments and attributes. Do not add per-user document ACLs unless a later requirement explicitly changes the access model.
- Store per-user AI budget configuration in the `app` schema and RAG spend evidence in `rag.query_audit_events`.
- Store functional audit in Postgres and technical logs as daily JSON files on mounted volumes.
- Keep operational limits configurable; defaults are defined in `context/architecture.md`.

## File Organization

- `apps/manage-web/` - management frontend.
- `apps/chat-web/` - chat frontend.
- `apps/docs-web/` - document viewer frontend.
- `services/dotnet-api/` - .NET API and `app` schema owner.
- `services/rag-api/` - FastAPI service and `rag` schema owner.
- `infra/compose/` - Docker Compose, Caddy, volumes, health checks, and secrets wiring.

## Backend Stack And Libraries

The MVP locks in the following library choices. Do not substitute without an updated decision in `context/design-decisions.md`. Pin exact versions during scaffolding (Task 0 of the implementation plan).

### .NET 8 API

The repository root uses `global.json` to select the .NET 8 SDK line for CLI commands. Do not scaffold or build the .NET API with a newer major SDK unless the stack decision is updated in `context/design-decisions.md`.

| Concern | Library | Notes |
| --- | --- | --- |
| Web framework | ASP.NET Core 8 MVC controllers | Controllers are the default for feature modules. Minimal APIs are reserved for small infrastructure endpoints such as health checks. Controllers stay thin. |
| ORM | EF Core 8 + `Npgsql.EntityFrameworkCore.PostgreSQL` | `app` schema only; never write `rag`. |
| Migrations | EF Core Migrations | One DbContext owning `app` schema. |
| Validation | `FluentValidation` + `FluentValidation.AspNetCore` | Returns shared error envelope, not raw `ValidationProblemDetails`. |
| Logging | `Serilog` + `Serilog.Sinks.File` (JSON formatter) + `Serilog.AspNetCore` | Daily rolling JSON files mounted on volume. Use `Serilog.Enrichers.CorrelationId` for `X-Request-ID`. |
| HTTP client | `HttpClient` via `IHttpClientFactory` + `Microsoft.Extensions.Http.Polly` | Retry with jitter and circuit breaker for `.NET â†’ FastAPI` internal calls. |
| OpenAI | Not used directly from .NET in the MVP | All AI provider calls live in FastAPI. |
| PDF extraction | `PdfPig` `0.1.14` | Assisted import only. |
| DOCX extraction | `DocumentFormat.OpenXml` `3.5.1` | Assisted import only. |
| HTML sanitization | `Ganss.Xss` via `HtmlSanitizer` `9.0.892` | Sanitize stored normalized document HTML and any review comment input that may render HTML. |
| Authentication | Cookie authentication plus local users in `app.users`; hand-rolled PBKDF2-SHA256 password hashing using `Rfc2898DeriveBytes` | Session cookies are host-only `__Host-session` cookies. The legacy chat-token endpoint still exists until Phase 1.5 cleanup. |
| CSRF | Signed double-submit token using `__Host-CSRF` cookie plus `X-CSRF-Token` header | HMAC secret is shared with FastAPI through `csrf_signing_key`; see `architecture.md`. |
| JWT signing/validation | `System.IdentityModel.Tokens.Jwt` `8.14.0` + `Microsoft.IdentityModel.Tokens` | RS256, `kid` header, two active keys for rotation. |
| Testing | `xUnit` + `FluentAssertions` + `Microsoft.AspNetCore.Mvc.Testing` (`WebApplicationFactory`) + `Testcontainers.PostgreSql` | Integration tests hit a real Postgres container; no DB mocking. |

### FastAPI RAG Service

| Concern | Library | Notes |
| --- | --- | --- |
| Web framework | `fastapi` | Run with `uvicorn` in dev, `gunicorn` + `uvicorn.workers.UvicornWorker` in compose. |
| Package manager | `uv` | `pyproject.toml` with locked `uv.lock`; the Task 3 scaffold used `uv` 0.9.22. |
| ORM | `SQLAlchemy` 2.0 async + `asyncpg` driver | `rag` schema only; never write `app`. |
| Migrations | `Alembic` | Generates SQL for `rag` schema only. Owns the read-only reporting views consumed by .NET. |
| Validation | `pydantic` v2 | All request/response models, all config via `pydantic-settings`. |
| Logging | `structlog` configured with `JSONRenderer` + stdlib `logging` bridge | Daily rolling JSON via a custom file handler; same log envelope as .NET. |
| HTTP client | `httpx` (async) | Used for FastAPI -> .NET internal session validation and available for provider transports that require direct HTTP calls. |
| OpenAI | `openai` (official Python SDK, async client) | Wrap behind a thin internal adapter that the rest of the service depends on. |
| Vector DB | `pgvector` Postgres extension + `pgvector.asyncpg` integration registered through SQLAlchemy types | Current v2 column type is `Vector(1024)` after migration `20260522_120000_v2_change_embedding_dimensions.py`. |
| Tokenization | `tiktoken` | Chunk sizing and token-cost calculations. |
| JWT validation crypto | `pyjwt[crypto]` `2.12.1` | Required for the legacy RS256 chat-token validator while `POST /api/auth/chat-token` remains in the codebase. |
| Tracing/correlation | `asgi-correlation-id` | Reads/propagates `X-Request-ID`. |
| Testing | `pytest` + `pytest-asyncio` + `httpx.AsyncClient` + `testcontainers[postgres]` | Integration tests hit a real Postgres container with `pgvector`. |

### Cross-cutting

- Do not introduce additional libraries during implementation without recording the decision in `context/design-decisions.md`. The rule covers HTTP clients, ORMs, logging, validation, testing, and AI provider SDKs.
- Use the official OpenAI Python SDK only in FastAPI. The .NET service must not call OpenAI directly in the MVP.
- All retries that touch paid AI provider endpoints must respect `AI_BUDGET_EXCEEDED` checks before each attempt, not just before the first.

## Frontend Stack And Libraries

All three React frontends share the same stack. Each app has its own `package.json` inside a `pnpm` workspace under `apps/`.

| Concern | Library | Notes |
| --- | --- | --- |
| Build tool | `vite` | React + TypeScript SWC template. |
| Package manager | `pnpm` | Workspace covers `apps/manage-web`, `apps/chat-web`, `apps/docs-web`. |
| Language | TypeScript strict mode | `noImplicitAny`, `strictNullChecks`, `exactOptionalPropertyTypes` all on. |
| UI framework | React 18 | Functional components only. |
| Styling | Tailwind CSS 3 | Configured per app; share design tokens via a `packages/ui-tokens` if duplication appears. |
| Component system | `shadcn/ui` | Components copied locally per app; keep variants consistent across apps. |
| Icons | `lucide-react` | Default icon system. |
| HTTP client | `fetch` wrapped in a typed `apiClient` per app | No `axios`. The wrapper sets credentials, request ID, CSRF token, and error envelope parsing. |
| Server state | `@tanstack/react-query` v5 | Caches, retries, suspense-ready. All API calls go through it. |
| Client state | `zustand` | Use sparingly; prefer URL state and react-query cache. |
| Forms | `react-hook-form` + `zod` + `@hookform/resolvers` | Schemas mirror backend DTOs; no duplicate validation logic. |
| Rich text editor (management) | `@tiptap/react` + `@tiptap/starter-kit` + `@tiptap/extension-link` + `@tiptap/extension-image` + `@tiptap/extension-underline` + TipTap table extensions | Output is sanitized HTML stored in `app.document_versions`. |
| HTML sanitization | `dompurify` | Sanitize HTML before rendering document content in the viewer and before submitting from the editor. |
| Routing | `react-router-dom` v6 | Server-side rendering is out of scope for the MVP. |
| Testing (unit/component) | `vitest` + `@testing-library/react` + `@testing-library/user-event` + `msw` for API mocks | Mock at the network boundary, not at the hook level. |
| Testing (E2E) | `playwright` | Runs against the full Docker Compose stack via Caddy. |
| Lint/format | `eslint` (`@typescript-eslint`, `eslint-plugin-react`, `eslint-plugin-react-hooks`) + `prettier` | Pre-commit via `lint-staged` + `husky` is optional. |

### Frontend Rules

- All API calls go through the per-app typed `apiClient`. Components and hooks never call `fetch` directly.
- TanStack Query keys follow the convention `[domain, resource, params]`, e.g. `["documents", "list", { status: "Published" }]`.
- Use `zod` schemas for both form validation and API response parsing; the same schema can validate both ends.
- The TipTap editor emits HTML that is sanitized server-side by `Ganss.Xss` before persistence; the frontend also runs `DOMPurify` on render.
- The chat frontend handles answer streaming via Server-Sent Events; the `apiClient` exposes a typed `streamChat()` helper.

## Language Policy

The product UI for end users (chat, viewer, management) is **Spanish (Argentine Spanish)**. The rest of the project is in **English**.

| Surface | Language | Notes |
| --- | --- | --- |
| End-user UI strings | Spanish (es-AR) | All visible labels, buttons, errors, empty states, toasts. |
| Validation messages shown to end users | Spanish | Translated from stable error codes in a per-app `errorMessages.ts` map. |
| Email/notification copy if added later | Spanish | Out of scope for MVP. |
| Code identifiers (variables, functions, types, files, branches) | English | No exceptions. |
| Code comments | English | No exceptions. |
| Commit messages | English | No exceptions. |
| Pull request titles and descriptions | English | No exceptions. |
| Technical/structured logs (`message`, `event`, `error.type`) | English | Log content is for operators, not end users. |
| Error envelope `code` values | English UPPER_SNAKE_CASE | Stable, never localized; e.g. `AI_BUDGET_EXCEEDED`. |
| Error envelope `message` field | English | Technical, safe-for-log; never shown raw to end users. |
| Project documentation in `context/` and `docs/` | English | No exceptions. |
| OpenAI system prompts and answer generation | Spanish | The model is instructed to respond in Spanish. The system prompt itself is written in English with an explicit document to answer in Spanish. |

### i18n Approach

The MVP does not include a full i18n framework. Instead, each frontend app keeps user-facing strings in a single `src/strings.ts` (or `src/i18n/es.ts`) module so that a future locale addition is a mechanical refactor. Do not scatter user-facing literals through components.

Stable error codes coming from the shared error envelope are mapped to Spanish messages in a per-app `src/errorMessages.ts`. A missing code falls back to a generic "Ocurrió un error inesperado." message and is logged with the unmapped code for operators.
