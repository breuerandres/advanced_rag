# Base Architecture Design

Date: 2026-05-11

## Status

Approved for written-spec review. No implementation plan has been created yet, and no code implementation should start until this spec is reviewed and approved.

## Purpose

Advanced RAG Document Platform is a sellable single-tenant corporate document management and RAG product. It centralizes internal documents, controls their lifecycle, and lets authorized viewers ask questions through a secure RAG chat experience that only retrieves content allowed by their effective access scope.

The first implementation unit is the complete base architecture. This spec defines the MVP system boundaries, security model, data ownership, API contract groups, lifecycle behavior, RAG behavior, audit, operations, and UI foundation.

## Goals

1. Provide a secure document lifecycle from draft to published content with explicit roles, audit, and publication control.
2. Serve end users with a RAG chat experience that retrieves only published content matching their effective access scope.
3. Keep deployment sellable as a per-customer Docker Compose product with isolated data, secrets, logs, and configuration.
4. Capture operational and quality signals, including query audit, token/cost metadata, cache behavior, citations, feedback, and latency.
5. Give administrators control over AI spend through configurable per-user monthly monetary budgets.

## Non-Goals

- Multi-tenant SaaS deployment.
- Kubernetes deployment.
- External SSO/OIDC.
- Dedicated `Reviewer` role.
- OCR for scanned PDFs.
- Retaining original PDF/DOCX imports.
- Per-user document ACL exceptions.
- One-time-use viewer access tokens.
- Separate soft delete workflow beyond `Archived`.

## System Architecture

The product uses a monorepo with clear service and frontend boundaries.

| Path | Ownership |
| --- | --- |
| `apps/manage-web` | Management frontend |
| `apps/chat-web` | Chat frontend |
| `apps/docs-web` | Document viewer frontend |
| `services/dotnet-api` | .NET API and `app` schema owner |
| `services/rag-api` | FastAPI service and `rag` schema owner |
| `infra/compose` | Docker Compose, Caddy, volumes, health checks, secrets wiring |
| `docs` | Product and technical documentation |
| `context` | Durable project memory and current design state |

The MVP deployment is single-tenant per customer using Docker Compose. Each customer receives isolated services, database, secrets, logs, and configuration.

## Runtime Components

| Layer | Technology | Responsibility |
| --- | --- | --- |
| Reverse proxy | Caddy | TLS termination, routing, same-origin API gateway, request IDs, access logs |
| Management frontend | React TypeScript | Document management, users/groups, audit, feedback review, AI budgets, configuration |
| Chat frontend | React TypeScript | End-user RAG chat, citations, feedback, budget-limited chat states |
| Docs frontend | React TypeScript | Token-gated document viewer |
| Management/API backend | .NET 8 | Identity, users, roles, groups, document lifecycle, imports, viewer links/tokens, AI budget config, management audit |
| RAG backend | FastAPI | Chat, retrieval, embeddings, semantic cache, RAG audit, feedback submission, indexing jobs |
| Database | PostgreSQL with pgvector | Relational data, vectors, audit, cache, jobs, pricing |
| AI provider | OpenAI | Chat and embeddings |

## Public Routing

| Host | Primary Target |
| --- | --- |
| `manage.client.com` | Management frontend |
| `chat.client.com` | Chat frontend |
| `docs.client.com` | Document viewer frontend |
| `api.client.com` | Optional controlled direct .NET API surface |
| `rag.client.com` | Optional controlled direct FastAPI API surface |

Browser frontends should call same-origin `/api/*` routes through Caddy instead of calling backend hosts cross-origin.

| Browser Host | Same-Origin API Routes | Upstream |
| --- | --- | --- |
| `manage.client.com` | `/api/*` | .NET API |
| `chat.client.com` | `/api/chat/*`, `/api/feedback/*` | FastAPI |
| `chat.client.com` | `/api/auth/*`, `/api/session/*`, viewer-link support | .NET API |
| `docs.client.com` | `/api/*` | .NET API |

FastAPI internal indexing endpoints are not exposed through Caddy. The .NET API calls FastAPI over the Docker network using an internal service token from Compose secrets.

## API Contract Strategy

The architecture spec defines MVP contracts by contract group, not exhaustive endpoint-by-endpoint DTOs. Each group must define owner, exposure, primary route families, authorization, lifecycle side effects, audit requirements, and stable errors. Detailed request/response DTOs are finalized during implementation planning and coding.

