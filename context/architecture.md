# Architecture Context

## Stack

| Layer | Technology | Role |
| --- | --- | --- |
| Reverse proxy | Caddy | Public TLS termination, subdomain routing, request IDs, access logs |
| Management frontend | React | Corporate document management UI |
| Chat frontend | React | End-user chatbot UI |
| Document viewer frontend | React | Token-gated document viewer |
| Management/API backend | .NET 8 | Authentication, users, roles, document lifecycle, session-authenticated document viewing, management audit |
| RAG backend | FastAPI | Public chat API, retrieval, semantic cache, embeddings, RAG audit, indexing worker |
| Database | PostgreSQL with pgvector | Relational data, document content, audit, vectors, cache, indexing jobs |
| AI provider | OpenAI | Chat completions/responses and embeddings |
| Document image object storage | MinIO / S3-compatible object storage | Private binary storage for document images referenced by canonical HTML |
| Deployment | Docker Compose | Single-tenant customer deployment |
| Secrets | Docker Compose secrets | Sensitive configuration mounted as files |
| Logs | JSON files on mounted volumes | Daily per-service technical logs |

## Deployment Model

The product starts as a single-tenant deployment. Each customer receives an isolated Docker Compose stack, isolated PostgreSQL database, isolated secrets, and isolated logs. Kubernetes is out of scope for the MVP.

## Public Routes

| Host | Target |
| --- | --- |
| `manage.client.com` | Management React frontend |
| `chat.client.com` | Chat React frontend |
| `docs.client.com` | Document viewer React frontend |
| `api.client.com` | Optional controlled direct .NET API surface for technical/API access |
| `rag.client.com` | Optional controlled direct FastAPI chat API surface for technical/API access |

FastAPI internal indexing endpoints are not exposed through Caddy. The .NET API calls FastAPI over the Docker network at `http://rag-api:8000/internal/indexing-jobs` using an internal service token from Compose secrets.

Browser frontends should call same-origin `/api/*` routes exposed on their own host instead of calling cross-origin backend hosts directly:

| Browser Host | Same-Origin API Routes | Upstream |
| --- | --- | --- |
| `manage.client.com` | `/api/*` | .NET API |
| `chat.client.com` | `/api/chat/*`, `/api/feedback/*`, chat health routes | FastAPI |
| `chat.client.com` | `/api/auth/*`, `/api/session/*` | .NET API |
| `docs.client.com` | `/api/*` | .NET API |

This same-origin gateway pattern is the preferred browser topology for the MVP because it supports host-only cookies, reduces CORS complexity, and avoids broad parent-domain cookies.

## API Contract Strategy

The architecture spec defines MVP API contracts by contract group rather than exhaustively documenting every endpoint field up front. Each group must identify the owning service, public or internal exposure, primary routes, authorization requirements, stable error codes, lifecycle side effects, and representative request/response DTO shape.

Detailed endpoint-by-endpoint DTOs are finalized during implementation planning and coding, but they must not violate the service ownership, lifecycle, authorization, audit, and error-envelope rules defined in the architecture context.

## MVP API Contract Groups

| Contract Group | Owner | Exposure | Responsibilities |
| --- | --- | --- | --- |
| Auth/session | .NET API | Browser same-origin `/api/auth/*` and `/api/session/*`; internal `/internal/session/validate` | Login, logout, current session, CSRF token support, secure session cookies, and internal session validation for FastAPI |
| Users/groups | .NET API | Management same-origin `/api/*` | User administration, role assignment, group/department management, activation/deactivation |
| Documents | .NET API | Management same-origin `/api/*` | Document CRUD, metadata, filters, assisted import extraction, lifecycle transitions, publish request, indexing retry, archive, restore, and management audit |
| Viewer | .NET API | Docs same-origin `/api/*`; link creation from chat/management | Session-authenticated document links, document access validation, and document loading |
| Chat/RAG | FastAPI | Chat same-origin `/api/chat/*` through Caddy | Question submission, retrieval, answer generation, citations, semantic cache lookup/write, RAG query audit, and token/cost/latency tracking |
| Feedback/reporting | FastAPI and .NET API | Chat feedback via FastAPI; management reporting via .NET | Feedback submission tied to RAG query audit, plus read-only management feedback review/reporting over RAG audit data |
| Usage budgets | .NET API and FastAPI | Management configuration via .NET; enforcement in FastAPI | Per-user monthly AI budget configuration, usage reporting, and chat budget enforcement based on RAG cost audit |
| Internal indexing | FastAPI, called by .NET API | Docker-network-only internal API | Indexing job creation, status/result reporting, retry support, chunking, embeddings, vector storage, and indexing audit fields |
| Health/errors | All services | Public or internal as appropriate | Liveness/readiness endpoints, shared error envelope, request IDs, and safe error codes |

