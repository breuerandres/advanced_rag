# Design Decisions

This is a chronological log. For the **current effective rules** see `context/architecture.md`, `context/code-standards.md`, `context/rag-spec.md`, `context/ui-context.md`, and `context/code-patterns.md`. Use this file when you need to know *why* a rule exists or what alternatives were considered.

## Topic Index

Jump to the relevant decision group below. Section names match the `##` headings in the chronological log.

### Architecture And Service Boundaries

- [Base Architecture Scope](#2026-05-11---base-architecture-scope)
- [Single-Tenant Docker Compose Deployment](#2026-05-11---single-tenant-docker-compose-deployment)
- [Monorepo Compose Product](#2026-05-11---monorepo-compose-product)
- [Service Boundaries](#2026-05-11---service-boundaries)
- [API Contract Granularity](#2026-05-11---api-contract-granularity)
- [MVP API Contract Groups](#2026-05-11---mvp-api-contract-groups)
- [Management Reporting Read Model](#2026-05-11---management-reporting-read-model)

### Authentication, Sessions, And Access

- [Authentication And Roles](#2026-05-11---authentication-and-roles)
- [MVP Document Access Rules](#2026-05-11---mvp-document-access-rules)
- [Browser Session Storage](#2026-05-11---browser-session-storage)
- [Browser API And Cookie Topology](#2026-05-11---browser-api-and-cookie-topology)
- [FastAPI Chat Token Validation](#2026-05-11---fastapi-chat-token-validation)
- [Chat Token Refresh](#2026-05-11---chat-token-refresh)
- [Viewer Access Tokens](#2026-05-11---viewer-access-tokens)
- [Viewer Access Token TTL](#2026-05-11---viewer-access-token-ttl)
- [Viewer Access Token Reuse](#2026-05-11---viewer-access-token-reuse)

### Document Lifecycle And Versioning

- [Document Lifecycle And RAG Corpus](#2026-05-11---document-lifecycle-and-rag-corpus)
- [MVP Publishing Authority](#2026-05-11---mvp-publishing-authority)
- [Publication Requires Successful Indexing](#2026-05-11---publication-requires-successful-indexing)
- [Manual Retry For Pre-Publication Indexing Failure](#2026-05-11---manual-retry-for-pre-publication-indexing-failure)
- [Simple Formal Document Versioning](#2026-05-11---simple-formal-document-versioning)
- [Editing Published Instructions Creates Draft Version](#2026-05-11---editing-published-instructions-creates-draft-version)
- [Archive Entire Instruction](#2026-05-11---archive-entire-instruction)
- [Archive Authority](#2026-05-11---archive-authority)
- [Restore Archived Instruction To Draft](#2026-05-11---restore-archived-instruction-to-draft)
- [Restore Authority](#2026-05-11---restore-authority)
- [No Separate Soft Delete In MVP](#2026-05-11---no-separate-soft-delete-in-mvp)
- [Minimum Validation Before Review](#2026-05-11---minimum-validation-before-review)
- [Review Comments](#2026-05-11---review-comments)
- [Review Return Authority](#2026-05-11---review-return-authority)
- [Indexing Ownership](#2026-05-11---indexing-ownership)

### Imports

- [Document Storage And Imports](#2026-05-11---document-storage-and-imports)
- [Assisted Document Import Workflow](#2026-05-11---assisted-document-import-workflow)
- [Import Extraction Ownership](#2026-05-11---import-extraction-ownership)
- [Unsaved Import Extraction Persistence](#2026-05-11---unsaved-import-extraction-persistence)
- [Import Upload Size Limit](#2026-05-11---import-upload-size-limit)
- [.NET Import Extraction Libraries](#2026-05-11---net-import-extraction-libraries)
- [Non-Extractable PDF Handling](#2026-05-11---non-extractable-pdf-handling)

### RAG, Cache, And Feedback

- [Semantic Cache](#2026-05-11---semantic-cache)
- [Semantic Cache Similarity Threshold](#2026-05-11---semantic-cache-similarity-threshold)
- [Cache Invalidation](#2026-05-11---cache-invalidation)
- [Chat Answer Feedback](#2026-05-11---chat-answer-feedback)
- [Chat Feedback Update Behavior](#2026-05-11---chat-feedback-update-behavior)
- [Chat Feedback Review UI](#2026-05-11---chat-feedback-review-ui)
- [RAG Chunk And Audit Table Shape](#2026-05-11---rag-chunk-and-audit-table-shape)

### Audit, Pricing, And Budgets

- [Audit, Pricing, And Logs](#2026-05-11---audit-pricing-and-logs)
- [Per-User AI Usage Budgets](#2026-05-11---per-user-ai-usage-budgets)
- [AI Budget Enforcement Scope](#2026-05-11---ai-budget-enforcement-scope)
- [Over-Budget Cache Behavior](#2026-05-11---over-budget-cache-behavior)

### Data Model And Operations

- [Initial Database Entity Boundaries](#2026-05-11---initial-database-entity-boundaries)
- [Secrets And Configuration](#2026-05-11---secrets-and-configuration)
- [MVP Operational Defaults](#2026-05-11---mvp-operational-defaults)
- [Default OpenAI Models](#2026-05-11---default-openai-models)

### Workflow

- [Human-In-The-Loop Implementation Protocol](#2026-05-13---human-in-the-loop-implementation-protocol)
- [Documentation Marker Scan Scope](#2026-05-13---documentation-marker-scan-scope)

### UI Foundation

- [MVP UI Foundation](#2026-05-11---mvp-ui-foundation)

---

## 2026-05-11 - Base Architecture Scope

**Context:** The product includes document management, RAG chat, instruction viewing, audit, security, and deployment concerns. This is too broad for a single implementation step.

**Options Considered:** Design the full platform at once, focus on document management first, focus on RAG first, or define the complete base architecture first.

**Decision:** Define the complete base architecture first.

**Rationale:** The architecture defines boundaries for identity, permissions, storage, indexing, cache, audit, logging, deployment, and secrets. These choices affect every later module.

**Tradeoffs:** This delays implementation, but prevents contradictory service and data ownership decisions.

**Consequences:** Implementation will start only after the architecture design and written spec are approved.

## 2026-05-11 - Single-Tenant Docker Compose Deployment

**Context:** The platform should be sellable to corporate customers while remaining feasible for an MVP.

**Options Considered:** Single-tenant deployment per customer, multi-tenant SaaS, or hybrid SaaS/dedicated deployments.

**Decision:** Start with single-tenant deployment per customer using Docker Compose.

**Rationale:** Single-tenant deployment reduces tenant-isolation risk and fits corporate customers that prefer dedicated deployments.

**Tradeoffs:** Operating many customer deployments costs more than a shared SaaS platform.

**Consequences:** Each customer has isolated secrets, database, logs, and deployment configuration.

## 2026-05-11 - Monorepo Compose Product

**Context:** The product has three frontends, two backend services, shared deployment, and shared documentation.

**Options Considered:** Monorepo Compose Product, platform-ready split across repos, or simplified demo bundle.

**Decision:** Use a monorepo with `apps/*`, `services/*`, `infra/compose`, `docs`, and `context`.

**Rationale:** A monorepo keeps MVP coordination simple while preserving clear service boundaries.

**Tradeoffs:** It is less independent than separate repositories for future teams.

**Consequences:** Initial implementation plans can coordinate frontends, APIs, database migrations, and Compose without cross-repo release overhead.

## 2026-05-11 - MVP UI Foundation

**Context:** The product has three React frontends that need consistent operational UI patterns without spending the MVP building a custom design system from scratch.

**Options Considered:** Custom CSS/component system, a full enterprise UI kit, or Tailwind CSS with `shadcn/ui` and `lucide-react`.

**Decision:** Use React with strict TypeScript, Tailwind CSS, `shadcn/ui`, and `lucide-react` as the MVP UI foundation for all three frontends.

**Rationale:** This stack supports dense SaaS-style tables, forms, filters, dialogs, badges, tabs, menus, and stateful controls with low design-system overhead.

**Tradeoffs:** `shadcn/ui` is copied component code rather than a traditional installed component package, so the project must maintain local component consistency.

**Consequences:** Management, chat, and docs frontends share UI conventions but keep workflow-specific layouts. Product surfaces should avoid landing-page patterns, decorative gradients, oversized heroes, and marketing-style card layouts.

## 2026-05-11 - Service Boundaries

**Context:** The user specified three independent frontends and separate .NET and FastAPI backends.

**Options Considered:** Make .NET the only public API, expose both .NET and FastAPI publicly, or make FastAPI the primary API.

**Decision:** Expose `.NET API` publicly for management/viewer workflows and expose FastAPI publicly for chat. Keep FastAPI indexing endpoints internal-only.

**Rationale:** The chat frontend can call FastAPI directly while document lifecycle remains owned by .NET.

**Tradeoffs:** FastAPI must implement production-grade auth validation, error handling, logging, and audit for public chat.

**Consequences:** Caddy exposes `api.client.com` and `rag.client.com`; internal indexing is only reachable over the Docker network.

## 2026-05-11 - API Contract Granularity

**Context:** The base architecture spec needs enough API detail to guide implementation across three React frontends, the .NET API, and FastAPI, but exhaustive endpoint DTOs could turn the architecture step into premature implementation design.

**Options Considered:** Define MVP contracts by contract group, define every endpoint request/response field up front, or leave API contracts entirely to implementation planning.

**Decision:** Define MVP API contracts by contract group in the architecture spec.

**Rationale:** Contract groups are specific enough to lock service ownership, route families, authorization, lifecycle side effects, stable errors, and representative DTO shapes without over-designing every field before the entity model and implementation plan are written.

**Tradeoffs:** Some endpoint details remain unresolved until implementation planning, so the plan must include a focused DTO pass before coding each contract group.

**Consequences:** The architecture spec will document contract groups for authentication/session, user/group administration, document lifecycle/import, viewer tokens/document viewing, chat/RAG, feedback/reporting, internal indexing, health, and errors. Implementation details must preserve the approved ownership, authorization, audit, lifecycle, and shared error-envelope rules.

## 2026-05-11 - MVP API Contract Groups

**Context:** After choosing contract-group granularity, the architecture needs a concrete list of API groups so implementation planning can split work cleanly across services and frontends.

**Options Considered:** Keep only service-level boundaries, define a small set of broad contract groups, or define many fine-grained endpoint families now.

**Decision:** Use eight MVP API contract groups: auth/session, users/groups, documents, viewer, chat/RAG, feedback/reporting, internal indexing, and health/errors.

**Rationale:** These groups map directly to product workflows and service ownership without forcing premature endpoint-by-endpoint DTO design.

**Tradeoffs:** Some groups, especially documents and feedback/reporting, are broad and will need decomposition during implementation planning.

**Consequences:** .NET owns auth/session, users/groups, documents, viewer, and management reporting. FastAPI owns chat/RAG, feedback submission, and internal indexing. All services implement health/error contracts. Management reporting reads RAG data through .NET-controlled read-only reporting paths, not direct FastAPI calls from the management frontend.

## 2026-05-11 - Authentication And Roles

**Context:** The system needs local authentication, restricted management access, and secure RAG/document retrieval.

**Options Considered:** Local users managed by .NET, external OIDC provider, or service-specific authentication.

**Decision:** Use local users managed by .NET. Use roles for system capabilities and groups/departments for document access.

**Rationale:** This keeps MVP dependencies low while allowing corporate access models.

**Tradeoffs:** SSO is deferred.

**Consequences:** .NET issues JWTs. FastAPI validates those JWTs. Current explicit roles are `Admin`, `DocumentManager`, and `Viewer`.

## 2026-05-11 - MVP Document Access Rules

**Context:** Document retrieval, viewer authorization, semantic cache reuse, and audit all depend on a stable effective access scope. Per-user document exceptions would complicate UI, authorization checks, and `access_scope_hash` calculation.

**Options Considered:** Group/department and attribute-based access only, group access plus per-user allow/deny exceptions, or role-only access.

**Decision:** Use group/department and document-attribute access rules only in the MVP. Do not support per-user document ACL exceptions.

**Rationale:** Group-based access is easier to explain, audit, cache, and reproduce in customer deployments. Exceptional access can be modeled by creating a dedicated group and assigning the user to it.

**Tradeoffs:** Administrators need to create small groups for exceptional cases instead of directly assigning one user to one document.

**Consequences:** `instruction_permissions` models group/department and attribute rules, not direct per-user ACL rows. `access_scope_hash` is derived from role, groups/departments, and relevant access attributes.

## 2026-05-11 - Browser Session Storage

**Context:** The React frontends need authenticated access to the .NET API and FastAPI without exposing long-lived credentials to browser JavaScript.

**Options Considered:** Store JWTs in `localStorage` or `sessionStorage`, keep access tokens only in JavaScript memory, or use secure cookies with CSRF protection.

**Decision:** Use browser-facing secure cookies for sessions/auth claims. Cookies must be `HttpOnly`, `Secure`, and `SameSite`, and mutating requests must use CSRF protection. Do not store JWTs, refresh tokens, session IDs, or other credential-bearing tokens in `localStorage` or `sessionStorage`.

**Rationale:** Browser storage is accessible to JavaScript running in the origin, so one XSS flaw can expose tokens. `HttpOnly` cookies reduce token theft through JavaScript, while CSRF protection addresses the main risk introduced by cookie-based sessions.

**Tradeoffs:** Cookie sessions require careful CSRF design, CORS/credentials configuration for cross-origin API calls, and explicit cookie topology across the product subdomains.

**Consequences:** The architecture must define how `api.client.com`, `rag.client.com`, and the frontend subdomains receive and validate host-appropriate secure cookies without relying on unsafe broad parent-domain cookies. SameSite is treated as defense in depth, not as the only CSRF mitigation.

**Evidence:** Verified against OWASP guidance on 2026-05-11. OWASP's HTML5 Security Cheat Sheet and Session Management Cheat Sheet warn against storing session identifiers or tokens in web storage because they are accessible to JavaScript. OWASP's CSRF Prevention Cheat Sheet recommends token/header-based CSRF mitigations and treats SameSite as defense in depth.

## 2026-05-11 - Browser API And Cookie Topology

**Context:** The product uses separate subdomains for management, chat, docs, and backend API surfaces. Browser authentication should avoid broad parent-domain cookies while still allowing each frontend to call the right backend.

**Options Considered:** Store a parent-domain cookie for `.client.com`, call `api.client.com` and `rag.client.com` cross-origin from every frontend, or route frontend API calls through same-origin `/api/*` paths in Caddy.

**Decision:** Use Caddy as the browser same-origin API gateway. Frontends call `/api/*` on their own host. Caddy routes `manage.client.com/api/*` and `docs.client.com/api/*` to .NET, routes chat API paths on `chat.client.com` to FastAPI, and routes chat auth/session paths to .NET. Browser session cookies are host-only `__Host-` cookies. Broad parent-domain cookies such as `Domain=.client.com` are out of scope for the MVP.

**Rationale:** Same-origin routing lets the MVP use stronger host-only cookies, reduces CORS and credential-mode complexity, and avoids sharing browser session cookies with every sibling subdomain under the customer domain.

**Tradeoffs:** Caddy routing becomes more important and each frontend host may have its own host-scoped session cookie. Backend direct hosts are less central for browser flows.

**Consequences:** `api.client.com` and `rag.client.com` may remain as controlled technical/API surfaces if needed, but the default browser integration path is same-origin `/api/*`. Implementation must define route precedence carefully so frontend assets and API paths do not conflict.

## 2026-05-11 - FastAPI Chat Token Validation

**Context:** FastAPI must authenticate public chat requests while .NET remains the identity authority. Chat response speed is a product quality attribute, so auth checks should not add avoidable per-question latency.

**Options Considered:** FastAPI validates signed access tokens locally, FastAPI calls .NET to introspect every chat request, or FastAPI trusts Caddy/.NET forwarding without validating a token.

**Decision:** FastAPI validates short-lived signed access tokens issued by .NET locally for normal public chat requests.

**Rationale:** Local validation avoids a synchronous .NET dependency on every chat question while keeping .NET as the authority for issuing claims, signing keys, user state, roles, groups/departments, and `access_scope_hash`.

**Tradeoffs:** Revocation and group changes are not reflected instantly unless an explicit revocation/introspection mechanism is added. The MVP accepts this with short token lifetimes.

**Consequences:** Chat access tokens have an initial 15-minute TTL, configurable per deployment. FastAPI validates signature, issuer, audience, expiration, user identifier, roles, groups/departments, and `access_scope_hash`. Disabling users or changing access groups takes effect no later than token expiration in the MVP.

## 2026-05-11 - Chat Token Refresh

**Context:** FastAPI validates short-lived chat access tokens locally, but users still need a smooth chat session when a token expires.

**Options Considered:** Give FastAPI its own refresh token, introspect/refresh through .NET on every chat request, or renew chat tokens through a .NET route only when needed.

**Decision:** Renew chat access tokens through a same-origin `chat.client.com` route backed by .NET, using the main secure session. Do not add a separate FastAPI refresh token in the MVP.

**Rationale:** This keeps identity/session authority in .NET, avoids exposing refresh credentials to browser JavaScript, and preserves normal chat latency because FastAPI still validates locally during regular chat requests.

**Tradeoffs:** The chat frontend must handle expired-token responses by calling the .NET renewal route and retrying or redirecting to login when the main session has expired.

**Consequences:** A route such as `/api/auth/chat-token` on `chat.client.com` is routed by Caddy to .NET. .NET sets a host-only `HttpOnly`, `Secure`, `SameSite` chat token cookie for `chat.client.com` only after validating the main session. FastAPI owns no refresh-token table or refresh endpoint in the MVP.

## 2026-05-11 - Viewer Access Tokens

**Context:** `docs.client.com` must open documents from management and chat links without relying on unsafe long-lived tokens in URLs.

**Options Considered:** Put the main JWT in links, put the scoped viewer access token directly in the URL, rely on existing browser session only, or use a one-time exchange code that is converted into a secure viewer cookie.

**Decision:** Use one-time exchange codes in viewer URLs and store the real scoped viewer access token only in a host-only secure cookie for `docs.client.com`.

**Rationale:** Main JWTs or real viewer tokens in URLs are unsafe because URLs can leak through logs, browser history, screenshots, bookmarks, and referer headers. A one-time short-lived code limits what appears in the URL and lets `.NET` place the real viewer access token in an `HttpOnly` cookie after `docs.client.com` validates the code.

**Tradeoffs:** This adds exchange-code persistence, single-use validation, and more viewer loading/error states.

**Consequences:** `.NET API` issues document-specific viewer exchange links such as `https://docs.client.com/open?code=...`. Exchange codes expire after 60 seconds by default and can be used once. After exchange, `.NET` sets the scoped viewer access token as a host-only `HttpOnly`, `Secure`, `SameSite` cookie for `docs.client.com`. The viewer must handle expired, already-used, invalid, and unauthorized code states safely.

**Evidence:** Verified against OWASP Session Management guidance on 2026-05-11. OWASP recommends cookies for session ID exchange and warns that URL-based session identifiers can leak through links, logs, browser history, bookmarks, referer headers, and search engines.

## 2026-05-11 - Viewer Access Token TTL

**Context:** Viewer access tokens are used in links from management and chat to `docs.client.com`, so expiration must balance usability with link-leak risk.

**Options Considered:** 10 minutes, 5 minutes, or 15 minutes.

**Decision:** Viewer access tokens expire after 15 minutes.

**Rationale:** Fifteen minutes gives users enough time to open links from chat or management without frequent token renewal friction.

**Tradeoffs:** A leaked viewer link remains usable longer than a 5- or 10-minute token.

**Consequences:** Viewer access tokens remain document-specific, scoped by purpose and allowed status, and must include `expires_at` and `jti`.

## 2026-05-11 - Viewer Access Token Reuse

**Context:** Viewer links may be opened, refreshed, or revisited during a short session. One-time-use tokens would require persistent token state and would make normal browser behavior more fragile.

**Options Considered:** Reusable tokens during the 15-minute TTL, strict one-time-use tokens, or hybrid behavior with reusable management links and one-time-use chat links.

**Decision:** Viewer access tokens are reusable during their 15-minute validity window.

**Rationale:** Reusable short-lived tokens keep the MVP simpler and avoid breaking refresh, back navigation, and quick reopening from chat or management.

**Tradeoffs:** If a scoped link leaks, it can be reused until expiration unless explicitly revoked.

**Consequences:** The `jti` is used for audit and revocation if viewer token records are persisted. One-time-use viewer tokens are not required in the MVP.

## 2026-05-11 - Document Lifecycle And RAG Corpus

**Context:** The platform controls document lifecycle and also supports RAG preview for managers.

**Options Considered:** Index only published documents, include internal previews, or index all states and filter later.

**Decision:** Public chat uses only `Published` documents. Internal preview can use `Draft`, `In Review`, and `Published` for authorized users.

**Rationale:** This prevents non-published content from leaking to normal chat while supporting management validation.

**Tradeoffs:** Preview adds a second corpus/scope path.

**Consequences:** Cache, audit, and retrieval must distinguish `published` and `preview` corpus modes.

## 2026-05-11 - MVP Publishing Authority

**Context:** The MVP role model currently includes `Admin`, `DocumentManager`, and `Viewer`. Publishing authority must be explicit because published documents become visible to chat and normal viewers.

**Options Considered:** Admin publishes directly, add a separate `Reviewer` role, or require a two-step Admin approval flow.

**Decision:** Admin publishes directly from `In Review` to `Published`. `DocumentManager` can create, edit, import, update metadata, and send documents to `In Review`, but cannot publish.

**Rationale:** This keeps the MVP workflow simple while preserving separation between document preparation and final publication authority.

**Tradeoffs:** The workflow has less formal review separation than a dedicated `Reviewer` role.

**Consequences:** A separate `Reviewer` role is deferred. Audit must record the Admin publication action and the previous/new document state.

## 2026-05-11 - Publication Requires Successful Indexing

**Context:** Published documents should be immediately consistent between the viewer and the public RAG corpus. If a document cannot be indexed, it should not be published.

**Options Considered:** Publish and index automatically, publish immediately while indexing later, or block publication until indexing completes.

**Decision:** A publish action starts pre-publication indexing through FastAPI's internal indexing endpoint. The document remains `In Review` while indexing is pending or running. It transitions to `Published` only after FastAPI indexes it successfully. If indexing fails, the document is not published.

**Rationale:** This keeps public viewer and public chat behavior consistent. A user should not see a document as published if the chat cannot retrieve it.

**Tradeoffs:** OpenAI, FastAPI, parser, or vector database failures can block publication. The management UI must make the indexing failure visible and re-triable.

**Consequences:** Publication becomes an asynchronous workflow: publish requested, indexing running, indexing failed, or published. Public RAG and normal viewer access only use successfully indexed `Published` documents.

## 2026-05-11 - Manual Retry For Pre-Publication Indexing Failure

**Context:** Publication is blocked until indexing succeeds, so indexing failures need an explicit recovery path for managers.

**Options Considered:** Manual retry from management, automatic retries plus manual retry, or automatic retries only.

**Decision:** Use manual retry from the management UI for the MVP.

**Rationale:** Manual retry gives the user a clear recovery action without adding retry scheduling complexity before implementation validates the indexing workflow.

**Tradeoffs:** Temporary provider or network failures will not recover automatically until a user retries.

**Consequences:** The management UI must show a safe error summary, preserve the technical error in logs/job details, and expose a `Retry indexing` action for authorized users.

## 2026-05-11 - Simple Formal Document Versioning

**Context:** Corporate instructions need traceability and stable references after publication.

**Options Considered:** No formal versioning with audit only, simple published versions, or full parallel draft/version workflow.

**Decision:** Use simple formal versioning in the MVP. Each successful publication creates an immutable version such as `v1`, `v2`, and so on. The viewer and public RAG use the latest successfully published version.

**Rationale:** This provides corporate-grade traceability without the complexity of parallel drafts and active published versions.

**Tradeoffs:** Editing a published instruction still needs a clear rule for whether edits mutate a working copy or create a new draft version; that is the next lifecycle decision.

**Consequences:** Version records must store the normalized HTML, metadata snapshot, publication timestamp, publisher, and indexing reference used by RAG.

## 2026-05-11 - Editing Published Instructions Creates Draft Version

**Context:** The MVP uses immutable published versions, so editing a published instruction must not mutate the active published content.

**Options Considered:** Create a new draft version, block/lock the published version while editing, or edit directly and overwrite on publication.

**Decision:** Editing an already published instruction creates a new draft version. The latest published version remains active until the new draft completes review, pre-publication indexing, and publication.

**Rationale:** This preserves stable published content while allowing managers to prepare updates safely.

**Tradeoffs:** The data model must support an active published version and a draft next version at the same time.

**Consequences:** Viewer and public RAG continue using the latest published version. Management UI must clearly show draft vs published version state.

## 2026-05-11 - Archive Entire Instruction

**Context:** The platform needs a reversible/non-destructive removal path for obsolete instructions while preserving audit and historical versions.

**Options Considered:** Archive the entire instruction, archive individual versions, or add separate `Archived` and `Deleted` states.

**Decision:** Archive applies to the entire instruction in the MVP.

**Rationale:** Instruction-level archiving is simpler for viewer, RAG, permissions, and management search while preserving historical evidence.

**Tradeoffs:** The MVP cannot archive only a specific published version while keeping another version active.

**Consequences:** Archived instructions are unavailable to public viewer and public chat. Versions and audit history are retained. Archiving deactivates the instruction from the active RAG corpus and invalidates cache entries that cite it.

## 2026-05-11 - Archive Authority

**Context:** Archiving removes an instruction from public viewer and public chat, so the authority model must distinguish draft cleanup from removal of published content.

**Options Considered:** `Admin` archives any instruction while `DocumentManager` archives only `Draft` or `In Review`, both roles archive any instruction, or only `Admin` archives.

**Decision:** `Admin` can archive any instruction, including `Published` instructions. `DocumentManager` can archive only instructions whose current lifecycle state is `Draft` or `In Review` and that do not have an active published version.

**Rationale:** Document managers can clean up unpublished work, while published content removal stays under admin control because it affects normal viewers, chat retrieval, cache invalidation, and audit expectations.

**Tradeoffs:** Document managers cannot remove a published instruction even when they are responsible for its content.

**Consequences:** Archiving any instruction with an active published version requires `Admin`. The role table must describe `DocumentManager` archive permission as limited to eligible draft or in-review instructions.

## 2026-05-11 - Restore Archived Instruction To Draft

**Context:** Archived instructions may need to return to service, but direct re-publication can expose stale content.

**Options Considered:** Restore to `Draft`, restore directly to `Published`, or disallow restore in the MVP.

**Decision:** Restoring an archived instruction moves it to `Draft`.

**Rationale:** Returning through `Draft`, `In Review`, pre-publication indexing, and publication keeps editorial and RAG consistency checks intact.

**Tradeoffs:** Restoration requires extra steps before the instruction is public again.

**Consequences:** Restored instructions remain unavailable to public viewer and public chat until they are reviewed, indexed successfully, and published.

## 2026-05-11 - Restore Authority

**Context:** Restoring an archived instruction only moves it back into the internal editing lifecycle. It does not immediately expose the instruction publicly.

**Options Considered:** Use the same authority model as archiving, allow only `Admin` to restore, or allow both `Admin` and `DocumentManager` to restore any archived instruction to `Draft`.

**Decision:** `Admin` and `DocumentManager` can restore any archived instruction to `Draft`.

**Rationale:** Restoration is lower risk than archiving or publishing because restored content remains internal and must still pass review, pre-publication indexing, and Admin publication before it becomes public again.

**Tradeoffs:** Document managers can bring back archived instructions that were originally published, so the management UI and audit trail must make restoration visible.

**Consequences:** Restoring an archived instruction does not reactivate any previous published version, public viewer access, public chat retrieval, or RAG corpus entry.

## 2026-05-11 - No Separate Soft Delete In MVP

**Context:** The lifecycle already includes `Archived`, which removes instructions from public use while preserving audit and versions.

**Options Considered:** No soft delete, soft delete only for drafts, or soft delete for all instructions.

**Decision:** Do not include a separate soft delete state in the MVP.

**Rationale:** `Archived` is sufficient as the functional removal path and keeps lifecycle behavior simpler.

**Tradeoffs:** Draft cleanup workflows are less granular in the MVP.

**Consequences:** All removals use archive/restore semantics. Hard deletion remains an operational/database maintenance concern, not a product workflow.

## 2026-05-11 - Minimum Validation Before Review

**Context:** Documents should not enter review without enough metadata and valid content for permissions, viewer rendering, and future RAG indexing.

**Options Considered:** Full minimum validation, title/content only, or client-configurable required fields.

**Decision:** Moving a draft to `In Review` requires title, instruction type, allowed groups/departments, audience/user type, non-empty sanitized HTML, and valid sanitized content. Tags are optional.

**Rationale:** Review without access metadata or sanitized content would create preventable publishing and retrieval failures.

**Tradeoffs:** Managers must complete more fields before review.

**Consequences:** The .NET API must enforce these validations server-side, and the management UI must show field-level validation errors.

## 2026-05-11 - Review Comments

**Context:** The review workflow needs enough collaboration context and audit evidence without making every state transition unnecessarily heavy.

**Options Considered:** Optional comments for all transitions, required comments for every review transition, or optional comments when sending to review and required comments when returning or rejecting to draft.

**Decision:** Sending a draft to `In Review` may include an optional internal comment. Returning or rejecting a version from `In Review` back to `Draft` requires an internal comment explaining the reason.

**Rationale:** Optional submission comments keep the happy path lightweight, while mandatory return/rejection comments prevent unexplained rework and create useful audit history.

**Tradeoffs:** The management UI and .NET API need validation and storage for review comments.

**Consequences:** Review comments are stored in the `app` schema and included in management audit. Return/rejection authorization is defined as a separate decision.

## 2026-05-11 - Review Return Authority

**Context:** Once a version is in `In Review`, the system needs a clear authority model for sending it back to `Draft` without weakening publication control.

**Options Considered:** Only `Admin` can return/reject from review, `Admin` and `DocumentManager` can return/reject from review, or only the submitting user can withdraw the review request.

**Decision:** `Admin` and `DocumentManager` can return or reject a version from `In Review` back to `Draft` when they provide the required internal comment.

**Rationale:** This keeps review correction fast for document managers while preserving the stronger rule that only `Admin` can publish.

**Tradeoffs:** More users can move content backward in the lifecycle, so audit evidence must be complete and visible.

**Consequences:** Return/rejection audit events must capture actor, previous state, new state, required comment, timestamp, and request ID. `DocumentManager` still cannot publish.

## 2026-05-11 - Indexing Ownership

**Context:** Document changes must trigger indexing without blocking document operations or breaking schema ownership.

**Options Considered:** .NET writes indexing jobs directly, .NET calls FastAPI internal endpoint, or .NET writes an outbox consumed by FastAPI.

**Decision:** .NET calls an internal FastAPI endpoint, and FastAPI creates `rag.indexing_jobs`.

**Rationale:** This keeps `rag` schema ownership with FastAPI and avoids direct cross-schema writes.

**Tradeoffs:** Requires an internal service token and internal endpoint contract.

**Consequences:** The internal indexing endpoint is not exposed by Caddy.

## 2026-05-11 - Document Storage And Imports

**Context:** The system supports an HTML editor and PDF/DOCX imports.

**Options Considered:** Store HTML and metadata in Postgres, store originals in object storage, store original files in Postgres, or discard originals after extraction.

**Decision:** Store normalized HTML and metadata in Postgres. Do not retain original PDF/DOCX files in the MVP.

**Rationale:** This keeps the MVP storage model simple and transactional.

**Tradeoffs:** Original files cannot be reprocessed if extraction logic improves later.

**Consequences:** Import metadata must record original filename, MIME type, file size, hash, importer, timestamp, and parser result.

## 2026-05-11 - Assisted Document Import Workflow

**Context:** Automatically converting PDF/DOCX files into final publishable instructions can produce poor formatting and incorrect metadata, especially when source documents are inconsistent.

**Options Considered:** Fully automatic document conversion, backend extraction into normalized content, frontend extraction before upload, or assisted extraction into the editor.

**Decision:** Use assisted PDF/DOCX import. The system extracts text from the uploaded file and inserts it into the document editor; the user then decides final formatting, structure, title, instruction type, access attributes, and readiness for review.

**Rationale:** This gives better content quality than trusting automatic conversion while still reducing manual copy/paste work.

**Tradeoffs:** Import is less automated and requires user review before the document is useful.

**Consequences:** Imported files do not become publishable content automatically. The canonical saved document remains the user-edited normalized HTML and metadata. Original files are still not retained in the MVP.

## 2026-05-11 - Import Extraction Ownership

**Context:** Assisted import supports the management workflow by extracting text into the document editor. It does not perform retrieval, embeddings, or RAG indexing.

**Options Considered:** FastAPI internal extraction endpoint, .NET management API extraction, or browser-side extraction.

**Decision:** PDF/DOCX extraction is owned by the .NET management API.

**Rationale:** Importing source documents is part of document management, not RAG. Keeping extraction in .NET preserves the boundary where FastAPI only handles chat, retrieval, embeddings, semantic cache, RAG audit, and indexing of already saved normalized content.

**Tradeoffs:** Advanced extraction libraries and file parsing concerns must be handled in the .NET service instead of Python.

**Consequences:** `manage.client.com` uploads PDF/DOCX files to the .NET API for extraction. The .NET API returns extracted text to the editor and stores import metadata when the user saves the draft. FastAPI does not parse PDF/DOCX imports in the MVP.

## 2026-05-11 - Unsaved Import Extraction Persistence

**Context:** Assisted import can extract text before a document exists or before the user decides to save a draft.

**Options Considered:** Persist nothing until save, persist an `import_attempt` record even without a document, or automatically create a temporary draft.

**Decision:** Do not persist extracted text, import metadata, or extraction result as business data if the user does not save the draft.

**Rationale:** This keeps the import flow lightweight and avoids storing abandoned source content or metadata that has no document lifecycle context.

**Tradeoffs:** The product loses business-level analytics for abandoned imports.

**Consequences:** Import metadata is stored only when the user saves the draft. Unsaved extraction requests may still appear in sanitized technical logs with request ID, status, duration, and safe error code.

## 2026-05-11 - Import Upload Size Limit

**Context:** PDF/DOCX extraction runs in the .NET management API and needs a bounded upload size for predictable MVP latency, memory use, and error handling.

**Options Considered:** 10 MB, 25 MB, or 50 MB per imported file.

**Decision:** Use a 10 MB upload size limit per PDF/DOCX import file.

**Rationale:** Ten megabytes is conservative and should cover typical corporate instruction files while reducing extraction latency and resource risk.

**Tradeoffs:** Some large manuals will need to be split or reduced before import.

**Consequences:** The .NET API must enforce the limit and return the shared error envelope with a stable validation error code for oversized files. The management UI should prevalidate file size before upload where possible.

## 2026-05-11 - .NET Import Extraction Libraries

**Context:** The import flow needs .NET libraries for assisted text extraction from DOCX and PDF files. The goal is usable text prefill, not high-fidelity automatic conversion.

**Options Considered:** `DocumentFormat.OpenXml` plus `PdfPig`, a commercial extraction suite, or DOCX-only support in the MVP.

**Decision:** Use `DocumentFormat.OpenXml` for DOCX extraction and `PdfPig` for PDF extraction.

**Rationale:** The selected libraries fit the .NET management boundary, avoid commercial licensing for the MVP, and are sufficient for extracting text into an editor where the user controls final formatting and metadata.

**Tradeoffs:** PDF extraction quality depends on source PDF structure. Scanned PDFs and complex layouts may require future OCR or a commercial extractor.

**Consequences:** The .NET API owns extraction adapters for both libraries. Package versions must be pinned during implementation and avoid prerelease packages unless explicitly approved. Verified references on 2026-05-11: Microsoft Open XML SDK documentation and NuGet package `DocumentFormat.OpenXml`; PdfPig official documentation and NuGet package `PdfPig`.

## 2026-05-11 - Non-Extractable PDF Handling

**Context:** Some PDFs are scanned images or otherwise contain no extractable text. The MVP import flow is assisted text extraction, not OCR or original file retention.

**Options Considered:** Reject with a clear extraction error, retain the original file as a reference, or add OCR in the MVP.

**Decision:** Reject scanned PDFs and files with no extractable text.

**Rationale:** This keeps the MVP aligned with the no-original-retention decision and avoids adding OCR infrastructure before the core document lifecycle and RAG flow are implemented.

**Tradeoffs:** Users must manually transcribe, use OCR outside the system, or provide a text-based PDF/DOCX.

**Consequences:** The .NET API returns the shared error envelope with stable code `IMPORT_TEXT_NOT_EXTRACTABLE`. The editor content remains unchanged, and OCR is explicitly out of scope for the MVP.

## 2026-05-11 - Semantic Cache

**Context:** The RAG module needs a cache but must not leak answers across access boundaries.

**Options Considered:** Exact cache, semantic cache, no cache, cache full answers, or cache retrieval candidates only.

**Decision:** Use semantic cache with full cached answers and citations, only when the effective access scope matches.

**Rationale:** Semantic cache can reduce repeated LLM cost while preserving access controls through `access_scope_hash`.

**Tradeoffs:** Similarity thresholds and invalidation must be designed carefully.

**Consequences:** Cache entries include corpus, `access_scope_hash`, normalized question embedding, similarity threshold, expiration, and source document IDs.

## 2026-05-11 - Semantic Cache Similarity Threshold

**Context:** Semantic cache can save LLM cost, but an overly loose threshold can return a cached answer for a question that is only superficially similar.

**Options Considered:** Conservative threshold `0.90`, balanced threshold `0.86`, or aggressive threshold `0.80`.

**Decision:** Use `0.90` as the initial semantic cache similarity threshold.

**Rationale:** Corporate instruction answers should favor correctness and access safety over early cost savings. A conservative threshold reduces the chance of semantically wrong cached answers.

**Tradeoffs:** The cache will have fewer hits at first, so LLM usage and cost reduction will be lower until metrics justify tuning.

**Consequences:** The threshold must be configurable, with a default of `0.90`, so it can be adjusted per customer using operational metrics.

## 2026-05-11 - Cache Invalidation

**Context:** Cached answers can become stale when documents change.

**Options Considered:** Invalidate by corpus version, invalidate by source document, use TTL only, or invalidate on creation.

**Decision:** Invalidate cached answers when a source document changes, but do not invalidate existing cache entries when a new document is created.

**Rationale:** Source-document invalidation removes known stale answers without wiping the cache on every new document.

**Tradeoffs:** New documents may not influence cached answers until TTL expiry.

**Consequences:** This limitation must be documented in product behavior and operational notes.

## 2026-05-11 - Audit, Pricing, And Logs

**Context:** The product needs usage analytics, feedback, token/cost calculation, and technical diagnosis.

**Options Considered:** Store audit only in logs, use generic JSON events, or use dedicated audit tables plus logs.

**Decision:** Store functional audit in Postgres and technical logs in daily JSON files. Store model pricing in a versioned DB table and snapshot prices into query audits.

**Rationale:** Tables support dashboards and reports; logs support diagnosis.

**Tradeoffs:** More schema work is required.

**Consequences:** `app.audit_events`, `rag.query_audit_events`, and `rag.model_pricing` are required MVP data structures.

## 2026-05-11 - Per-User AI Usage Budgets

**Context:** The product must give administrators control over AI spend, not only request frequency. The user requested a management page where an administrator can set a monetary budget such as USD 5 per month per user and adjust the limit when needed.

**Options Considered:** Only technical rate limits, global customer-level AI budget, per-user monthly monetary budgets, or group-level budgets.

**Decision:** Add per-user monthly AI usage budgets to the MVP. Budgets are configured by `Admin` users in the management app, initially in USD, and can be adjusted per user. The default monthly budget is USD 5 per user. Budget periods reset by customer calendar month using the deployment's configured timezone.

**Rationale:** Per-user monetary budgets map directly to AI provider cost exposure and are easier for customer administrators to understand than token-only limits.

**Tradeoffs:** Monetary budgets require reliable query-level cost snapshots, model pricing maintenance, currency assumptions, and an enforcement path inside FastAPI before paid AI calls.

**Consequences:** Budget configuration lives in the `app` schema and is managed by .NET. FastAPI enforces budgets before starting new paid chat work using the user's current-period spend from `rag.query_audit_events` for the current customer calendar month. The shared error envelope uses a stable code such as `AI_BUDGET_EXCEEDED` when the budget is reached. Rate limits remain separate from monetary budgets. New users receive the USD 5 monthly default unless an `Admin` configures another limit.

## 2026-05-11 - AI Budget Enforcement Scope

**Context:** Reaching an AI usage budget should control provider cost without unnecessarily blocking non-AI product access.

**Options Considered:** Block the whole account, block chat and document viewing, block only new paid chat/RAG work, or only warn without enforcement.

**Decision:** Budget exhaustion blocks only new chat/RAG work that can generate AI provider cost.

**Rationale:** The budget protects monthly AI spend. It is not an account suspension mechanism and should not prevent users from opening documents they are already authorized to access.

**Tradeoffs:** Users over budget may still consume non-AI platform resources, and cached-answer behavior requires care if cache lookup needs a paid embedding call.

**Consequences:** `AI_BUDGET_EXCEEDED` applies to paid chat/RAG work. It does not block management workflows, the document viewer, or authorized document access. Cached answers may be served after budget exhaustion only if the implementation can prove no paid embedding or LLM call is required; otherwise the conservative behavior is to block before paid work starts.

## 2026-05-11 - Over-Budget Cache Behavior

**Context:** Semantic cache lookup still requires embedding the user's question. That embedding request can generate provider cost, which conflicts with budget enforcement after the user reaches the monthly limit.

**Options Considered:** Allow semantic cache lookup after budget exhaustion, add exact no-cost cache lookup, or block chat/RAG requests after budget exhaustion.

**Decision:** Block semantic cached answers for over-budget users in the MVP.

**Rationale:** The budget is a monetary control. If semantic lookup requires a paid embedding call, it should not run after the budget is exhausted.

**Tradeoffs:** Some answers that could have been served from cache remain unavailable until the budget is increased or the next calendar month starts.

**Consequences:** `AI_BUDGET_EXCEEDED` is returned before semantic cache lookup for over-budget users. Exact no-cost cache lookup can be added later as a separate optimization.

## 2026-05-11 - Initial Database Entity Boundaries

**Context:** The architecture needs a first-pass database entity list and ownership boundary without designing every column before implementation planning.

**Options Considered:** Keep only schema-level ownership, define detailed full table schemas now, or define initial table names and ownership with important modeling decisions.

**Decision:** Define initial entity names and ownership in the architecture context. `.NET` owns the `app` schema; FastAPI owns the `rag` schema.

**Rationale:** Table names and ownership are enough to guide migrations, service boundaries, reporting, and implementation planning while avoiding premature full schema design.

**Tradeoffs:** Column-level schema, indexes, constraints, and DTO mappings remain for implementation planning.

**Consequences:** `.NET` owns users, roles, groups, instructions, versions, permissions, tags, review comments, import metadata, viewer exchange codes, viewer token audit, and management audit. FastAPI owns indexing jobs, document chunks, semantic cache, query audit, audit citations, and model pricing.

## 2026-05-11 - RAG Chunk And Audit Table Shape

**Context:** The initial RAG entity list considered separate chunk and embedding tables plus separate feedback and citation tables. The product only needs one embedding per active chunk and one thumbs feedback value per answer in the MVP.

**Options Considered:** Separate `document_chunks` and `chunk_embeddings`; combine chunk text/metadata and embedding in `document_chunks`; store feedback/citations as standalone product tables; or treat feedback/citations as part of query audit.

**Decision:** Store chunk text/metadata and vector embedding together in `rag.document_chunks`. Store simple thumbs feedback directly on `rag.query_audit_events`. Store citations in `rag.query_audit_citations` as child rows of query audit events.

**Rationale:** One row per indexable chunk is simpler and matches the MVP retrieval model. Feedback and citations are audit facts tied to a generated or cached answer, not independent product workflows in the MVP.

**Tradeoffs:** Combining chunks and embeddings makes future multi-embedding-per-chunk support require a migration. Storing feedback on the audit row assumes one feedback value per answer.

**Consequences:** If future requirements introduce multiple embeddings per chunk, embedding model comparisons, feedback history, or multiple evaluators, the schema can split embeddings or feedback into child tables later. For the MVP, reporting remains straightforward through `query_audit_events` and `query_audit_citations`.

## 2026-05-11 - Chat Answer Feedback

**Context:** The chat experience needs a low-friction way to capture answer quality signals for later review and improvement.

**Options Considered:** Thumbs up/down with optional comment, 1-5 rating with optional comment, or no feedback in the MVP.

**Decision:** Use thumbs up/down plus an optional comment for each chat answer.

**Rationale:** Binary feedback is fast enough for frequent chat use and still gives clear positive/negative quality signal. Optional comments allow users to explain bad answers without forcing extra input on every response.

**Tradeoffs:** Binary feedback is less granular than a 1-5 rating and will need aggregation with comments, citations, and audit metadata to understand quality issues.

**Consequences:** Feedback is tied to the original `rag.query_audit_events` record for the generated or cached answer and submitted to FastAPI by the authenticated chat user. Feedback comments must be sanitized before storage.

## 2026-05-11 - Chat Feedback Update Behavior

**Context:** The MVP stores one feedback value and optional comment on `rag.query_audit_events`, not a separate feedback history table.

**Options Considered:** Reject duplicate feedback, allow same-user updates by overwriting the audit row's feedback fields, or create a feedback history child table now.

**Decision:** Allow the same user to update feedback for the same answer by overwriting the single feedback value/comment and updating `feedback_updated_at`.

**Rationale:** Users can correct a mistaken thumbs selection without introducing feedback history complexity in the MVP.

**Tradeoffs:** The MVP does not retain a history of feedback changes.

**Consequences:** Multi-reviewer feedback, feedback history, or audit trails for feedback edits require a future child table.

## 2026-05-11 - Chat Feedback Review UI

**Context:** Capturing feedback is only useful if administrators and document managers can review negative signals and correlate them with answers, citations, users, and time.

**Options Considered:** Management review view with filters, persist feedback only in audit without MVP UI, or create a task/notification inbox for negative feedback.

**Decision:** Add a management feedback review view for `Admin` and `DocumentManager`, with filters by negative feedback, cited document, user, and date range.

**Rationale:** A filtered review view provides enough operational value without introducing workflow/task management complexity in the MVP.

**Tradeoffs:** It adds reporting UI and read-only reporting API work, but avoids building a heavier notification or triage workflow.

**Consequences:** `manage.client.com` uses the .NET API for this view. The .NET API reads feedback through controlled read-only reporting queries or views over `rag.query_audit_events`; the management frontend does not call FastAPI directly.

## 2026-05-11 - Management Reporting Read Model

**Context:** Management feedback/reporting needs data owned by FastAPI in the `rag` schema, but the management frontend must call .NET rather than FastAPI directly.

**Options Considered:** .NET direct ad hoc queries over RAG tables, FastAPI reporting API called by management, or FastAPI-owned read-only database views consumed by .NET.

**Decision:** FastAPI migrations create read-only reporting views in the `rag` schema, and .NET reads those views using a read-only reporting connection/grant.

**Rationale:** The `rag` schema remains owned by FastAPI, while .NET can serve management reporting without duplicating RAG data or exposing FastAPI to the management frontend.

**Tradeoffs:** Reporting views become part of the RAG schema contract and must be migration-managed carefully.

**Consequences:** .NET must not write to `rag` tables. Reporting performance and indexes are designed in the FastAPI/Alembic migration path.

## 2026-05-11 - Secrets And Configuration

**Context:** The product needs secure per-customer deployment configuration.

**Options Considered:** Compose secrets, `.env` with all values, or external Vault/secret manager.

**Decision:** Use Docker Compose secrets for sensitive values and environment variables for non-sensitive configuration.

**Rationale:** Compose secrets reduce accidental exposure of passwords/API keys compared with env-only secrets.

**Tradeoffs:** Applications must read secrets from files.

**Consequences:** OpenAI API key, DB passwords, JWT signing/private key, and internal service tokens are mounted as secret files.

## 2026-05-11 - MVP Operational Defaults

**Context:** The MVP needs concrete default limits for safety, predictable behavior, and deployment configuration, while still allowing per-customer tuning.

**Options Considered:** Leave operational defaults unspecified, hardcode strict defaults, or define configurable defaults in the architecture.

**Decision:** Define configurable MVP defaults in the architecture context for request lengths, log retention, token TTLs, semantic cache, technical rate limits, customer timezone, and AI usage budgets.

**Rationale:** Explicit defaults prevent ambiguous implementation and make the product easier to deploy, test, and explain to customers.

**Tradeoffs:** Defaults may need tuning after real usage data, especially chat rate limits and budget values.

**Consequences:** The MVP starts with `UTC` customer timezone unless configured, 30-day technical log retention, 4000-character chat questions, 1000-character feedback comments, 2000-character review comments, login/chat/import/viewer-exchange rate limits, 24-hour semantic cache TTL, 0.90 semantic cache threshold, 15-minute chat/viewer tokens, 60-second viewer exchange codes, and USD 5 monthly AI budget per user.

## 2026-05-11 - Default OpenAI Models

**Context:** The RAG service needs explicit default OpenAI models for MVP chat responses and embeddings while preserving configurability per deployment.

**Options Considered:** Use a newer GPT-5.x default with smaller embeddings, use the strongest available chat model plus `text-embedding-3-large`, leave model defaults unspecified, or use `gpt-4.1-mini` plus `text-embedding-3-large`.

**Decision:** Use `gpt-4.1-mini` as the MVP default chat model and `text-embedding-3-large` as the MVP default embedding model with `OPENAI_EMBEDDING_DIMENSIONS=1536`.

**Rationale:** `gpt-4.1-mini` is sufficient for the initial corporate instruction assistant use case and favors response speed. Speed is a quality attribute for the chat experience. `text-embedding-3-large` improves retrieval quality for RAG compared with smaller embeddings.

**Tradeoffs:** The chat model is not the strongest available model family, so answer quality must be evaluated against real instructions. The embedding model costs more and stores larger vectors than `text-embedding-3-small`.

**Consequences:** Model IDs must be configurable through `OPENAI_CHAT_MODEL` and `OPENAI_EMBEDDING_MODEL`, and embedding dimensions must be configurable through `OPENAI_EMBEDDING_DIMENSIONS`. The RAG audit must persist actual model IDs, embedding dimensions, token usage, latency, estimated cost, and the model pricing snapshot used. Revisit the defaults after retrieval and answer-quality evaluations.

**Evidence:** Verified against OpenAI documentation on 2026-05-11. The OpenAI data residency/model support table lists `gpt-4.1-mini-2025-04-14` for `/v1/chat/completions` and `/v1/responses`, and lists `text-embedding-3-large` for `/v1/embeddings`. The embeddings guide states that `text-embedding-3-large` produces 3072-dimensional vectors by default and supports reducing dimensions with the `dimensions` parameter. pgvector documentation states that `vector` supports up to 2000 dimensions, so the MVP uses 1536 dimensions for compatibility with `vector(1536)`.

## 2026-05-13 - Human-In-The-Loop Implementation Protocol

**Context:** The user approved starting implementation from `docs/superpowers/plans/2026-05-11-mvp-implementation-plan.md` and explicitly wants active participation in the process to understand what happens during setup, scaffolding, dependency installation, Docker usage, and verification.

**Options Considered:** Let the agent implement autonomously, ask the user only at blockers, or make user participation an explicit implementation protocol.

**Decision:** Use an explicit human-in-the-loop implementation protocol. Each implementation task is split into `Agent-owned` and `User-owned` steps. User-owned steps include concrete commands, expected results, and required confirmation. Secret creation and real secret values remain user-owned. Agents update `context/progress-tracker.md` after meaningful checkpoints and stop when user command results are needed before the next safe step.

**Rationale:** Active participation gives the user direct operational understanding while preserving a durable, auditable implementation workflow. It also prevents local environment actions and secret handling from being hidden inside autonomous agent work.

**Tradeoffs:** Implementation will move more slowly because some steps wait for user confirmation. The benefit is stronger shared understanding and safer handling of local setup and secrets.

**Consequences:** `AGENTS.md` and `context/ai-workflow-rules.md` now require this protocol during MVP implementation. The implementation plan remains the roadmap, but each task must include a collaboration checkpoint before execution.

## 2026-05-13 - Documentation Marker Scan Scope

**Context:** During Task 0 verification, the unresolved-marker scan matched the implementation plan itself because the plan contains the literal marker-search command.

**Options Considered:** Leave the false positive for later, remove the final verification command from the plan, or exclude the plan file from that specific marker scan.

**Decision:** Exclude `docs/superpowers/plans/2026-05-11-mvp-implementation-plan.md` from the unresolved-marker scan command.

**Rationale:** The plan should keep the verification command visible, but the command must not fail only because it finds its own literal search pattern.

**Tradeoffs:** The marker scan no longer checks the implementation plan file itself. That is acceptable because the plan was already self-reviewed and is now the execution roadmap.

**Consequences:** Task 18 and the final verification command set scan `README.md`, `docs`, and `context` while excluding the active implementation plan file.