| Contract Group | Owner | Exposure | Responsibilities |
| --- | --- | --- | --- |
| Auth/session | .NET API | Browser same-origin `/api/auth/*`, `/api/session/*` | Login, logout, session status, CSRF, secure cookies, chat-token renewal |
| Users/groups | .NET API | Management same-origin `/api/*` | Users, roles, groups/departments, activation/deactivation |
| Documents | .NET API | Management same-origin `/api/*` | CRUD, metadata, filters, import extraction, lifecycle transitions, publish request, retry indexing, archive, restore |
| Viewer | .NET API | Docs same-origin `/api/*`; link creation from chat/management | Exchange-link creation, one-time code exchange, viewer token cookie issuance, document access validation |
| Chat/RAG | FastAPI | Chat same-origin `/api/chat/*` | Question submission, retrieval, answer generation, citations, semantic cache, RAG audit, token/cost/latency tracking |
| Feedback/reporting | FastAPI and .NET API | Chat feedback via FastAPI; management reporting via .NET | Feedback submission, read-only management reporting over RAG audit |
| Usage budgets | .NET API and FastAPI | Management config via .NET; enforcement in FastAPI | Per-user monthly AI budget config, usage reporting, chat budget enforcement |
| Internal indexing | FastAPI, called by .NET | Docker-network-only | Job creation, status/result, retry support, chunking, embeddings, vector storage |
| Health/errors | All services | Public or internal as appropriate | Health/readiness, shared error envelope, request IDs, stable safe error codes |

## Authentication And Session Security

.NET is the identity authority. It owns login, password hashing, browser sessions, user state, roles, groups/departments, and signing key management.

Browser-facing sessions use secure cookies, not browser storage. Cookies must be `HttpOnly`, `Secure`, `SameSite`, and host-only `__Host-` prefixed where routing permits. Credential-bearing tokens must not be stored in `localStorage` or `sessionStorage`.

State-changing browser requests must use CSRF protection. SameSite cookies are defense in depth, not the only CSRF mitigation.

FastAPI validates short-lived signed chat access tokens issued by .NET. Validation is local and checks signature, issuer/audience, expiration, user identifier, roles, groups/departments, and `access_scope_hash`. FastAPI must not call .NET for session introspection on every chat request.

Chat token behavior:

- Initial default TTL: 15 minutes, configurable.
- `chat.client.com` obtains or renews a chat token through a same-origin route such as `/api/auth/chat-token`, routed to .NET.
- .NET issues the chat token only when the main secure session is valid.
- The chat token is stored only as a host-only `HttpOnly`, `Secure`, `SameSite` cookie for `chat.client.com`.
- FastAPI owns no refresh token in the MVP.

## Roles And Access

| Role | Permissions |
| --- | --- |
| `Admin` | Full access, including publishing, user/group management, audit, configuration, and AI budget management |
| `DocumentManager` | Can create, edit, import, manage metadata, send to review, return/reject review with comment, archive eligible unpublished documents, restore archived documents to draft, and review feedback |
| `Viewer` | Cannot access management. Can use chat and open allowed published documents |

Document access is based on groups/departments plus document attributes. Per-user document ACL exceptions are out of scope for the MVP. Exceptional access should be modeled by creating a dedicated group and assigning the user to that group.

## Viewer Link And Token Flow

`docs.client.com` is token-gated. The real viewer access token must not appear in URLs.

Flow:

1. Chat or management requests a document link from .NET.
2. .NET returns a URL such as `https://docs.client.com/open?code=...`.
3. The code is single-use and expires after 60 seconds by default.
4. `docs.client.com` exchanges the code through a same-origin `/api/*` route backed by .NET.
5. .NET validates authorization and sets the real scoped viewer token as a host-only `HttpOnly`, `Secure`, `SameSite` cookie for `docs.client.com`.
6. The viewer loads the document using that cookie.

Viewer access tokens are document-specific, reusable during their 15-minute TTL, and include at least `document_id`, `user_id`, `purpose`, `allowed_status`, `access_scope_hash`, `expires_at`, and `jti`.

Invalid, expired, already-used, or unauthorized exchange codes must not expose document details. The viewer shows a safe expired-link or access-denied state.

## Document Lifecycle

Document states:

- `Draft`
- `In Review`
- `Published`
- `Archived`

Rules:

- Each successful publication creates an immutable version such as `v1`, `v2`, and so on.
- Viewer and public RAG use the latest successfully published version.
- Editing a published document creates a new draft version while the latest published version remains active.
- `Admin` publishes directly from `In Review` to `Published`.
- `DocumentManager` cannot publish.
- Moving a draft to `In Review` requires title, document type, allowed groups/departments, audience/user type, non-empty sanitized HTML, and valid sanitized content. Tags are optional.
- Sending to review may include an optional internal comment.
- Returning/rejecting from `In Review` back to `Draft` requires an internal comment.
- `Admin` and `DocumentManager` can return/reject from review with the required comment.
- `Archived` is the only functional removal path in the MVP.