Management and docs frontends call the .NET-owned contract groups. The chat frontend calls FastAPI for chat and feedback, and calls .NET through same-origin chat routes only for auth/session and viewer-link support. Management reporting reads RAG audit/feedback through .NET-controlled read-only reporting queries or views; the management frontend does not call FastAPI directly.

## Monorepo Boundaries

| Path | Ownership |
| --- | --- |
| `apps/manage-web` | Management frontend |
| `apps/chat-web` | Chat frontend |
| `apps/docs-web` | Document viewer frontend |
| `services/dotnet-api` | .NET API and `app` schema owner |
| `services/rag-api` | FastAPI RAG service and `rag` schema owner |
| `infra/compose` | Docker Compose, Caddy, volumes, healthchecks, secrets wiring |
| `docs` | Product and technical documentation |
| `context` | Durable project memory and current design state |

## Storage Model

- **PostgreSQL database:** one database per customer deployment.
- **`app` schema:** owned by .NET. Stores users, roles, groups/departments, documents, document metadata, lifecycle state, permissions, session-authenticated document access data, and management audit events.
- **`rag` schema:** owned by FastAPI. Stores indexing jobs, document chunks with embeddings, semantic cache entries and sources, query audit events with simple feedback, query audit citations, and model pricing.
- **Document source content:** canonical normalized HTML and metadata are stored in Postgres.
- **Document image content:** image bytes are stored in private S3-compatible object storage, MinIO in the Docker Compose MVP. Postgres stores only image metadata, document ownership, version references, content type, size, checksum, object key, and audit fields. Canonical document HTML must reference images through stable same-origin application URLs such as `/api/document-images/{imageId}/content`; it must not store base64 image data, raw MinIO URLs, raw AWS S3 URLs, or expiring presigned URLs.
- **Imported files:** PDF/DOCX originals are not retained in the MVP. Imports are assisted extraction flows owned by the .NET management API: .NET accepts PDF/DOCX uploads up to 10 MB, extracts text from the uploaded file, and returns it to `manage.client.com`, which inserts it into the document editor so the user can correct formatting, structure, and attributes before saving. The system stores the user-edited normalized HTML plus import metadata such as original filename, MIME type, size, hash, importer, timestamp, extraction result, and file size.
- **Import extraction libraries:** .NET uses `DocumentFormat.OpenXml` for DOCX extraction and `PdfPig` for PDF extraction. These libraries are used only for assisted text extraction into the editor, not for final formatting or publication decisions.

## Auth And Access Model

- .NET is the authentication authority.
- Users are local database users in the `app` schema.
- .NET handles password hashing, login, secure browser session issuance, JWT/session claims, user administration, roles, and groups/departments.
- Browser-facing sessions use secure cookies instead of tokens stored in browser storage. Session/auth cookies must be `HttpOnly`, `Secure`, and `SameSite`, with CSRF protection for state-changing requests.
- JWTs, refresh tokens, session IDs, and other credential-bearing tokens must not be stored in `localStorage` or `sessionStorage`.
- SameSite cookies are defense in depth, not the only CSRF defense. Mutating browser requests must include an approved CSRF mitigation such as synchronizer tokens or a signed double-submit cookie/header pattern.
- Browser session cookies must be host-only `__Host-` prefixed cookies when set through the same-origin frontend hosts. Avoid broad parent-domain cookies such as `Domain=.client.com` for the MVP.
- Unified auth uses `__Host-session` as the only browser auth cookie. FastAPI chat/feedback read the `__Host-session` cookie and validate it through `.NET` internal `GET /internal/session/validate` on cache miss with `X-Internal-Service-Token`.
- FastAPI caches safe session claims in process for 60 seconds keyed by a SHA-256 hash of the session cookie value. Raw session cookies must not be logged.
- .NET remains the authority for login, session issuance, user state, role/group assignment, internal session validation, and token signing key management.
- Legacy `POST /api/auth/chat-token`, `__Host-chat-token`, `/api/viewer/exchange`, and `__Host-viewer-token` runtime flows have been removed. The deprecated viewer exchange/audit tables are not part of the current EF model or initial app-schema migration.
- Roles define system capabilities.
- Groups/departments and document attributes define content access.
- Document access in the MVP is group/department-based plus document attributes. Per-user document access exceptions are out of scope. If a customer needs an exception, administrators create a specific group and assign the user to that group.
- RAG retrieval and semantic cache matching must use the effective access scope.
- Viewer links use document-id locators only; the user's authenticated session is revalidated server-side before document content is returned.

## CSRF Strategy

- Browser-facing mutating requests use a signed double-submit CSRF cookie/header pair shared by `.NET` and FastAPI.
- `.NET` exposes `GET /api/csrf` (non-mutating) that issues a signed request token in the response header `X-CSRF-Token` and sets the same token in a host-only `__Host-CSRF` cookie (`HttpOnly`, `Secure`, `SameSite=Strict`, `Path=/`).
- Frontends call `GET /api/csrf` on app boot and after each session change, store the response header value in memory (not localStorage), and send it as the `X-CSRF-Token` header on every state-changing request.
- `.NET` validates the HMAC-signed header token against the `__Host-CSRF` cookie on every mutating endpoint before route handling.
- `chat.client.com` mutating requests against FastAPI (e.g., `POST /api/chat`, `POST /api/feedback`) send the same `X-CSRF-Token` header from `chat-web`; FastAPI validates the `__Host-CSRF` cookie/header pair locally before session validation.
- SameSite=Strict on the auth/session cookies is treated as defense in depth, not the only CSRF defense.

## Local Development HTTPS

- Local development uses Caddy with its **internal CA**. Caddy issues certificates automatically for `manage.localhost`, `chat.localhost`, and `docs.localhost`, and developers trust Caddy's root CA once per workstation (`caddy trust`).
- `__Host-` cookies remain `Secure` in local development because Caddy serves HTTPS locally. There is no `Secure=false` exception in any environment.
- The `.localhost` TLD resolves to `127.0.0.1` per RFC 6761; no `hosts` file edits are required.
- Because the project Caddy instance runs inside Docker Compose, local Windows developers should use `infra/compose/Start-Local.ps1 -TrustCaddyCertificate` to copy and trust the Docker-generated Caddy root CA. Running `caddy trust` on the host only trusts a separate host-installed Caddy instance and does not trust the Compose container CA.
- The local Compose override may expose the MinIO admin console on `127.0.0.1:9001` for developer inspection of the private document image bucket. Production Compose must not publish the MinIO console or S3 API ports; runtime document image access remains authorized and streamed through `.NET`.

## JWT Signing Key Rotation

- Any JWTs issued by `.NET` are signed with **RS256**.
- `.NET` maintains **two active keys** at all times: `current` (used for new tokens) and `previous` (only used for validation during the rotation overlap window).
- Each issued token includes the `kid` (key id) header so the verifier can pick the right public key.
- Public keys are exposed by `.NET` at `GET /.well-known/jwks.json` for internal consumers (FastAPI). FastAPI fetches and caches the JWKS at startup and refreshes every 5 minutes.
- Rotation procedure for MVP:
  1. Operator generates a new key pair offline.
  2. Operator updates the Compose secret `jwt_signing_keys.json` to contain `[{kid, public, private, status: "current"}, {kid, public, private, status: "previous"}]` (the old `current` becomes `previous`; the old `previous` is dropped).
  3. Rolling restart of the `.NET` service.
  4. FastAPI picks up the new JWKS within 5 minutes; restart FastAPI to accelerate.
- Tokens with a retired `kid` fail validation with `AUTH_TOKEN_INVALID_KEY`.
- The MVP does not include automated rotation. Rotation cadence is a customer operational policy.

## Internal Service Token

- `.NET â†’ FastAPI` internal calls (`/internal/indexing-jobs`, `/internal/cache-invalidations`) carry an `X-Internal-Service-Token` header.
- The token is a 256-bit random value mounted on both services as the Compose secret `internal_service_token`. Both services read it from the file on startup.
- The token is static in the MVP. Rotation is manual: update the secret file, rolling restart both services.
- FastAPI rejects requests missing or mismatching the token with `AUTH_INTERNAL_TOKEN_INVALID` and never logs the token value.