Publishing starts pre-publication indexing through FastAPI. The document remains `In Review` while indexing is pending or running. It transitions to `Published` only after indexing succeeds. If indexing fails, the document remains unavailable to public chat and normal viewer access. Management UI must show a safe error summary and a manual retry action.

Archiving applies to the entire document. `Admin` can archive any document. `DocumentManager` can archive only documents in `Draft` or `In Review` that do not have an active published version. Restore moves an archived document to `Draft` and does not reactivate previous public access or RAG entries.

## Assisted Imports

PDF/DOCX import is a management-domain responsibility owned by .NET. FastAPI does not parse imported files in the MVP.

Rules:

- DOCX extraction uses `DocumentFormat.OpenXml`.
- PDF extraction uses `PdfPig`.
- Import size limit is 10 MB per file.
- Originals are not retained.
- Extracted text is inserted into the editor for user correction.
- The user remains responsible for final formatting, title, document type, access attributes, and review readiness.
- If the user abandons the import without saving, extracted text and import metadata are not persisted as business data.
- Scanned PDFs or files with no extractable text are rejected with stable code `IMPORT_TEXT_NOT_EXTRACTABLE`.

## RAG And Indexing

Public chat uses only `Published` content. Internal preview can use `Draft`, `In Review`, and `Published` content for authorized management users.

Indexing is asynchronous. .NET requests indexing through FastAPI's Docker-network-only internal endpoint. FastAPI creates and owns `rag.indexing_jobs`.

FastAPI receives saved normalized document content for indexing. It chunks content, creates embeddings, stores vectors, and records indexing state, attempts, technical errors, timestamps, and document references.

## Semantic Cache

The semantic cache can return complete cached answers with citations only when the effective access scope matches.

Cache matching uses:

- Corpus: `published` or `preview`.
- `access_scope_hash`.
- Normalized question embedding.
- Similarity threshold, default `0.90`.
- Expiration timestamp.
- Source document IDs.

Cache invalidation:

- Invalidate cached answers when a source document changes, is archived, changes state, or changes accessibility.
- Creating a new document does not invalidate existing cache entries.
- TTL default is 24 hours and configurable.

## AI Usage Budgets

The MVP supports per-user monthly AI usage budgets in USD. Default budget is USD 5 per user per calendar month. The customer deployment's configured timezone defines the calendar month; default timezone is UTC if not configured.

`Admin` users can set, adjust, or disable a user's budget from the management app. Budget configuration lives in the `app` schema and is audited by .NET.

FastAPI enforces budgets before new paid chat/RAG work whenever possible. Current-period spend is calculated from `rag.query_audit_events`, which stores model IDs, token usage, pricing snapshot, and estimated cost.

When the budget is reached, chat returns the shared error envelope with stable code `AI_BUDGET_EXCEEDED`.

Budget exhaustion blocks new chat/RAG work that can generate AI provider cost. It does not block document viewing, management workflows, or authorized document access. In the MVP, over-budget users do not receive semantic cached answers because semantic cache lookup requires a paid embedding request. Exact no-cost cache lookup is deferred.

Rate limits and AI usage budgets are separate controls.

## Database Ownership

One PostgreSQL database is used per customer deployment.

### `app` Schema, Owned By .NET

- `users`
- `roles`
- `user_roles`
- `groups`
- `user_groups`
- `documents`
- `document_versions`
- `document_permissions`
- `document_tags`
- `review_comments`
- `import_metadata`
- `viewer_exchange_codes`
- `viewer_token_audit`
- `user_ai_budget_limits`
- `audit_events`

`document_permissions` stores group/department and attribute access rules, not per-user ACL rows.

### `rag` Schema, Owned By FastAPI

- `indexing_jobs`
- `document_chunks` with chunk text/metadata and `vector(1536)` embedding in the same row by default
- `semantic_cache_entries`
- `semantic_cache_sources`
- `query_audit_events`
- `query_audit_citations`
- `model_pricing`

Feedback is stored as one simple thumbs value and optional sanitized comment on `rag.query_audit_events`. The same user may update feedback for the same answer by overwriting the single feedback value/comment and updating `feedback_updated_at`; feedback history is not retained in the MVP. Citations are child rows in `rag.query_audit_citations`.

.NET uses EF Core migrations for `app`. FastAPI uses Alembic migrations for `rag`. Services must not write outside their owned schema except through explicit internal contracts.

## Audit, Logging, And Errors

Management audit lives in `app.audit_events`.

RAG query audit lives in `rag.query_audit_events` and stores:

- User.
- Question.
- Answer.
- Cache hit.
- Feedback.
- Model IDs.
- Input/cached/output tokens.
- Estimated cost.
- Pricing snapshot.
- Latency.
- Request ID.
- `access_scope_hash`.

Citations live in `rag.query_audit_citations`.

Technical logs are structured JSON files, rotated daily on mounted volumes. Default retention is 30 days. Technical details are logged, not exposed to users.

All HTTP errors use the shared envelope:

```json
{
  "error": {
    "code": "STABLE_ERROR_CODE",
    "message": "Safe user-facing message",
    "details": {},
    "requestId": "request-id"
  }
}
```

Stable error codes are required for oversized imports, non-extractable imports, invalid lifecycle transitions, unauthorized access, indexing failures, viewer exchange failures, rate limits, and AI budget exhaustion.

## Configuration And Secrets

Sensitive values use Docker Compose secrets mounted as files:

- OpenAI API key.
- Postgres passwords.
- JWT signing/private key.
- Internal service tokens.

Non-sensitive runtime config uses environment variables:

- Internal URLs.
- Ports.
- Cache TTL.
- Model names.
- Environment flags.
- Customer timezone.
- Operational limits.

OpenAI defaults:

- Chat model: `gpt-4.1-mini`.
- Embedding model: `text-embedding-3-large`.
- Embedding dimensions: `1536`, passed through the OpenAI embeddings `dimensions` parameter for pgvector `vector(1536)` compatibility.

These are runtime configuration values, not hardcoded business logic. Model prices live in `rag.model_pricing`, and query audit stores the pricing snapshot used.

## Operational Defaults

| Setting | Default |
| --- | --- |
| Customer timezone | `UTC` unless configured |
| PDF/DOCX import size limit | `10 MB` per file |
| Chat question length | `4000` characters |
| Chat feedback comment length | `1000` characters |
| Review/comment field length | `2000` characters |
| Technical JSON log retention | `30` days |
| RAG semantic cache TTL | `24` hours |
| RAG semantic cache similarity threshold | `0.90` |
| OpenAI embedding dimensions | `1536` |
| Chat access token TTL | `15` minutes |
| Viewer exchange code TTL | `60` seconds |
| Viewer access token TTL | `15` minutes |
| Login rate limit by IP | `5` attempts per minute |
| Login rate limit by user | `10` attempts per 15 minutes |
| Chat request rate limit | `30` questions per minute per user |
| Import extraction rate limit | `10` imports per hour per user |
| Viewer exchange rate limit | `30` exchanges per minute per user |
| AI usage budget | `USD 5` per calendar month per user |

## Health And Readiness

The MVP includes:

- Postgres readiness check.
- .NET `/health/live` and `/health/ready`.
- FastAPI `/health/live` and `/health/ready`.
- Frontend HTTP health checks.
- Caddy routing/health check.

Readiness must verify critical dependencies such as DB connectivity and required secrets presence.

## UI Foundation

All three frontends use:

- React with strict TypeScript.
- Tailwind CSS.
- `shadcn/ui`.
- `lucide-react`.

The product UI should feel like a dense operational SaaS tool, not a landing page. Prefer compact tables, filters, forms, detail panels, tabs, dialogs, menus, badges, and status indicators. Avoid marketing hero sections, decorative gradients, and visually noisy layouts in product surfaces.

Management UI includes documents, users/groups, audit, feedback review, AI budget configuration, and system configuration.

Chat UI prioritizes fast question entry, citations, answer feedback, expired/budget-limited states, and safe links to `docs.client.com`.

Docs UI prioritizes readable document content, token exchange/loading states, expired-code handling, unauthorized states, token-expired states, and successful document rendering.

## Invariants

1. Public chat must never retrieve from non-`Published` content.
2. `docs.client.com` must revalidate document access; links are not authorization.
3. FastAPI internal indexing endpoints must not be publicly exposed.
4. .NET owns the `app` schema; FastAPI owns the `rag` schema.
5. Main user JWT/session tokens must not be placed in URLs or browser storage.
6. Cached answers must only be reused for matching effective access scopes.
7. Functional audit must be persisted in Postgres, not only in logs.
8. API implementation details must preserve the approved contract groups and shared error envelope.
9. AI budget exhaustion must not block authorized document viewing.

## Implementation Planning Notes

The implementation plan should split work into small, verifiable units:

1. Repository scaffold and Compose baseline.
2. Database schema/migration foundations for `app` and `rag`.
3. Auth/session/cookie/CSRF foundation.
4. Document lifecycle and assisted imports.
5. Internal indexing contract and RAG schema.
6. Chat query flow with audit, cost snapshots, and budget enforcement.
7. Viewer exchange-code flow.
8. Feedback and management reporting.
9. UI workflows for management, chat, docs, and budget configuration.
10. Health checks, logs, operational defaults, and deployment hardening.

Before coding each contract group, the implementation plan must define exact endpoint DTOs, validation rules, stable error codes, and tests for that group.