## Roles

| Role | Permissions |
| --- | --- |
| `Admin` | Full management access, including publishing, user creation, role/status changes, group management, group assignment, AI budget limits, audit, feedback, configuration, chat, docs, and account self-service. |
| `DocumentManager` | Can use Viewer self-service, create/read/update/import/archive eligible draft or in-review documents, restore archived documents to draft, edit metadata, send documents to review, view all users and AI balances, create/edit groups, assign users to groups, and view audit/feedback/configuration read models. Cannot publish, create users, change roles/status, or modify AI budget limits. |
| `Viewer` | Can use chat, open allowed published documents in `docs.client.com`, and access a limited `manage.client.com` self-service surface for own account, own AI usage balance, and safe read-only configuration. Cannot access full management workspaces. |

## Viewer Document Access

`docs.client.com` is session-gated. Links from `manage.client.com` and `chat.client.com` carry only a `documentId` query value, never a credential-bearing token. `docs.client.com` calls same-origin `.NET` routes with its authenticated `__Host-session` cookie, and `.NET` revalidates the current user, role, groups, document state, and permissions before returning document content.

Chat-created document links are limited to `Published` documents. Management-created links may allow `Draft`, `In Review`, and `Published` when the user has `Admin` or `DocumentManager`. The link itself is not authorization; it is only a document locator.

The main session token and any other credential-bearing token must not be placed in document viewer URLs. Unauthorized or missing-session document requests must return the shared error envelope without exposing document details.

## Document Lifecycle And Indexing

Document states are `Draft`, `In Review`, `Published`, and `Archived`.

- The MVP uses simple formal versioning. Each successful publication creates an immutable version number such as `version 1`, `version 2`, and so on.
- The document viewer and public RAG use the latest successfully published version.
- Version records preserve the normalized HTML, metadata snapshot, publication timestamp, publisher, and indexing reference used for that version.
- Editing an already published document creates a new draft version. The latest published version remains active until the new draft completes review, pre-publication indexing, and publication.
- Draft versions can move through `Draft` and `In Review`; published versions are immutable.
- `Admin` publishes documents directly from `In Review` to `Published`.
- `DocumentManager` can create, edit, import, update metadata, and send documents to `In Review`, but cannot publish.
- A separate `Reviewer` role is not part of the MVP.
- Importing a PDF/DOCX creates or fills a draft editor with extracted text; it does not automatically create publishable formatted content.
- PDF/DOCX extraction is a management-domain responsibility in the .NET API, not a RAG responsibility in FastAPI.
- DOCX extraction uses `DocumentFormat.OpenXml`; PDF extraction uses `PdfPig`.
- The initial PDF/DOCX import upload size limit is 10 MB per file and must be enforced by the .NET API. The management UI should validate the size before upload when possible.
- Files over the import size limit are rejected with the shared error envelope and a stable validation error code.
- Scanned PDFs or files with no extractable text are rejected in the MVP with a clear extraction error. OCR is out of scope.
- The stable validation error code for files with no extractable text is `IMPORT_TEXT_NOT_EXTRACTABLE`.
- If the user abandons the import flow without saving the draft, the extracted text, import metadata, and extraction result are not persisted as business data.
- Unsaved extraction requests may still appear in normal sanitized technical logs with request ID, status, duration, and safe error code.
- The user remains responsible for final formatting, title, document type, access attributes, and review readiness after import.
- Moving a draft to `In Review` requires title, document type, allowed groups/departments, audience/user type, non-empty sanitized HTML content, and valid sanitized content. Tags are optional.
- Sending a draft to `In Review` may include an optional internal review comment. Returning or rejecting a version from `In Review` back to `Draft` requires an internal comment explaining the reason.
- Review comments are stored in the `app` schema and included in the management audit trail.
- `Admin` and `DocumentManager` can return or reject a version from `In Review` back to `Draft` when they provide the required internal comment. This does not grant `DocumentManager` permission to publish.
- Return/rejection audit events must capture actor, previous state, new state, required comment, timestamp, and request ID.
- A publish action starts pre-publication indexing through FastAPI's internal indexing endpoint.
- The document remains `In Review` while pre-publication indexing is pending or running.
- The document transitions to `Published` only after FastAPI indexes it successfully.
- If pre-publication indexing fails, the document is not published and remains unavailable to public chat and normal viewers.
- Pre-publication indexing failures are surfaced in the management UI with a safe error summary and a manual retry action.
- Archiving applies to the entire document, not a single version. Archived documents are unavailable to public viewer and public chat, while versions and audit history are retained.
- `Admin` can archive any document, including `Published` documents.
- `DocumentManager` can archive only documents whose current lifecycle state is `Draft` or `In Review` and that do not have an active published version.
- Archiving any document with an active published version requires `Admin`.
- Archiving triggers removal or deactivation from the active RAG corpus and invalidates semantic cache entries that cite the archived document.
- `Admin` and `DocumentManager` can restore any archived document to `Draft`.
- Restoring an archived document does not reactivate any previous published version, public viewer access, public chat retrieval, or RAG corpus entry. It must pass review, pre-publication indexing, and publication again.
- The MVP does not include a separate soft delete state. `Archived` is the only functional removal path.
- Public chat uses only `Published` content.
- Internal preview can use `Draft`, `In Review`, and `Published` for authorized management users.
- Indexing is asynchronous.
- The .NET API requests indexing by calling an internal FastAPI endpoint.
- FastAPI creates and owns `rag.indexing_jobs`.
- FastAPI receives only saved normalized document content for indexing; it does not parse PDF/DOCX imports in the MVP.
- Document image upload, metadata, authorization, and serving are owned by `.NET` because they are part of document lifecycle and viewer access. FastAPI does not write document image metadata or objects. The first RAG image update remains text-first: indexing converts accessible image descriptions and captions from saved HTML into ordinary chunk text, but it does not persist image URLs, fetch image bytes, or send query-time multimodal OpenAI image inputs.
- Indexing jobs store state, attempts, technical error, timestamps, and document references.

## Semantic Cache

The RAG service uses semantic cache by question embedding. Cache entries may return complete cached answers with original citations only when the effective access scope matches.

Cache matching uses at least:

- corpus: `published` or `preview`
- `access_scope_hash`
- normalized question embedding
- similarity threshold, initially `0.90`
- expiration timestamp
- source document IDs

Cache invalidation rules:

- If a source document is modified, archived, deleted, changes state, or changes accessibility, cached answers referencing that document are deleted.
- Creating a new document does not invalidate existing cache entries.
- TTL is configurable by customer with default `24` hours, using an environment variable such as `RAG_SEMANTIC_CACHE_TTL_HOURS`.
- Semantic cache similarity threshold is configurable by customer with default `0.90`, using an environment variable such as `RAG_SEMANTIC_CACHE_SIMILARITY_THRESHOLD`.

## AI Usage Budgets

The MVP supports per-user monthly AI usage budgets configured in monetary value, initially USD. The default monthly budget is USD 5 per user unless an `Admin` configures another value. Budget periods use the customer deployment's configured timezone and reset by calendar month, from the first day through the last day of that month. `Admin` users can set and adjust a user's monthly budget from the management app. `Viewer` and `DocumentManager` users can inspect budget status and available balance according to their role: Viewers see only their own balance, while DocumentManagers can inspect all user balances but cannot modify limits. Budget configuration is owned by the .NET API in the `app` schema and changes are captured in management audit.

FastAPI enforces the budget before starting new paid AI work for chat. Enforcement reads `.NET`-owned `app.user_ai_budget_limits` through an explicitly granted read-only database path, then combines the configured budget with the user's current-period spend calculated from `rag.query_audit_events`, where each query stores the actual model IDs, token usage, pricing snapshot, and estimated cost. Budget checks must happen before semantic cache lookup, embeddings, or LLM generation so over-budget users do not keep generating cost.

When a user reaches the configured budget, chat returns the shared error envelope with a stable code such as `AI_BUDGET_EXCEEDED`. The management app can increase the user's budget, disable the budget, or wait for the next monthly period depending on the configured policy. Blocked attempts should be auditable without adding model cost.

Budget exhaustion blocks new chat/RAG work that can generate AI provider cost. It does not block the document viewer, management workflows, or access to already authorized documents. In the MVP, over-budget users do not receive semantic cached answers because semantic cache lookup requires question embedding and could generate provider cost. Exact no-cost cache lookup is deferred.

Rate limits and AI usage budgets are separate controls. Rate limits protect service stability and abuse by request frequency. AI usage budgets control monthly monetary spend.

## Audit, Logging, And Error Handling

- Management audit lives in `app.audit_events`.
- RAG query audit lives in `rag.query_audit_events`.
- RAG query audit stores user, question, answer, cache hit, simple feedback, model, input/cached/output tokens, estimated cost, latency, request ID, and `access_scope_hash`.
- Cited documents are stored as child audit rows in `rag.query_audit_citations` so reporting can filter by cited document/version without treating citations as a separate product module.
- Chat answer feedback uses thumbs up/down plus an optional sanitized comment. For the MVP, one feedback value per answer is stored directly on `rag.query_audit_events`.
- Feedback is tied to the original RAG query audit record for the generated or cached answer and is submitted to FastAPI by the authenticated chat user. For the MVP, the same user may update their feedback on the same answer, overwriting the single feedback value/comment and updating `feedback_updated_at`; historical feedback changes are not retained. If future requirements need multiple reviewers or feedback history, feedback can be split into a child table later.
- `manage.client.com` includes a feedback review view for `Admin` and `DocumentManager`.
- The feedback review view is served by the .NET API through read-only reporting views created in the `rag` schema by FastAPI migrations and granted to the .NET reporting connection as read-only access. The management frontend does not call FastAPI directly.
- The MVP feedback review filters are negative feedback, cited document, user, and date range.
- Pricing lives in versioned `rag.model_pricing`; each query audit stores the pricing snapshot used.
- RAG migrations seed initial active pricing rows for the default OpenAI models so a clean Compose deployment can answer chat without a manual SQL pricing step. Operators must review and update pricing rows when model prices or configured provider/model choices change.
- FastAPI readiness fails if `rag.model_pricing` lacks active rows for the configured `OPENAI_CHAT_MODEL` or `OPENAI_EMBEDDING_MODEL`. Runtime chat requests must fail safely with `RAG_PROVIDER_MISCONFIGURED` rather than estimating provider cost as zero.
- Technical logs are structured JSON files, rotated daily per service on mounted volumes.
- HTTP errors use the shared envelope: `{ "error": { "code", "message", "details", "requestId" } }`.
- Technical details are logged, not exposed to users.

## Configuration And Secrets

Sensitive values use Docker Compose secrets mounted as files, including OpenAI API key, Postgres passwords, JWT signing/private key sets, CSRF signing key, and internal service tokens. The management configuration endpoint may check secret file presence from `.NET` for status reporting, but it must never return secret values to the browser.

Postgres uses separate Compose secret files for the admin/superuser password and each service role password:

- `postgres_admin_password.txt`
- `postgres_app_password.txt`
- `postgres_rag_password.txt`
- `postgres_reporting_password.txt`

The `postgres-init` service reads those files to create or update the `app_owner`, `rag_owner`, and `app_reporting_reader` role passwords. Runtime services read their own password files and must not receive database passwords as plain environment variable values.

Non-sensitive runtime configuration uses environment variables, including internal URLs, ports, cache TTL, model names, and environment flags.

Document image object storage uses separate Compose secrets for the S3-compatible access key and secret key. The MinIO root user/password are operational bootstrap secrets and must not be reused by runtime services. The document image bucket is private; browser image requests always pass through `.NET` authorization and object streaming.

OpenAI models are configurable with environment variables such as `OPENAI_CHAT_MODEL`, `OPENAI_EMBEDDING_MODEL`, and `OPENAI_EMBEDDING_DIMENSIONS`. Model prices are stored in the database.

The current default chat model is `gpt-4.1-nano` to minimize cost while the product is being validated. The current RAG schema uses `OPENAI_EMBEDDING_DIMENSIONS=1024` and pgvector `vector(1024)`. `infra/compose/.env.example` sets `OPENAI_EMBEDDING_MODEL=text-embedding-3-small`. These defaults remain runtime configuration values, not hardcoded business logic. Chat response speed, answer quality, and cost are product quality attributes and must be tracked through latency, feedback, and cost metrics in RAG query audit and logs.

## Operational Defaults

The MVP starts with conservative operational defaults that are configurable per deployment:

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
| Login rate limit by IP | `5` attempts per minute |
| Login rate limit by user | `10` attempts per 15 minutes |
| Chat request rate limit | `30` questions per minute per user |
| Import extraction rate limit | `10` imports per hour per user |
| AI usage budget | `USD 5` per calendar month per user |

Rate limit exceedances must use stable safe error codes and must not expose internal implementation details. AI usage budget exhaustion is handled separately with `AI_BUDGET_EXCEEDED`.

## Migrations

- .NET uses EF Core migrations for the `app` schema.
- FastAPI uses Alembic migrations for the `rag` schema.
- Services must not modify tables outside their owned schema except through explicitly granted read permissions or internal API contracts.
- FastAPI may read `.NET`-owned `app.document_permissions` for retrieval permission filtering and `app.user_ai_budget_limits` for budget enforcement through explicit read-only grants applied by `.NET` EF migrations after those tables exist. FastAPI must not write to any `app` table.

## Initial Database Entities

### `app` Schema, Owned By .NET

- `users`
- `roles`
- `user_roles`
- `groups`
- `user_groups`
- `documents`
- `document_versions`
- `document_permissions` for group/department and attribute-based access rules, not per-user exceptions
- `document_tags`
- `review_comments`
- `import_metadata`
- `user_ai_budget_limits`
- `audit_events`

### `rag` Schema, Owned By FastAPI

- `indexing_jobs`
- `document_chunks` with the `vector(1024)` embedding stored on the chunk row
- `semantic_cache_entries`
- `semantic_cache_sources`
- `query_audit_events` with one simple feedback value/comment per answer
- `query_audit_citations` as child rows of query audit events
- `model_pricing`

## Health Checks

The MVP includes health/readiness endpoints and Compose healthchecks:

- Postgres readiness check (`pg_isready` against the deployment user and database).
- .NET `/health/live` (always 200 if the process is up) and `/health/ready` (verifies DB connectivity, ability to read each Compose secret file, and JWT signing key load).
- FastAPI `/health/live` (always 200 if the process is up) and `/health/ready` (verifies DB connectivity, pgvector extension presence, OpenAI API key file readable, JWKS fetched from .NET, and internal service token loaded).
- Frontend HTTP health checks: each frontend returns `200` on `/` once Vite preview/served bundle is up.
- Caddy admin endpoint `/-/health` (or equivalent) to verify route configuration loaded.
- MinIO readiness check uses MinIO health endpoints and bucket initialization must complete before runtime image upload is considered ready.

Readiness must verify critical dependencies such as DB connectivity and required secrets presence. The Compose `depends_on` graph uses `service_healthy` for the migration ordering described in Operations.

## Operations

### Database Initialization

- A dedicated short-lived Compose service `postgres-init` runs once on stack start, after `postgres` is healthy and before `dotnet-api` or `rag-api` start.
- `postgres-init` runs an idempotent SQL script that:
  1. Creates the deployment database if it does not exist.
  2. Creates the `pgvector` extension (`CREATE EXTENSION IF NOT EXISTS vector`).
  3. Creates the database roles used by the services:
     - `app_owner` (owns the `app` schema, used by `.NET`)
     - `rag_owner` (owns the `rag` schema, used by FastAPI)
     - `app_reporting_reader` (read-only across the FastAPI-owned reporting views, used by `.NET` for management reporting)
  4. Sets or updates those role passwords from Compose secret files.
  5. Creates the schemas `app` and `rag` with the right owners.
  6. Grants schema USAGE to the reporting reader role.
  7. Grants schema USAGE on `app` to `rag_owner` so later table-level read grants can be used.
- `postgres-init` exits 0 once the script completes. Compose's `depends_on: service_completed_successfully` gates `dotnet-api` and `rag-api` on this.

### Migration Order

- `.NET` runs EF Core migrations on startup against the `app` schema only.
- FastAPI runs Alembic migrations on startup against the `rag` schema only.
- Both can run in parallel because their schemas do not overlap. The only cross-schema interaction is FastAPI creating reporting views and granting SELECT to `app_reporting_reader` (next section).
- `.NET` EF migrations own table-level read grants from `app` to `rag_owner` because `postgres-init` runs before `.NET` tables may exist. EF migrations conditionally grant `SELECT` on `app.document_permissions` and `app.user_ai_budget_limits` after creating or renaming those tables.
- Each service's `entrypoint` script is `wait-for-postgres && run-migrations && start-server`. On migration failure, the container exits non-zero and Compose's restart policy retries with backoff. Operators see the failure in the technical JSON log under event `migration.failed` with the SQL error.

### Initial Operational Data

- EF migrations seed a default administrator account for controlled deployments:
  - Email: `admin@admin.com`
  - Initial password: `admin`
  - Role: `Admin`
  - AI budget: default USD 5 monthly budget.
- This account exists to minimize first-run Compose setup steps. Operators must change or replace the password immediately after first login in any non-throwaway deployment.
- Alembic migrations seed initial active pricing rows for `gpt-4.1-nano` and `text-embedding-3-small` based on OpenAI pricing visible on 2026-05-22. Pricing rows are operational configuration, not contractual billing truth; operators must update them when changing models or when provider pricing changes.

### Reporting Views

- The management feedback review reads from FastAPI-owned reporting views in the `rag` schema:
  - `rag.v_query_audit_with_citations`
  - `rag.v_feedback_summary`
- Both views are created by Alembic migrations and explicitly `GRANT SELECT ... TO app_reporting_reader`.
- `.NET` uses a separate read-only connection string (`Postgres__ReportingConnectionString`) authenticated as `app_reporting_reader`. The connection is configured with `SET search_path = rag, public` so view names are unqualified in queries.
- `.NET` must not write to or alter any `rag` object. The reporting reader role has no INSERT/UPDATE/DELETE grants by design.

### Request ID Propagation

- Caddy generates `X-Request-ID` on every incoming public request and passes it upstream. If the client already sent one, Caddy preserves it.
- `.NET` reads `X-Request-ID` from the request, enriches every log line via Serilog's correlation enricher, and forwards it on outbound calls to FastAPI (`X-Request-ID` header on `HttpClient`).
- FastAPI reads `X-Request-ID` via `asgi-correlation-id`, binds it to `structlog` context, and forwards it on outbound calls to OpenAI when supported (OpenAI SDK accepts `extra_headers`).
- The request id appears in the shared error envelope's `requestId` field on every error response.

### Tracing And Metrics

- OpenTelemetry is **out of scope for the MVP**. Structured logs with request ids and audit tables cover the MVP's observability needs.
- Adding OpenTelemetry is a v1.1 work item once the platform proves stable.

### CI/CD

- CI runs on **GitHub Actions** with one workflow file per service plus a top-level `lint-and-test.yml` for cross-cutting checks.
- Per-service jobs:
  - `services/dotnet-api`: `dotnet restore` â†’ `dotnet build --no-restore` â†’ `dotnet test` against Testcontainers Postgres.
  - `services/rag-api`: `uv sync` â†’ `uv run ruff check` â†’ `uv run pytest` against Testcontainers Postgres with `pgvector` extension preinstalled.
  - `apps/*`: `pnpm install --frozen-lockfile` â†’ `pnpm -r lint` â†’ `pnpm -r test` â†’ `pnpm -r build`.
- Docker image builds are gated on tests passing and tagged with the short Git SHA. Images are published to GitHub Container Registry (`ghcr.io/<org>/<service>:<sha>`).
- Production deployment is manual: an operator pulls the tagged images on the customer host and runs `docker compose up -d`.

### Backup And Restore

- Postgres backups for the MVP use a dedicated `postgres-backup` service in Compose that runs `pg_dump --format=custom` daily, writing to a mounted host volume under `/var/backups/postgres/<customer>/`.
- Retention default: **14 daily dumps**, configurable via `POSTGRES_BACKUP_RETENTION_DAYS`.
- Restore procedure is documented in `docs/operations/restore.md` (created during Task 16 of the implementation plan).
- Backups are not encrypted at rest in the MVP. Customer-side disk encryption is assumed; explicit encrypted backup is a v1.1 concern.

### Log Rotation And Retention

- Both `.NET` and FastAPI write daily-rotated JSON log files to `/var/log/<service>/` inside the container, mounted to a named volume on the host.
- Retention default: **30 days** (`LOG_RETENTION_DAYS`), enforced by each service's log sink configuration.
- Log files are not shipped to a remote aggregator in the MVP.

## Invariants

1. Public chat must never retrieve from non-`Published` content.
2. `docs.client.com` must revalidate document access; links are not authorization.
3. FastAPI internal indexing endpoints must not be publicly exposed.
4. `.NET` owns the `app` schema; FastAPI owns the `rag` schema.
5. The main user JWT must not be placed in URLs.
6. Cached answers must only be reused for matching effective access scopes.
7. Management audit and RAG query audit must be persisted in Postgres, not only in logs.
8. API implementation details must preserve the approved MVP contract groups and shared error envelope.
