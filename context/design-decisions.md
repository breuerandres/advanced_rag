# Design Decisions

This is a chronological log. For the **current effective rules** see `context/architecture.md`, `context/code-standards.md`, `context/rag-spec.md`, `context/ui-context.md`, and `context/code-patterns.md`. Use this file when you need to know *why* a rule exists or what alternatives were considered.

## Topic Index

Jump to the relevant decision group below. Section names match the `##` headings in the chronological log.

### Architecture And Service Boundaries

- [Base Architecture Scope](#2026-05-11---base-architecture-scope)
- [Single-Tenant Docker Compose Deployment](#2026-05-11---single-tenant-docker-compose-deployment)
- [Monorepo Compose Product](#2026-05-11---monorepo-compose-product)
- [Service Boundaries](#2026-05-11---service-boundaries)
- [.NET API Foundation Shape](#2026-05-13---net-api-foundation-shape)
- [Backend HTTP Organization](#2026-05-14---backend-http-organization)
- [API Contract Granularity](#2026-05-11---api-contract-granularity)
- [MVP API Contract Groups](#2026-05-11---mvp-api-contract-groups)
- [Management Reporting Read Model](#2026-05-11---management-reporting-read-model)
- [Frontend Scaffold Stack Alignment](#2026-05-13---frontend-scaffold-stack-alignment)

### Authentication, Sessions, And Access

- [Authentication And Roles](#2026-05-11---authentication-and-roles)
- [MVP Document Access Rules](#2026-05-11---mvp-document-access-rules)
- [Hierarchical Access Refactor Direction](#2026-06-07---hierarchical-access-refactor-direction)
- [Browser Session Storage](#2026-05-11---browser-session-storage)
- [Browser API And Cookie Topology](#2026-05-11---browser-api-and-cookie-topology)
- [FastAPI Chat Token Validation](#2026-05-11---fastapi-chat-token-validation)
- [Chat Token Refresh](#2026-05-11---chat-token-refresh)
- [Viewer Access Tokens](#2026-05-11---viewer-access-tokens)
- [Viewer Access Token TTL](#2026-05-11---viewer-access-token-ttl)
- [Viewer Access Token Reuse](#2026-05-11---viewer-access-token-reuse)
- [Task 7 Auth Foundation](#2026-05-13---task-7-auth-foundation)
- [Task 8 User Administration And Budget Configuration](#2026-05-14---task-8-user-administration-and-budget-configuration)
- [Role Acceptance Matrix Promotes Limited Viewer Management Access](#2026-05-31---role-acceptance-matrix-promotes-limited-viewer-management-access)
- [Secure Cross-Subdomain Viewer SSO Handoff](#2026-06-03---secure-cross-subdomain-viewer-sso-handoff)

### Document Lifecycle And Versioning

- [Document Lifecycle And RAG Corpus](#2026-05-11---document-lifecycle-and-rag-corpus)
- [MVP Publishing Authority](#2026-05-11---mvp-publishing-authority)
- [Publication Requires Successful Indexing](#2026-05-11---publication-requires-successful-indexing)
- [Manual Retry For Pre-Publication Indexing Failure](#2026-05-11---manual-retry-for-pre-publication-indexing-failure)
- [Simple Formal Document Versioning](#2026-05-11---simple-formal-document-versioning)
- [Editing Published Documents Creates Draft Version](#2026-05-11---editing-published-documents-creates-draft-version)
- [Archive Entire Document](#2026-05-11---archive-entire-document)
- [Archive Authority](#2026-05-11---archive-authority)
- [Restore Archived Document To Draft](#2026-05-11---restore-archived-document-to-draft)
- [Restore Authority](#2026-05-11---restore-authority)
- [No Separate Soft Delete In MVP](#2026-05-11---no-separate-soft-delete-in-mvp)
- [Minimum Validation Before Review](#2026-05-11---minimum-validation-before-review)
- [Review Comments](#2026-05-11---review-comments)
- [Review Return Authority](#2026-05-11---review-return-authority)
- [Indexing Ownership](#2026-05-11---indexing-ownership)
- [Task 9 Document Lifecycle And Assisted Imports](#2026-05-17---task-9-document-lifecycle-and-assisted-imports)
- [Task 10 Internal Indexing Pipeline](#2026-05-17---task-10-internal-indexing-pipeline)
- [Deferred MVP Document Tags](#2026-06-03---deferred-mvp-document-tags)

### Imports

- [Document Storage And Imports](#2026-05-11---document-storage-and-imports)
- [Assisted Document Import Workflow](#2026-05-11---assisted-document-import-workflow)
- [Import Extraction Ownership](#2026-05-11---import-extraction-ownership)
- [Unsaved Import Extraction Persistence](#2026-05-11---unsaved-import-extraction-persistence)
- [Import Upload Size Limit](#2026-05-11---import-upload-size-limit)
- [.NET Import Extraction Libraries](#2026-05-11---net-import-extraction-libraries)
- [Non-Extractable PDF Handling](#2026-05-11---non-extractable-pdf-handling)
- [Assisted Import Moves From Plain Text To Safer Structured HTML](#2026-06-03---assisted-import-moves-from-plain-text-to-safer-structured-html)

### RAG, Cache, And Feedback

- [Semantic Cache](#2026-05-11---semantic-cache)
- [Semantic Cache Similarity Threshold](#2026-05-11---semantic-cache-similarity-threshold)
- [Cache Invalidation](#2026-05-11---cache-invalidation)
- [Generic Internal Services Demo Corpus](#2026-06-03---generic-internal-services-demo-corpus)
- [Chat Answer Feedback](#2026-05-11---chat-answer-feedback)
- [Chat Feedback Update Behavior](#2026-05-11---chat-feedback-update-behavior)
- [Chat Feedback Review UI](#2026-05-11---chat-feedback-review-ui)
- [RAG Chunk And Audit Table Shape](#2026-05-11---rag-chunk-and-audit-table-shape)
- [Cost-First MVP Chat Model](#2026-05-17---cost-first-mvp-chat-model)
- [Cost-First MVP Embedding Model](#2026-05-17---cost-first-mvp-embedding-model)
- [Task 11 Chat RAG Core](#2026-05-17---task-11-chat-rag-core)
- [Task 12 Feedback And Management Reporting](#2026-05-18---task-12-feedback-and-management-reporting)
- [Task 13 Viewer Exchange And Document Viewer](#2026-05-18---task-13-viewer-exchange-and-document-viewer)
- [Task 14 Chat Frontend Workflow](#2026-05-18---task-14-chat-frontend-workflow)
- [Task 15 Management Frontend Workflow](#2026-05-18---task-15-management-frontend-workflow)
- [Task 17 E2E Bootstrap Strategy](#2026-05-18---task-17-e2e-bootstrap-strategy)
- [Task 17 Viewer Reload Behavior](#2026-05-18---task-17-viewer-reload-behavior)
- [Feedback Report Shows Reviewer Name + Email Via A Column-Level Cross-Schema Grant](#2026-06-15--feedback-report-shows-reviewer-name--email-via-a-column-level-cross-schema-grant)

### Audit, Pricing, And Budgets

- [Audit, Pricing, And Logs](#2026-05-11---audit-pricing-and-logs)
- [Per-User AI Usage Budgets](#2026-05-11---per-user-ai-usage-budgets)
- [AI Budget Enforcement Scope](#2026-05-11---ai-budget-enforcement-scope)
- [Over-Budget Cache Behavior](#2026-05-11---over-budget-cache-behavior)
- [Task 11 RAG Enforcement Boundaries](#2026-05-17---task-11-rag-enforcement-boundaries)
- [Task 8 User Administration And Budget Configuration](#2026-05-14---task-8-user-administration-and-budget-configuration)
- [Task 17 Budget Exhaustion E2E Scope](#2026-05-18---task-17-budget-exhaustion-e2e-scope)
- [Text-First RAG Image Indexing](#2026-06-01---text-first-rag-image-indexing)
- [Query-Time Multimodal RAG](#2026-06-01---query-time-multimodal-rag)
- [Multimodal Answers Stream Token-By-Token](#2026-06-14--multimodal-answers-stream-token-by-token)

### Data Model And Operations

- [Initial Database Entity Boundaries](#2026-05-11---initial-database-entity-boundaries)
- [Initial Database Migration Foundation](#2026-05-13---initial-database-migration-foundation)
- [Document Images Will Use MinIO Object Storage](#2026-05-31---document-images-will-use-minio-object-storage)
- [Secrets And Configuration](#2026-05-11---secrets-and-configuration)
- [Compose PostgreSQL Role Password Secrets](#2026-05-13---compose-postgresql-role-password-secrets)
- [Local Startup Script Trusts Docker Caddy CA](#2026-05-22---local-startup-script-trusts-docker-caddy-ca)
- [.NET SDK Selection With global.json](#2026-05-13---net-sdk-selection-with-globaljson)
- [FastAPI Python And Packaging Foundation](#2026-05-13---fastapi-python-and-packaging-foundation)
- [FastAPI Docker Base Image](#2026-05-14---fastapi-docker-base-image)
- [MVP Operational Defaults](#2026-05-11---mvp-operational-defaults)
- [Default OpenAI Models](#2026-05-11---default-openai-models)
- [Task 17 Local Compose Deep Links And Viewer Host](#2026-05-18---task-17-local-compose-deep-links-and-viewer-host)
- [Clean Compose Startup Seeds Default Admin And Pricing](#2026-05-22---clean-compose-startup-seeds-default-admin-and-pricing)
- [Interim Manual Source-Based Service Updates](#2026-06-03---interim-manual-source-based-service-updates)
- [Demo Host systemd Autostart](#2026-06-03---demo-host-systemd-autostart)

### Workflow

- [Human-In-The-Loop Implementation Protocol](#2026-05-13---human-in-the-loop-implementation-protocol)
- [Documentation Marker Scan Scope](#2026-05-13---documentation-marker-scan-scope)
- [Backend Readability And Boundary Data Rules](#2026-05-18---backend-readability-and-boundary-data-rules)
- [Task 17.5 First-Run Setup And UI Stabilization](#2026-05-18---task-175-first-run-setup-and-ui-stabilization)
- [Task 17.5 Bootstrap Concurrency And Base Roles](#2026-05-19---task-175-bootstrap-concurrency-and-base-roles)
- [Task 17.5 UI Polish Skill Adaptation](#2026-05-19---task-175-ui-polish-skill-adaptation)
- [Task 17.5 Management Information Architecture](#2026-05-20---task-175-management-information-architecture)
- [Task 17.5 Management Workspace Refinement And TipTap](#2026-05-20---task-175-management-workspace-refinement-and-tiptap)
- [Task 17.5 Feedback And Functional Audit Separation](#2026-05-20---task-175-feedback-and-functional-audit-separation)
- [Task 17.5 Functional Audit Read Model](#2026-05-20---task-175-functional-audit-read-model)
- [Task 17.5 Management Usability Refinement](#2026-05-20---task-175-management-usability-refinement)
- [Task 17.5 Product Surface Session And UI Consolidation](#2026-05-22---task-175-product-surface-session-and-ui-consolidation)
- [Document Domain Vocabulary Rename](#2026-05-20---document-domain-vocabulary-rename)

### UI Foundation

- [MVP UI Foundation](#2026-05-11---mvp-ui-foundation)
- [Chat UI Simplifies To Left Drawer Layout](#2026-06-05---chat-ui-simplifies-to-left-drawer-layout)
- [Management Report Exports Use Client-Side XLSX](#2026-06-13---management-report-exports-use-client-side-xlsx-exceljs)

---

## 2026-05-11 - Base Architecture Scope

**Context:** The product includes document management, RAG chat, document viewing, audit, security, and deployment concerns. This is too broad for a single implementation step.

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

**Consequences:** `document_permissions` models group/department and attribute rules, not direct per-user ACL rows. `access_scope_hash` is derived from role, groups/departments, and relevant access attributes.

## 2026-06-07 - Hierarchical Access Refactor Direction

**Context:** Flat access groups are becoming a poor fit for larger companies because they force administrators to manually model every departmental subtree and management level as separate groups. Document retrieval, viewer authorization, semantic cache reuse, and audit still require a stable effective access scope.

**Options Considered:** Keep flat groups only, replace groups with a pure organizational tree, or use a hybrid model with hierarchical organizational units plus transverse groups.

**Decision:** Continue brainstorming and specification around the hybrid model. Organizational units will model the company tree and use closure-table storage for hierarchical permission joins. Each user will have one primary organizational-unit assignment and zero or more group assignments. `Admin` is a global unrestricted system role with full management, publishing, chat retrieval, and document-viewing authority across all organizational units and groups. Non-admin document management should use explicit scoped permissions instead of the current all-purpose `DocumentManager` role: `DocumentEditor` can create, import, edit, update metadata, send to review, and return/reject draft or in-review documents within scope; `DocumentPublisher` includes editor capabilities and can publish, archive, and restore documents within scope. The normal management scope is the user's assigned organizational unit plus descendants. Hierarchy-based read access is branch-scoped: documents published at an ancestor organizational unit are visible to descendant users, and users assigned to a parent organizational unit can read documents assigned to descendant units; sibling branches do not match. Documents intended for all company users should be assigned to the root organizational unit, not to an empty rule. Root organizational-unit rules match every user's organizational-unit dimension; normal non-root organizational-unit rules require the document unit and user unit to be in the same ancestor/descendant branch. Groups remain available for department-like or cross-cutting access that does not fit a strict tree. Groups do not grant document publication authority by themselves. Each transverse group must have an ownership scope, either global or tied to one organizational-unit node. Non-admin publishers may attach a group to a document rule only when the group owner's organizational unit is inside the publisher's management scope, or when an explicit publisher-to-group usage grant allows that publisher to use the group. Global groups require `Admin` or an explicit usage grant; they are not automatically available to every non-admin publisher. Non-admin publishers must also have scoped authority over the document's organizational-unit rule. Document access rules will use AND semantics within a rule and OR semantics between rules: a user can access a document when at least one rule matches, and every dimension configured on that matching rule is satisfied. When a rule includes multiple groups, the group dimension matches if the user belongs to any listed group. A rule with no organizational unit and no group is invalid and must not grant access implicitly. User moves and group changes must change effective chat and document access on the next request. The first hierarchy-management slice should allow creating, renaming, and deactivating organizational-unit nodes, but should defer moving branches until a later slice with impact preview and stronger audit. For the current development/demo dataset, no migration from existing flat groups and document permissions is required; implementation may reset data and seed a fresh hierarchy after an explicit user-owned destructive-data checkpoint. The approved fresh seed hierarchy is `Empresa` as root, with child units `Comunicación`, `Operaciones`, `Recursos Humanos`, `Sistemas`, and `Finanzas`; `Comunicación` has child units `Marketing` and `Producción Audiovisual`. Approved seed transverse groups and ownership scopes are `Gerentes` as global with publishing use only through explicit grants, `Comité de crisis` owned by `Comunicación`, `Liderazgo` as global and admin-only by default, and `Demo viewers` as a global technical group if still needed for tests. Approved seed users are `admin@admin.com` as unrestricted `Admin` at `Empresa` with `Liderazgo`, `manager.comunicacion@demo.com` as a non-admin `DocumentPublisher` scoped to `Comunicación` with `Gerentes` and `Comité de crisis`, `viewer.marketing@demo.com` as `Viewer` at `Marketing`, `viewer.audiovisual@demo.com` as `Viewer` at `Producción Audiovisual`, `viewer.sistemas@demo.com` as `Viewer` at `Sistemas`, and `crisis.comunicacion@demo.com` as `Viewer` at `Marketing` with `Comité de crisis`. Approved seed documents are `Manual general de comunicación interna` with rule `Empresa`, `Guía del área Comunicación` with rule `Comunicación`, `Calendario de campañas de Marketing` with rule `Marketing`, `Checklist de producción audiovisual` with rule `Producción Audiovisual`, `Procedimiento de guardias de Sistemas` with rule `Sistemas`, and `Protocolo de comunicación en crisis` with rule `Comunicación` plus group `Comité de crisis`.

**Acceptance Matrix:** `admin@admin.com` sees and manages everything. `manager.comunicacion@demo.com` sees company-wide, `Comunicación`, `Marketing`, `Producción Audiovisual`, and crisis documents, and can manage/publish only within the `Comunicación` branch while using `Comité de crisis`; it cannot publish `Empresa`, `Sistemas`, `Finanzas`, or `Liderazgo` documents, and cannot use `Gerentes` for publishing without an explicit grant. `viewer.marketing@demo.com` sees company-wide, `Comunicación`, and `Marketing` documents only. `viewer.audiovisual@demo.com` sees company-wide, `Comunicación`, and `Producción Audiovisual` documents only. `viewer.sistemas@demo.com` sees company-wide and `Sistemas` documents only. `crisis.comunicacion@demo.com` sees company-wide, `Comunicación`, `Marketing`, and crisis documents only. Group membership alone must not grant publication authority, empty document-access rules are rejected, and user node/group changes affect chat/docs authorization on the next request.

**Rationale:** A pure tree is easy to explain but too rigid for real corporate exceptions such as committees, temporary projects, leadership audiences, or company-wide policies. Keeping groups as a second access dimension preserves flexibility while the hierarchy handles the large-company default case.

**Tradeoffs:** The hybrid model is more complex than flat groups. It requires new schema, migration rules, updated management UI, updated session validation claims, revised RAG permission SQL, a bumped `access_scope_hash` schema version, cache safety controls, freshness handling for FastAPI session-claim caching, and end-to-end acceptance coverage. Closure-table storage adds extra rows and write maintenance, but it keeps descendant access joins explicit and efficient for viewer authorization and RAG retrieval.

**Consequences:** The current implementation remains group/department-based until the refactor is specified and implemented. The design must explicitly define cache invalidation and acceptance examples. Multiple organizational-unit assignments are intentionally out of the first implementation slice; cross-cutting access should use groups. Empty rules are rejected for safety and auditability. Branch moves are a later workflow, not part of the first slice. Data reset is an implementation checkpoint, not something to perform during brainstorming.

**Evidence:** External product research on 2026-06-07 found similar patterns in Google Workspace organizational units plus access groups, Google Cloud IAM resource hierarchy, GitLab group/subgroup inheritance, Microsoft Entra administrative units, SharePoint permission inheritance, AWS Organizations OUs/SCPs, and NIST role hierarchy guidance. Task 1 implementation started on 2026-06-08 with an append-only EF migration for the hierarchy baseline. The migration keeps the legacy `document_permissions.group_id` column temporarily while adding `document_permission_groups`, so existing code paths can compile until later slices replace flat group DTOs and centralize access-rule policy. Verification is currently limited by Docker/Testcontainers availability.

## 2026-06-08 - Hierarchical Effective Access Scope Contract

**Context:** The hierarchical access refactor needs a stable scope contract before document authorization and RAG branch-aware filtering can be implemented. FastAPI previously reused `.NET` session claims for a short cache window, which could leave chat/docs access stale after user role, group, organizational-unit, or active-status changes.

**Options Considered:** Keep V1 `role + groups + attributes` hash and add invalidation hooks, keep FastAPI's per-cookie session-claim cache and rely on a version claim, or move to a V2 hash with explicit hierarchical inputs and no FastAPI access-claim caching.

**Decision:** Use `AccessScopeHash` schema `v = 2` with canonical JSON sorted by key and compact UTF-8 SHA-256 serialization. Inputs are primary role, `isGlobalAdmin`, organizational-unit id, sorted group ids, and `accessScopeVersion`; `corpus` remains outside the hash and is matched as a separate cache key dimension. `.NET` owns V2 hash computation and returns `userId`, `role`, `isGlobalAdmin`, `organizationalUnitId`, sorted `groups`, `accessScopeVersion`, `accessScopeHash`, and `corpus` from internal session validation. FastAPI parses the new claim shape and fetches `.NET` validation on every request that uses `DotnetSessionValidator` instead of caching access-relevant claims.

**Rationale:** The hash must change when any access-relevant dimension changes, and freshness must be immediate enough for user/group/node changes to affect chat/docs on the next request. Computing the hash in `.NET` keeps one authority for the app-schema scope, while FastAPI still preserves the hash for semantic-cache and audit partitioning.

**Tradeoffs:** Removing FastAPI's short session-claim cache adds one internal `.NET` validation call per chat/docs API request. That is acceptable for correctness in this refactor; future optimization would need an explicit freshness mechanism that does not reintroduce stale access scopes.

**Consequences:** Existing V1 semantic-cache entries cannot match V2 scopes because the hash input schema and digest differ. Role/group/active-status changes increment `users.access_scope_version`; organizational-unit assignment changes must do the same when the user-administration API slice adds that mutation. RAG branch-aware filtering still needs the later SQL slice before hierarchical document visibility is complete end to end.

**Evidence:** Implemented and verified in Task 2 on 2026-06-08 with `AccessScopeHashTests`, `AuthEndpointTests`, FastAPI `test_session_validation.py`, and the Task 2 verification commands.

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

**Context:** Corporate documents need traceability and stable references after publication.

**Options Considered:** No formal versioning with audit only, simple published versions, or full parallel draft/version workflow.

**Decision:** Use simple formal versioning in the MVP. Each successful publication creates an immutable version such as `v1`, `v2`, and so on. The viewer and public RAG use the latest successfully published version.

**Rationale:** This provides corporate-grade traceability without the complexity of parallel drafts and active published versions.

**Tradeoffs:** Editing a published document still needs a clear rule for whether edits mutate a working copy or create a new draft version; that is the next lifecycle decision.

**Consequences:** Version records must store the normalized HTML, metadata snapshot, publication timestamp, publisher, and indexing reference used by RAG.

## 2026-05-11 - Editing Published Documents Creates Draft Version

**Context:** The MVP uses immutable published versions, so editing a published document must not mutate the active published content.

**Options Considered:** Create a new draft version, block/lock the published version while editing, or edit directly and overwrite on publication.

**Decision:** Editing an already published document creates a new draft version. The latest published version remains active until the new draft completes review, pre-publication indexing, and publication.

**Rationale:** This preserves stable published content while allowing managers to prepare updates safely.

**Tradeoffs:** The data model must support an active published version and a draft next version at the same time.

**Consequences:** Viewer and public RAG continue using the latest published version. Management UI must clearly show draft vs published version state.

## 2026-05-11 - Archive Entire Document

**Context:** The platform needs a reversible/non-destructive removal path for obsolete documents while preserving audit and historical versions.

**Options Considered:** Archive the entire document, archive individual versions, or add separate `Archived` and `Deleted` states.

**Decision:** Archive applies to the entire document in the MVP.

**Rationale:** Document-level archiving is simpler for viewer, RAG, permissions, and management search while preserving historical evidence.

**Tradeoffs:** The MVP cannot archive only a specific published version while keeping another version active.

**Consequences:** Archived documents are unavailable to public viewer and public chat. Versions and audit history are retained. Archiving deactivates the document from the active RAG corpus and invalidates cache entries that cite it.

## 2026-05-11 - Archive Authority

**Context:** Archiving removes an document from public viewer and public chat, so the authority model must distinguish draft cleanup from removal of published content.

**Options Considered:** `Admin` archives any document while `DocumentManager` archives only `Draft` or `In Review`, both roles archive any document, or only `Admin` archives.

**Decision:** `Admin` can archive any document, including `Published` documents. `DocumentManager` can archive only documents whose current lifecycle state is `Draft` or `In Review` and that do not have an active published version.

**Rationale:** Document managers can clean up unpublished work, while published content removal stays under admin control because it affects normal viewers, chat retrieval, cache invalidation, and audit expectations.

**Tradeoffs:** Document managers cannot remove a published document even when they are responsible for its content.

**Consequences:** Archiving any document with an active published version requires `Admin`. The role table must describe `DocumentManager` archive permission as limited to eligible draft or in-review documents.

## 2026-05-11 - Restore Archived Document To Draft

**Context:** Archived documents may need to return to service, but direct re-publication can expose stale content.

**Options Considered:** Restore to `Draft`, restore directly to `Published`, or disallow restore in the MVP.

**Decision:** Restoring an archived document moves it to `Draft`.

**Rationale:** Returning through `Draft`, `In Review`, pre-publication indexing, and publication keeps editorial and RAG consistency checks intact.

**Tradeoffs:** Restoration requires extra steps before the document is public again.

**Consequences:** Restored documents remain unavailable to public viewer and public chat until they are reviewed, indexed successfully, and published.

## 2026-05-11 - Restore Authority

**Context:** Restoring an archived document only moves it back into the internal editing lifecycle. It does not immediately expose the document publicly.

**Options Considered:** Use the same authority model as archiving, allow only `Admin` to restore, or allow both `Admin` and `DocumentManager` to restore any archived document to `Draft`.

**Decision:** `Admin` and `DocumentManager` can restore any archived document to `Draft`.

**Rationale:** Restoration is lower risk than archiving or publishing because restored content remains internal and must still pass review, pre-publication indexing, and Admin publication before it becomes public again.

**Tradeoffs:** Document managers can bring back archived documents that were originally published, so the management UI and audit trail must make restoration visible.

**Consequences:** Restoring an archived document does not reactivate any previous published version, public viewer access, public chat retrieval, or RAG corpus entry.

## 2026-05-11 - No Separate Soft Delete In MVP

**Context:** The lifecycle already includes `Archived`, which removes documents from public use while preserving audit and versions.

**Options Considered:** No soft delete, soft delete only for drafts, or soft delete for all documents.

**Decision:** Do not include a separate soft delete state in the MVP.

**Rationale:** `Archived` is sufficient as the functional removal path and keeps lifecycle behavior simpler.

**Tradeoffs:** Draft cleanup workflows are less granular in the MVP.

**Consequences:** All removals use archive/restore semantics. Hard deletion remains an operational/database maintenance concern, not a product workflow.

## 2026-05-11 - Minimum Validation Before Review

**Context:** Documents should not enter review without enough metadata and valid content for permissions, viewer rendering, and future RAG indexing.

**Options Considered:** Full minimum validation, title/content only, or client-configurable required fields.

**Decision:** Moving a draft to `In Review` requires title, document type, allowed groups/departments, audience/user type, non-empty sanitized HTML, and valid sanitized content. Tags are optional.

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

**Context:** Automatically converting PDF/DOCX files into final publishable documents can produce poor formatting and incorrect metadata, especially when source documents are inconsistent.

**Options Considered:** Fully automatic document conversion, backend extraction into normalized content, frontend extraction before upload, or assisted extraction into the editor.

**Decision:** Use assisted PDF/DOCX import. The system extracts text from the uploaded file and inserts it into the document editor; the user then decides final formatting, structure, title, document type, access attributes, and readiness for review.

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

**Rationale:** Ten megabytes is conservative and should cover typical corporate document files while reducing extraction latency and resource risk.

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

**Rationale:** Corporate document answers should favor correctness and access safety over early cost savings. A conservative threshold reduces the chance of semantically wrong cached answers.

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

**Consequences:** `.NET` owns users, roles, groups, documents, versions, permissions, tags, review comments, import metadata, viewer exchange codes, viewer token audit, and management audit. FastAPI owns indexing jobs, document chunks, semantic cache, query audit, audit citations, and model pricing.

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

**Rationale:** `gpt-4.1-mini` is sufficient for the initial corporate document assistant use case and favors response speed. Speed is a quality attribute for the chat experience. `text-embedding-3-large` improves retrieval quality for RAG compared with smaller embeddings.

**Tradeoffs:** The chat model is not the strongest available model family, so answer quality must be evaluated against real documents. The embedding model costs more and stores larger vectors than `text-embedding-3-small`.

**Consequences:** Model IDs must be configurable through `OPENAI_CHAT_MODEL` and `OPENAI_EMBEDDING_MODEL`, and embedding dimensions must be configurable through `OPENAI_EMBEDDING_DIMENSIONS`. The RAG audit must persist actual model IDs, embedding dimensions, token usage, latency, estimated cost, and the model pricing snapshot used. Revisit the defaults after retrieval and answer-quality evaluations.

**Evidence:** Verified against OpenAI documentation on 2026-05-11. The OpenAI data residency/model support table lists `gpt-4.1-mini-2025-04-14` for `/v1/chat/completions` and `/v1/responses`, and lists `text-embedding-3-large` for `/v1/embeddings`. The embeddings guide states that `text-embedding-3-large` produces 3072-dimensional vectors by default and supports reducing dimensions with the `dimensions` parameter. pgvector documentation states that `vector` supports up to 2000 dimensions, so the MVP uses 1536 dimensions for compatibility with `vector(1536)`.

## 2026-05-17 - Cost-First MVP Chat Model

**Context:** During Task 10/Task 11 RAG refinement, the user identified that model choice, API key handling, pricing, and RAG quality/cost tradeoffs were not reviewed with enough detail before paid provider calls became possible.

**Options Considered:** Keep `gpt-4.1-mini`, move to a GPT-5.x model through the Responses API, or lower the MVP chat default to `gpt-4.1-nano` until the MVP is running end to end.

**Decision:** Use `gpt-4.1-nano` as the MVP default chat model and keep the current Chat Completions API path for the MVP.

**Rationale:** The immediate goal is to get the MVP working end to end with controlled cost. `gpt-4.1-nano` is cheaper than the previous default and lets the team gather real latency, cost, citation, and feedback evidence before improving response quality. Keeping Chat Completions avoids coupling a model downgrade with an API migration.

**Tradeoffs:** Answer quality may be weaker than `gpt-4.1-mini` or newer GPT-5.x models. That is acceptable for the MVP only if RAG audit, citation quality, user feedback, and cost metrics are captured and used to evaluate a later upgrade.

**Consequences:** `OPENAI_CHAT_MODEL` defaults should move to `gpt-4.1-nano` in the implementation and documentation. Task 11 must keep model IDs configurable and persist the actual model used per query. A post-MVP evaluation should compare answer quality, citation accuracy, latency, and cost before changing the default upward.

**Evidence:** Checked OpenAI developer documentation on 2026-05-17. The current OpenAI guidance identifies GPT-5.5 as the latest model and recommends the Responses API for new projects, while the model support listing still includes GPT-4.1 family models for Chat Completions. The project intentionally chooses a lower-cost MVP default and defers the Responses API migration until after MVP validation.

## 2026-05-17 - Cost-First MVP Embedding Model

**Context:** The previous RAG default used `text-embedding-3-large` with `dimensions=1536`. The user correctly pointed out that `text-embedding-3-large` is natively 3072 dimensions, while 1536 is the native size for `text-embedding-3-small`.

**Options Considered:** Keep `text-embedding-3-large` reduced to 1536 dimensions, use native `text-embedding-3-large` at 3072 dimensions, or switch the MVP default to `text-embedding-3-small` at native 1536 dimensions.

**Decision:** Use `text-embedding-3-small` with `OPENAI_EMBEDDING_DIMENSIONS=1536` as the MVP default embedding model.

**Rationale:** The MVP goal is to control provider cost until the end-to-end product is running. `text-embedding-3-small` aligns with the existing `vector(1536)` schema without a migration and avoids the confusing large-model-with-reduced-dimensions default.

**Tradeoffs:** Retrieval quality may be lower than `text-embedding-3-large`, especially on nuanced or long internal documents. This is acceptable for the MVP only if retrieval quality, citation relevance, user feedback, and answer quality are reviewed before production rollout.

**Consequences:** `OPENAI_EMBEDDING_MODEL` defaults should move to `text-embedding-3-small`, while `OPENAI_EMBEDDING_DIMENSIONS` remains `1536`. The existing `rag.document_chunks.embedding vector(1536)` and `rag.semantic_cache_entries.question_embedding vector(1536)` schema remains valid. A future upgrade to `text-embedding-3-large` at 3072 dimensions requires an explicit data/model migration plan and reindexing.

**Evidence:** Checked OpenAI developer documentation on 2026-05-17. The embeddings guide states that `text-embedding-3-small` defaults to 1536 dimensions and `text-embedding-3-large` defaults to 3072 dimensions, and that text-embedding-3 models support the `dimensions` parameter for shortening embeddings.

## 2026-05-17 - Task 11 RAG Enforcement Boundaries

**Context:** Before implementing Task 11, three RAG details remained ambiguous: how FastAPI reads `.NET`-owned AI budget configuration, what happens when `rag.model_pricing` lacks pricing for the configured models, and whether retrieval authorization should trust token claims, `access_scope_hash`, or database permissions.

**Options Considered:** Put budget values in the chat token, call an internal `.NET` budget endpoint on every chat request, or let FastAPI read budget configuration through read-only database grants. Allow missing pricing with zero-cost estimates, warn and continue, or fail readiness/runtime safely. Authorize retrieval from token-provided document ids, `access_scope_hash`, or SQL filtering against `.NET`-owned permissions.

**Decision:** FastAPI reads `app.user_ai_budget_limits` through explicit read-only database grants and combines it with `rag.query_audit_events` spend. `rag.model_pricing` active rows are mandatory for the configured chat and embedding models; readiness fails when pricing is missing and runtime chat returns `RAG_PROVIDER_MISCONFIGURED`. Retrieval uses signed chat-token scope claims as inputs, but filters in SQL against `app.document_permissions` through read-only grants. `access_scope_hash` is only for cache partitioning and audit, not authorization.

**Rationale:** Budgets can change during a chat-token lifetime, so putting the budget only in JWT claims would make enforcement stale. An internal `.NET` call on every chat request would add latency and a synchronous service dependency to the chat path. Read-only database access preserves `.NET` write ownership while keeping budget checks local to FastAPI. Missing pricing makes budget enforcement unreliable, so failing explicitly is safer than writing false zero-cost audit rows. Retrieval authorization must use current document permissions and cannot rely on a hash alone.

**Tradeoffs:** FastAPI now has approved read-only access to two `.NET`-owned app tables, which is a controlled exception to strict schema ownership. This must stay narrow and tested. Read-only SQL filtering is more complex than token-only filtering, but avoids oversized or stale document-id claims and keeps cache partitioning separate from authorization.

**Consequences:** Task 11 must add migration/init grants for `app.user_ai_budget_limits` and `app.document_permissions`, pricing seed/readiness checks, and retrieval tests proving that different permission scopes cannot retrieve unauthorized chunks or reuse each other's cache. FastAPI must not write to any `app` table.

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

## 2026-05-13 - Compose PostgreSQL Role Password Secrets

**Context:** Task 1 turns the architecture's database initialization model into concrete Compose files. The architecture requires separate `app`, `rag`, and reporting ownership boundaries, while services connect to Postgres over the Docker network and should not receive database passwords as plain environment variable values.

**Options Considered:** Use one shared Postgres password for all service roles, rely on passwordless/peer-style local access, or create separate Compose secret files for the admin, app, RAG, and reporting database roles.

**Decision:** Use separate Compose secret files for `postgres_admin_password`, `postgres_app_password`, `postgres_rag_password`, and `postgres_reporting_password`. The short-lived `postgres-init` service reads these files to create or update the role passwords, and runtime services read only their own password file.

**Rationale:** Separate role secrets preserve the schema ownership boundary and make future rotation/audit clearer. File-mounted Compose secrets reduce accidental exposure compared with putting passwords directly in environment variables.

**Tradeoffs:** Local setup requires more secret files before Compose validation or stack startup. The extra setup is acceptable because the project now has explicit user-owned secret preparation steps.

**Consequences:** `infra/compose/secrets/README.md` lists all required local secret files. `.NET` receives the app and reporting password file paths, FastAPI receives the RAG password file path, and Postgres receives the admin password file path.

## 2026-05-13 - .NET SDK Selection With global.json

**Context:** Task 2 targets .NET 8, but the development machine has both .NET SDK 8.0.421 and 9.0.311 installed. Without an SDK selector, the .NET CLI can use the latest installed SDK, which may scaffold or build with SDK 9 defaults.

**Options Considered:** Use the latest installed SDK, pass SDK/version flags manually on every command, or add a repository-level `global.json`.

**Decision:** Add repository-root `global.json` selecting SDK `8.0.421` with `rollForward` set to `latestFeature`.

**Rationale:** The MVP stack is explicitly .NET 8. A root `global.json` makes CLI behavior reproducible for scaffolding, local builds, and CI while still allowing newer compatible 8.0 feature bands/patches.

**Tradeoffs:** Developers must install a .NET 8 SDK locally even if they already have SDK 9 or newer. This is acceptable because .NET 8 is the approved target stack.

**Consequences:** `dotnet --version` from the repository root should resolve to an 8.0 SDK. Moving to a newer major SDK requires an explicit stack decision update.

**Evidence:** Verified against Microsoft documentation on 2026-05-13. Microsoft documents that `global.json` selects the .NET SDK used by CLI commands and that the `latestFeature` roll-forward policy stays within the requested major/minor SDK line while using a compatible later feature band or patch.

## 2026-05-13 - .NET API Foundation Shape

**Context:** Task 2 turns the approved .NET management API boundary into a concrete repository structure before EF Core, authentication, document workflows, or reporting are added.

**Options Considered:** Keep a single ASP.NET Core project, split only API and tests, or create the planned API/App/Domain/Infrastructure layers from the beginning.

**Decision:** Scaffold the .NET service as a six-project solution: `AdvancedRag.Api`, `AdvancedRag.App`, `AdvancedRag.Domain`, `AdvancedRag.Infrastructure`, `AdvancedRag.Api.Tests`, and `AdvancedRag.App.Tests`. The API host exposes minimal `/health/live` and `/health/ready` endpoints. Readiness returns `ok` until database and secret checks are introduced in the operational hardening task. The service image uses a multi-stage .NET 8 Dockerfile and a service-local `.dockerignore` that excludes generated `bin/obj` output from the Linux container build context.

**Rationale:** Starting with the layer split keeps future auth, EF Core, lifecycle, reporting, and integration work from collapsing into the API host. The minimal health endpoints give Compose and tests an early stable contract. The `.dockerignore` is required because copying local Windows restore/build artifacts into a Linux Docker build can break publish with host-specific NuGet paths.

**Tradeoffs:** The foundation has more projects than the current behavior strictly needs. This is acceptable because later tasks depend on clean boundaries and testable application services.

**Consequences:** Future .NET work should keep controllers/endpoints thin, place use cases in `AdvancedRag.App`, domain rules in `AdvancedRag.Domain`, and EF/infrastructure concerns in `AdvancedRag.Infrastructure`. Docker builds for `services/dotnet-api` must not depend on host-local `bin/obj` artifacts.

**Evidence:** Verified on 2026-05-13 with `dotnet test services/dotnet-api/AdvancedRag.sln`, `dotnet build services/dotnet-api/AdvancedRag.sln`, and `docker build -f services/dotnet-api/Dockerfile services/dotnet-api`.

## 2026-05-13 - FastAPI Python And Packaging Foundation

**Context:** Task 3 scaffolds the FastAPI RAG service. The user's local `python --version` is `3.12.5`, but the first `uv init`/`uv add` pass selected an installed CPython 3.13 interpreter and generated `requires-python = ">=3.13"`, which would make the service diverge from the confirmed local toolchain.

**Options Considered:** Accept Python 3.13, leave the interpreter unconstrained, or pin the RAG service to Python 3.12 for the MVP foundation.

**Decision:** Pin `services/rag-api` to Python 3.12 using `.python-version` and `requires-python = ">=3.12,<3.13"`. Keep dependencies exact in `pyproject.toml` and locked by `uv.lock`. Configure the uv build backend with `module-name = "advanced_rag"` because the package distribution name is `advanced-rag-rag-api` while the import module is `advanced_rag`.

**Rationale:** Python 3.12 matches the user-confirmed workstation interpreter, is a conservative target for FastAPI dependencies, and keeps local and Docker behavior aligned. The explicit uv build-backend module name prevents package builds from looking for the default normalized module name `advanced_rag_rag_api`.

**Tradeoffs:** Developers with only Python 3.13 installed must install a Python 3.12 interpreter for this service. This is acceptable for MVP reproducibility.

**Consequences:** FastAPI commands should be run from `services/rag-api` through `uv`, and Docker builds should use a Python 3.12 uv base image. Moving to Python 3.13 or newer requires an explicit stack decision update.

**Evidence:** Verified on 2026-05-13 with `uv run python --version`, `uv run pytest -q`, `uv run ruff check .`, `uv run mypy src tests`, `uv build`, and `docker build -f services/rag-api/Dockerfile services/rag-api`. The uv settings reference documents `[tool.uv.build-backend].module-name` as the way to set the module directory name when it differs from the package name.

## 2026-05-14 - FastAPI Docker Base Image

**Context:** The initial FastAPI container image used the `ghcr.io/astral-sh/uv` base image. That created an unnecessary dependency on GitHub Container Registry during local Compose builds and could block offline or restricted-network development.

**Options Considered:** Keep the `ghcr.io/astral-sh/uv` base image, vendor a custom uv image, or start from `python:3.12-slim-bookworm` and install `uv` during image build.

**Decision:** Use `python:3.12-slim-bookworm` as the FastAPI Docker base image and install `uv` with `pip` during the image build.

**Rationale:** This removes the external GHCR dependency while keeping the service on the approved Python 3.12 stack and preserving the uv-managed dependency workflow.

**Tradeoffs:** The image now does a small extra package install step during build. That cost is acceptable for the MVP because it improves portability and avoids the GHCR pull failure path.

**Consequences:** Local Compose builds for `rag-api` should no longer depend on pulling the Astral uv base image from GHCR. The FastAPI Dockerfile should continue to use `uv sync` for locked dependency installation after bootstrapping `uv` in the image.

## 2026-05-13 - Frontend Scaffold Stack Alignment

**Context:** Task 4 starts from `create-vite` scaffolds, which defaulted to React 19 and template noise. The approved code standards already fixed the MVP frontend stack to React 18 with Tailwind CSS, `shadcn/ui`, and `lucide-react`.

**Options Considered:** Leave the Vite defaults, accept React 19 in the app scaffolds, or realign the scaffolds to the approved React 18 stack before continuing.

**Decision:** Reconfigure the three frontend apps to React 18.3.1, install shared frontend dependencies under the pnpm workspace, add `pnpm@10.33.4` to the root `packageManager` field, and add local shadcn-compatible support files (`components.json`, `src/lib/utils.ts`, and `src/components/ui/button.tsx`) in each app. The frontend Dockerfiles build from the repo root so they can see the workspace lockfile and app package files.

**Rationale:** The scaffolds must match the approved stack and the monorepo packaging model. Accepting the template defaults would silently diverge from the documented frontend baseline. The local shadcn-compatible support files are enough to begin the component system without depending on an interactive CLI during the foundation task.

**Tradeoffs:** The root `packageManager` now tracks a specific pnpm patch version instead of the coarse `pnpm@10` placeholder. That is acceptable because the workspace already depends on exact pnpm behavior for reproducible installs and builds.

**Consequences:** Future frontend work should continue from React 18.3.1 and the existing `pnpm` workspace. If the team wants a different React major or a fully scripted shadcn CLI bootstrap later, that should be recorded as a new stack decision.

**Evidence:** Verified on 2026-05-13 with `pnpm -r typecheck`, `pnpm -r test -- --run`, `pnpm -r build`, and Docker builds for `apps/manage-web`, `apps/chat-web`, and `apps/docs-web`.

## 2026-05-13 - Initial Database Migration Foundation

**Context:** Task 6 turns the approved schema ownership model into executable migrations for `.NET` and FastAPI. The project needs early proof that `.NET` writes only `app` objects and FastAPI writes only `rag` objects while both can be tested against a disposable Postgres instance with pgvector.

**Options Considered:** Keep schema definitions as documentation only, create migrations without container-backed ownership tests, or implement EF Core and Alembic migrations with disposable Postgres verification.

**Decision:** Implement the initial `.NET` `app` schema through EF Core migrations and the initial FastAPI `rag` schema through Alembic. EF migration history is stored under the `app` schema through `__EFMigrationsHistory`; Alembic versioning is stored under the `rag` schema through `rag.alembic_version`. Migration verification uses `pgvector/pgvector:pg16` containers for both backend stacks.

**Rationale:** The MVP's strongest data invariant is service ownership by schema. Container-backed migration tests prove the actual database result, including pgvector extension behavior, instead of trusting model metadata or migration files alone.

**Tradeoffs:** Testcontainers adds slower backend tests and requires Docker for full verification. That cost is acceptable because schema ownership failures would be expensive to unwind later.

**Consequences:** Future `.NET` schema changes must go through `AppDbContext`/EF migrations and stay in `app`. Future RAG schema changes must go through Alembic and stay in `rag` except for explicitly approved extension setup and read-only grants. Task 6 pins the added migration/testing packages in project manifests: `Npgsql.EntityFrameworkCore.PostgreSQL` `8.0.11`, EF Core packages `8.0.27`, `Testcontainers.PostgreSql` `4.11.0`, Python `pgvector` `0.4.2`, and Python `testcontainers[postgres]` `4.14.2`.

**Evidence:** Verified on 2026-05-13 with `dotnet test services/dotnet-api/AdvancedRag.sln`, `dotnet build services/dotnet-api/AdvancedRag.sln`, `uv run pytest -q`, `uv run ruff check .`, and `uv run mypy src tests`.

## 2026-05-13 - Task 7 Auth Foundation

**Context:** Task 7 implements the first authentication foundation across `.NET` and FastAPI: local user login, secure browser cookies, CSRF, chat-token issuance, and FastAPI chat-token validation. The earlier CSRF text mixed ASP.NET Core AntiForgery with a requirement that FastAPI validate the same CSRF token locally, which is not portable across services.

**Options Considered:** Use ASP.NET Core Identity with a custom store, use a smaller local auth service over the existing `app.users` table, use ASP.NET Core AntiForgery only for `.NET`, or use a signed double-submit CSRF token that both `.NET` and FastAPI can validate.

**Decision:** Use a local `.NET` auth service over `app.users`, cookie authentication with host-only `__Host-advanced-rag-session`, PBKDF2-SHA256 password hashes using `Rfc2898DeriveBytes`, signed double-submit CSRF tokens with `__Host-CSRF` plus `X-CSRF-Token`, and RS256 chat tokens signed by `.NET` with a `kid` header. FastAPI validates chat tokens locally with `pyjwt[crypto]` against configured public keys.

**Rationale:** This keeps the MVP inside the approved local-user model without introducing the complexity of a full ASP.NET Core Identity custom store before user administration exists. The signed double-submit CSRF approach preserves the security goal while allowing both backend services to validate the same browser CSRF contract.

**Tradeoffs:** The first auth foundation does not yet include password reset, lockout, rate limiting, revocation beyond short token TTLs, or full user administration flows. Those remain later tasks. Signed double-submit CSRF is simpler than framework AntiForgery but requires careful HMAC key handling through Compose secrets.

**Consequences:** Future mutating browser endpoints must validate the CSRF cookie/header pair. FastAPI chat and feedback endpoints must use the same HMAC CSRF rule when those routes are implemented. Chat tokens carry `sub`, `role`, `groups`, `attributes`, `access_scope_hash`, `corpus`, `exp`, `iat`, `iss`, `aud`, and `jti`; FastAPI must not add per-request `.NET` introspection in the normal chat path.

**Evidence:** Verified on 2026-05-13 with `dotnet test services/dotnet-api/AdvancedRag.sln --filter Auth`, `Set-Location services/rag-api; uv run pytest tests -k auth -q; Set-Location ..\..`, `dotnet test services/dotnet-api/AdvancedRag.sln`, `dotnet build services/dotnet-api/AdvancedRag.sln`, `uv run pytest -q`, `uv run ruff check .`, `uv run mypy src tests`, and `uv build`.

## 2026-05-14 - Task 8 User Administration And Budget Configuration

**Context:** Task 8 turns the local-user model into administrator-operated user, group, access scope, and AI budget configuration. The implementation must preserve the boundary where `.NET` owns `app` configuration data while FastAPI later enforces paid RAG budget limits from audit data.

**Options Considered:** Defer user administration until after document workflows, implement only budget editing, or implement the full users/groups/roles/status/budget management boundary now.

**Decision:** Implement `.NET` user administration use cases and endpoints now. `Admin` users can list/create users, assign roles, assign groups, activate/deactivate users, set per-user AI budget limits, and create groups. `GET /api/groups` is also available to `DocumentManager` because document workflows need selectable groups, while group mutation remains `Admin` only. The `manage-web` Task 8 screen lists users, groups, status, monthly budget, current spend, and remaining budget, and supports search/status filters plus CSRF-protected budget edits.

**Rationale:** User, group, and budget configuration are prerequisites for document access rules, chat authorization claims, and cost controls. Implementing them before document lifecycle work gives later tasks a stable access-scope and budget configuration surface.

**Tradeoffs:** Task 8 reports `currentSpendUsd` as `0` until the later RAG audit and budget enforcement tasks connect `.NET` reporting to `rag.query_audit_events`. The management screen currently focuses on budget editing; broader user creation and role/group editing UI can build on the new API surface in later management iterations.

**Consequences:** New users receive the default USD 5 monthly AI budget through `app.user_ai_budget_limits`. Role and group changes recompute the effective access scope hash used by chat tokens and future retrieval filtering. Mutating user/group endpoints rely on the Task 7 signed double-submit CSRF middleware. Frontend calls continue to use same-origin `/api/*`, credentials-included fetches, request IDs, and the shared error envelope.

**Evidence:** Verified on 2026-05-14 with `docker version`, `dotnet test services\dotnet-api\AdvancedRag.sln --filter "Users|Groups|Budget"`, `dotnet build services\dotnet-api\AdvancedRag.sln`, `pnpm.cmd --dir apps\manage-web test -- --run`, `pnpm.cmd --dir apps\manage-web typecheck`, and `pnpm.cmd --dir apps\manage-web build`. Earlier targeted verification also passed with `dotnet test services\dotnet-api\tests\AdvancedRag.App.Tests\AdvancedRag.App.Tests.csproj --filter UserAdministration`, `dotnet test services\dotnet-api\tests\AdvancedRag.Api.Tests\AdvancedRag.Api.Tests.csproj --filter UserAdministration`, and `git diff --check`.

## 2026-05-17 - Task 9 Document Lifecycle And Assisted Imports

**Context:** Task 9 adds the first production document management workflow after the `.NET` HTTP boundary moved to MVC controllers. The implementation must support draft editing, review transitions, assisted PDF/DOCX text extraction, archive/restore actions, audit events, safe error envelopes, and management UI states without implementing the FastAPI indexing pipeline early.

**Options Considered:** Implement publish end-to-end with FastAPI indexing during Task 9, keep Task 9 focused on `.NET` lifecycle plus import extraction, or defer document UI until after indexing.

**Decision:** Keep Task 9 focused on `.NET` document lifecycle, import extraction, management UI, and pre-indexing status. Publish requests mark the current draft version as indexing `Pending`; the actual internal FastAPI indexing endpoint, chunking, embeddings, and publish-on-success behavior remain Task 10 scope.

**Rationale:** The implementation plan separates lifecycle/import work from internal indexing. Preserving that boundary keeps Task 9 verifiable without introducing a partial FastAPI indexing contract or mixing `app` and `rag` responsibilities.

**Tradeoffs:** The management UI can show pending indexing status, but a document is not fully published until Task 10 implements the indexing pipeline and success transition. This is acceptable because public chat retrieval is not implemented yet.

**Consequences:** The `.NET` app schema now tracks `document_versions.indexing_status`. The document service sanitizes stored document HTML through `Ganss.Xss` `HtmlSanitizer` `9.0.892`, extracts DOCX text with `DocumentFormat.OpenXml` `3.5.1`, and extracts PDF text with `PdfPig` `0.1.14`. Management document UI strings are Spanish, while code and project context remain English.

**Evidence:** Verified on 2026-05-17 with `dotnet test services\dotnet-api\AdvancedRag.sln --filter "Document|Import|Lifecycle"`, `pnpm.cmd --dir apps\manage-web test -- --run`, `dotnet test services\dotnet-api\AdvancedRag.sln`, `dotnet build services\dotnet-api\AdvancedRag.sln`, `pnpm.cmd --dir apps\manage-web typecheck`, and `pnpm.cmd --dir apps\manage-web build`. .NET verification emitted NU1900 vulnerability-metadata warnings because NuGet could not fetch `https://api.nuget.org/v3/index.json`; tests and builds still passed.

## 2026-05-17 - Task 10 Internal Indexing Pipeline

**Context:** Task 10 connects `.NET` publish requests to the FastAPI-owned indexing pipeline. The implementation must preserve service ownership: `.NET` owns document lifecycle state in `app`, while FastAPI owns `rag.indexing_jobs`, chunking, embeddings, and `rag.document_chunks`.

**Options Considered:** Keep Task 9's pending-only publish behavior, add a callback/status polling protocol, or keep pre-publication indexing synchronous from `.NET`'s perspective by awaiting the internal FastAPI indexing result during the publish request.

**Decision:** Implement synchronous pre-publication indexing for the MVP. `.NET` calls FastAPI's Docker-network-only `POST /internal/indexing-jobs` endpoint with `X-Internal-Service-Token`. FastAPI validates the token, creates an indexing job, chunks saved normalized HTML, requests embeddings through an embedding provider abstraction with configured dimensions, stores active chunks in `rag.document_chunks`, and returns a safe job result. `.NET` transitions the version to `Published` only on `Succeeded`; failed indexing leaves the document `In Review` with `IndexingStatus.Failed` and exposes safe `INDEXING_FAILED` behavior.

**Rationale:** The architecture already requires publication to be blocked until successful pre-publication indexing. Synchronous indexing is simpler for the MVP than a callback or poller and keeps all public retrieval dependent on successfully indexed published versions.

**Tradeoffs:** Publish requests can take longer while chunking and embeddings run. This is acceptable for the MVP because publishing is an administrative workflow, not a high-volume public chat path. A later background worker can preserve the same internal contract while changing execution mechanics.

**Consequences:** FastAPI now owns the first executable internal indexing contract and uses a fakeable embedding provider in tests. `.NET` stores the returned indexing job id on the published version and preserves safe failure semantics. Compose must expose the internal service token file to FastAPI through `INTERNAL_SERVICE_TOKEN_FILE`.

**Evidence:** Verified on 2026-05-17 with `dotnet test services/dotnet-api/AdvancedRag.sln --filter Indexing`, `Set-Location services/rag-api; uv run pytest tests -k indexing -q; Set-Location ..\..`, `dotnet test services/dotnet-api/AdvancedRag.sln`, `dotnet build services/dotnet-api/AdvancedRag.sln`, `uv run pytest -q`, `uv run ruff check .`, `uv run mypy src tests`, and `docker compose --env-file infra/compose/.env.example -f infra/compose/compose.yaml config --no-path-resolution --no-consistency -q`.

## 2026-05-17 - Task 11 Chat RAG Core

**Context:** Task 11 turns the indexed published corpus into the first public chat/RAG path. The implementation must enforce document access in SQL, avoid treating `access_scope_hash` as authorization, record durable RAG audit evidence, support semantic cache partitioning and invalidation, and block paid provider work when a user's monthly AI budget is exhausted.

**Options Considered:** Implement chat as a thin endpoint over provider calls, implement retrieval and budget enforcement as testable services, or defer cache and budget until after the chat frontend.

**Decision:** Implement the FastAPI chat/RAG core now as a service behind `/api/chat`, with injectable embedding, chat completion, and chat-token validator dependencies for testability. Retrieval filters chunks at SQL level through `.NET`-owned `app.document_permissions`; viewers are forced to the `published` corpus. Query audit writes store citations, model/pricing/cost evidence, latency, request ID, corpus, prompt version, chunker version, and access scope hash. Semantic cache entries are keyed by corpus and `access_scope_hash`, track source documents, and are invalidated through an internal service-token-protected endpoint. AI budget checks run before embedding or chat completion provider calls.

**Rationale:** This preserves the approved service boundary while creating a verifiable RAG core before feedback, viewer citation exchange, and the chat frontend are layered on. Injected providers keep integration tests deterministic and avoid spending real OpenAI credits during verification.

**Tradeoffs:** The first implementation streams the completed answer as one SSE `answer-token` event instead of token-by-token provider streaming. That is sufficient for the Task 11 backend contract and keeps Task 14 responsible for frontend streaming UX. Chat cost calculation records a single pricing snapshot id while using both embedding and chat pricing values to estimate total cost; a richer multi-pricing audit model can be added if reporting needs per-provider-line attribution later.

**Consequences:** FastAPI now has the executable public chat boundary. Over-budget users receive `AI_BUDGET_EXCEEDED` before paid provider work. Cache reuse is partitioned by effective scope and never authorizes retrieval. The async SQLAlchemy engine uses `NullPool` to avoid cross-event-loop asyncpg connection reuse in Windows/TestClient integration tests.

**Evidence:** Verified on 2026-05-17 with `uv run pytest tests/test_chat_rag.py -q` (`3 passed`), `uv run ruff check .` (`All checks passed!`), `uv run mypy src tests` (`Success: no issues found in 28 source files`), and `uv run pytest -q` (`21 passed`).

## 2026-05-14 - Backend HTTP Organization

**Context:** After Task 8, the `.NET` API used Minimal API endpoint files for auth and user administration. The user prefers a more familiar MVC-style backend structure with controllers, models, and explicit interfaces. The same maintainability preference should also guide FastAPI module organization without forcing Python into non-idiomatic ASP.NET naming.

**Options Considered:** Continue with Minimal API endpoint modules, use global `Controllers`/`Models`/`Interfaces` folders everywhere, or use MVC controllers for `.NET` feature routes and an equivalent FastAPI separation through routers, schemas, services, and infrastructure adapters.

**Decision:** Use ASP.NET Core MVC controllers as the default HTTP boundary for `.NET` feature routes. Feature DTOs live under `AdvancedRag.Api/Models/<Feature>/`, controllers stay thin, and business behavior remains in `AdvancedRag.App` services/use cases. Application service interfaces stay beside their feature use cases instead of a global `Interfaces` folder. Minimal APIs remain acceptable only for very small infrastructure endpoints such as health checks. FastAPI follows the analogous structure: `api/routers` for route/controller logic, `schemas` for Pydantic request/response models, feature services for business behavior, and infrastructure adapters/repositories for databases or external providers.

**Rationale:** Controllers and explicit model folders improve navigability for the user and future contributors while preserving the existing clean layering. Keeping feature interfaces beside use cases avoids the common low-cohesion `Interfaces` folder problem. FastAPI routers/schemas/services match the same mental model without fighting Python conventions.

**Tradeoffs:** Refactoring the existing Task 7 and Task 8 Minimal API files adds a small amount of ceremony and needs regression verification. The benefit is a clearer structure before the document lifecycle module adds many more routes and DTOs.

**Consequences:** Before implementing Task 9 document lifecycle endpoints, refactor the current `.NET` HTTP boundary from endpoint files to MVC controllers and move request/response DTOs into API model folders. Future FastAPI chat, feedback, indexing, budget, and retrieval endpoints should be added through routers and schemas rather than defining route functions directly in `main.py`.

## 2026-05-18 - Backend Readability And Boundary Data Rules

**Context:** Before Task 12, the user asked to tighten project rules for `.NET` local variable readability, FastAPI data modeling, endpoint testability through Postman, and project-specific assistant behavior. A quick scan showed existing `.NET` code already contains many `var` declarations and FastAPI uses a small number of standard-library dataclasses, so a blanket mechanical refactor would create noisy diffs without directly improving Task 12 behavior.

**Options Considered:** Ban `var` in all `.NET` code, prefer explicit local types only when they improve readability, convert all FastAPI internal data carriers to Pydantic, or limit Pydantic requirements to boundaries and validated contracts.

**Decision:** Prefer explicit `.NET` local variable types in new or actively refactored production code and tests when the concrete type is clear and improves readability. Keep `var` allowed when the explicit type is unavailable or noisier, including anonymous types, LINQ projections, deconstruction, and initializer-obvious cases. Use Pydantic `BaseModel` for FastAPI request/response schemas, API/provider contracts, persisted/read-model DTOs crossing boundaries, and configuration through `pydantic-settings`. Do not use standard-library `@dataclass` for boundary data, configuration, or validated contracts. Internal simple classes or dataclasses remain allowed only for private implementation details that do not cross module/service boundaries and do not need validation, serialization, aliases, or schema behavior.

**Rationale:** The rules improve readability and contract consistency without turning the next implementation task into a broad style-only rewrite. Pydantic is the correct default where validation, serialization, aliases, and OpenAPI/schema generation matter. Plain Python classes or dataclasses can still be appropriate for small private implementation details where Pydantic would add ceremony without a boundary benefit.

**Tradeoffs:** Existing code will not be fully normalized immediately, so both styles may coexist until touched by normal work or a dedicated refactor. This avoids churn but requires future edits to follow the updated standards.

**Consequences:** Task 12 and later tasks should use explicit `.NET` local variable types where helpful, Pydantic for FastAPI boundary/contracts, and include a concise Postman endpoint checklist in the final task message when API endpoints are added or changed. These Postman checklists are user-facing completion notes, not durable docs, unless the user requests documentation.

## 2026-05-18 - Task 12 Feedback And Management Reporting

**Context:** Task 12 implements chat answer feedback and management feedback review. The implementation must preserve the boundary where FastAPI owns feedback writes on `rag.query_audit_events`, while `.NET` exposes management reporting through read-only access to FastAPI-owned reporting views.

**Options Considered:** Let the management frontend call FastAPI directly, let `.NET` query RAG audit tables directly, or create FastAPI-owned RAG reporting views and expose them through a `.NET` management endpoint.

**Decision:** Store one feedback value/comment directly on `rag.query_audit_events` through FastAPI `POST /api/feedback/{query_audit_event_id}`. Add the audited answer id to the chat SSE citations payload so the chat UI can submit feedback for the correct answer. Create FastAPI-owned views `rag.v_query_audit_with_citations` and `rag.v_feedback_summary`, grant select to `app_reporting_reader` when that role exists, and expose management review through `.NET` `GET /api/reporting/feedback`.

**Rationale:** The approach keeps feedback writes in the RAG service, keeps management UI calls behind the `.NET` management API, and avoids cross-schema writes. The SSE audit id is necessary because feedback is tied to generated or cached answer audit rows, not to document citations alone.

**Tradeoffs:** The MVP reporting view returns user ids and RAG-side audit fields; richer user display metadata can be joined through an approved reporting path later if needed. Feedback history is still not retained; same-user updates overwrite the single value/comment as previously decided.

**Consequences:** Chat feedback can be submitted or updated by the same authenticated chat user only for their own query audit event. Management reporting supports negative-only, cited-document, user, and date-range filters through `.NET`, with the frontend calling only same-origin `/api/reporting/feedback`.

**Evidence:** Verified on 2026-05-18 with `uv run pytest tests -k feedback -q`, `dotnet test services/dotnet-api/AdvancedRag.sln --filter Reporting`, `pnpm.cmd --dir apps/chat-web test -- --run`, `pnpm.cmd --dir apps/manage-web test -- --run`, plus full FastAPI tests/lint/type checks, `.NET` build, and chat/manage frontend typecheck/build. `.NET` commands emitted NU1900 warnings because NuGet vulnerability metadata could not be fetched; build and tests passed.

## 2026-05-18 - Task 13 Viewer Exchange And Document Viewer

**Context:** Task 13 implements the secure citation/document opening path. The implementation must keep real viewer tokens out of URLs and browser JavaScript, use one-time short-lived exchange codes, revalidate document state during exchange and document loading, and support both chat-origin published links and management-origin draft/review/published links.

**Options Considered:** Put a viewer JWT directly in citation URLs, make exchange codes reusable until expiration, or persist one-time exchange codes that set a host-only viewer-token cookie.

**Decision:** Persist one-time 60-second exchange codes in `app.viewer_exchange_codes`, issue RS256 viewer tokens only through `POST /api/viewer/exchange`, set the token in a host-only `__Host-viewer-token` cookie, and audit token issuance in `app.viewer_token_audit`. Chat-created links allow only `Published`; management-created links allow `Draft`, `In Review`, and `Published` for `Admin` or `DocumentManager`. Viewer tokens are document-scoped and reusable for 15 minutes.

**Rationale:** This preserves the approved browser security model: URLs contain only short-lived exchange codes, not credential-bearing tokens. Revalidating state during exchange and document load prevents stale links from becoming authorization. Using the existing RS256 key store keeps token signing consistent with chat tokens while keeping viewer audience and claims separate.

**Tradeoffs:** The docs frontend must perform an extra exchange request before document loading, and management/chat need an additional link-creation call before navigation. The added round trip is acceptable because document opening is less latency-sensitive than chat answer generation.

**Consequences:** `.NET` owns all viewer exchange and document loading APIs under `/api/viewer/*`. `docs-web` handles expired, used, invalid, unauthorized, token-expired, not-found, loading, and success states. `chat-web` and `manage-web` request exchange-code links before opening documents instead of constructing docs URLs locally.

**Evidence:** Verified on 2026-05-18 with `dotnet test services\dotnet-api\AdvancedRag.sln --filter Viewer`, `pnpm.cmd --dir apps\docs-web test -- --run`, `pnpm.cmd --dir apps\chat-web test -- --run`, `pnpm.cmd --dir apps\manage-web test -- --run`, `pnpm.cmd --dir apps\docs-web typecheck`, `pnpm.cmd --dir apps\chat-web typecheck`, `pnpm.cmd --dir apps\manage-web typecheck`, `pnpm.cmd --dir apps\docs-web build`, `pnpm.cmd --dir apps\chat-web build`, `pnpm.cmd --dir apps\manage-web build`, and `dotnet build services\dotnet-api\AdvancedRag.sln`. `.NET` commands emitted NU1900 warnings because NuGet vulnerability metadata could not be fetched; build and tests passed.

## 2026-05-18 - Task 14 Chat Frontend Workflow

**Context:** Task 14 turns the backend `/api/chat` SSE contract, feedback endpoint, and secure viewer-link endpoint into a usable chat frontend. The implementation must keep chat focused on question entry, answer reading, citations, feedback, budget-limited states, auth recovery, and safe errors without crossing service boundaries.

**Options Considered:** Keep the previous minimal chat shell, add a full chat-history product surface now, or implement the Task 14 single-question workflow with complete production states.

**Decision:** Implement the Task 14 chat frontend as a compact single-question workflow. `apps/chat-web` calls FastAPI through same-origin `/api/chat` and `/api/feedback/*`, calls `.NET` only for `/api/auth/chat-token`, `/api/csrf`, and `/api/viewer/links`, parses SSE events into typed UI state, retries once after `AUTH_TOKEN_EXPIRED` by renewing the chat token, and shows Spanish states for empty, loading, answer, semantic cache hit, citations, feedback, budget exhaustion, auth expiration, and generic errors with request IDs.

**Rationale:** The MVP needs a reliable operational chat workflow before broader conversation history. Keeping the UI single-question and state-complete reduces scope while still exercising the critical RAG, feedback, budget, and viewer-link paths.

**Tradeoffs:** The frontend still treats each question independently and does not persist a multi-turn chat transcript. That matches the current RAG spec, where conversation memory across user sessions is out of scope for the MVP.

**Consequences:** Future chat history work can build on the typed SSE parser and error-state mapping without changing the backend contract. Over-budget users see a service-limited AI state instead of a general account lockout, and authorized document opening remains available through citation links.

**Evidence:** Verified on 2026-05-18 with `pnpm.cmd --dir apps\chat-web test -- --run` (`10 passed`), `pnpm.cmd --dir apps\chat-web typecheck`, and `pnpm.cmd --dir apps\chat-web build`.

## 2026-05-18 - Task 15 Management Frontend Workflow

**Context:** Task 15 turns the management frontend into a persistent operational console on top of existing `.NET` APIs for documents, users/groups, AI budgets, viewer links, and feedback reporting. The management UI must stay dense, scannable, Spanish-facing, and boundary-safe: it calls only same-origin `.NET` API routes and never calls FastAPI directly.

**Options Considered:** Keep per-screen navigation, build a shared management shell, defer configuration/audit placeholders, or expose configuration as a read-only operational view.

**Decision:** Use one persistent management shell navigation covering documents, users/groups, audit, feedback, AI budgets, and configuration. Keep documents, users/budgets, feedback review, audit, and configuration as separate screen components under that shell. Add failed-indexing retry through the existing `.NET` publish request route. Add `.NET` `GET /api/configuration` plus a read-only configuration screen for non-sensitive operational settings and secret-backed status indicators without exposing secret values.

**Rationale:** A shared shell gives management users a stable operational information architecture and removes inconsistent per-screen navigation. The retry action belongs in document operations because indexing failure blocks publication. Configuration should be visible to operators, but secret values must remain outside the browser.

**Tradeoffs:** The audit workspace is currently a frontend-level operational placeholder until Task 16+ adds dedicated audit/rate-limit/logging read models. The configuration endpoint intentionally reports secret status only (`Configured`/`Missing`) and not secret values, so operators still need filesystem or Compose access to rotate credentials.

**Consequences:** `apps/manage-web` now has a shared navigation component, a typed operational configuration client, a configuration screen, an audit screen, and document indexing retry UI. `.NET` exposes the corresponding read-only management configuration route. Management frontend service boundaries remain intact because all calls are same-origin `.NET` API routes.

**Evidence:** Verified on 2026-05-18 with `dotnet test services/dotnet-api/AdvancedRag.sln --filter Configuration` (`1 passed` in API tests), `pnpm.cmd --dir apps/manage-web test -- --run` (`18 passed`), `pnpm.cmd --dir apps/manage-web typecheck`, and `pnpm.cmd --dir apps/manage-web build`.

**Follow-up:** The configuration screen reports whether secret-backed settings are configured by checking file presence from `.NET`. Therefore, Compose must mount the relevant status-only secret files into `dotnet-api` as well as their primary runtime services. The endpoint remains read-only and must return only `Configured`/`Missing`, never secret values.

## 2026-05-18 - Task 16 Operational Hardening

**Context:** Task 16 implements the MVP's technical stability controls: rate limits, readiness checks, JSON technical request logs, and Compose health checks. These controls must remain separate from AI budget enforcement and must work within the approved single-tenant Docker Compose deployment.

**Options Considered:** Use per-process in-memory fixed-window counters, add a shared external rate-limit store, or persist technical counters in Postgres. For logs, use the approved daily JSON file approach rather than adding OpenTelemetry or a remote aggregator.

**Decision:** Use per-process fixed-window counters for MVP technical rate limits in `.NET` and FastAPI. Add explicit stable error codes for each limited workflow. Add readiness checks that validate backend critical dependencies while keeping liveness process-only. Write daily JSON request logs from each backend to mounted log volumes. Gate Caddy startup on healthy frontend and backend services through Compose health checks.

**Rationale:** The MVP deployment is single-instance per service under Docker Compose, so in-memory counters are sufficient and avoid adding Redis or cross-schema technical counter writes. Daily JSON logs match the architecture context and provide enough operational evidence for the MVP without introducing tracing infrastructure.

**Tradeoffs:** In-memory rate limits reset on service restart and are not safe for horizontal scaling. If the platform later runs multiple replicas per service, technical rate-limit state must move to shared storage. The current JSON file loggers are intentionally simple and local; remote aggregation remains a later operational enhancement.

**Consequences:** Technical rate-limit failures now return `LOGIN_IP_RATE_LIMITED`, `LOGIN_USER_RATE_LIMITED`, `CHAT_RATE_LIMITED`, `IMPORT_RATE_LIMITED`, or `VIEWER_EXCHANGE_RATE_LIMITED`. `/health/ready` now fails with 503 when critical dependencies are missing or unavailable, while `/health/live` stays independent. Compose can gate Caddy on service health.

**Evidence:** Verified on 2026-05-18 with `dotnet test services/dotnet-api/AdvancedRag.sln --filter "RateLimit|Health|Logging"` (`9 passed`), `uv run pytest tests -k "rate_limit or health or logging" -q` (`6 passed, 26 deselected`), `docker compose --env-file infra/compose/.env.example -f infra/compose/compose.yaml config`, `dotnet build services/dotnet-api/AdvancedRag.sln`, `uv run ruff check .`, `uv run mypy src tests`, and `git diff --check`. `.NET` commands emitted NU1900 warnings because NuGet vulnerability metadata could not be fetched; build and tests passed.

## 2026-05-18 - Task 17 E2E Bootstrap Strategy

**Context:** The MVP has local authentication and user administration, but no first-admin bootstrap UI or public seed endpoint. The end-to-end test must create the initial admin and document manager users before it can exercise the browser workflows.

**Options Considered:** Add a temporary bootstrap API, drive manual seed steps outside the test, or seed deterministic E2E records directly through the Compose PostgreSQL service.

**Decision:** Seed deterministic E2E roles, users, password hashes, and model pricing directly through Compose PostgreSQL from the Playwright test setup.

**Rationale:** This keeps bootstrap test data outside the production API surface and avoids adding a one-off product endpoint only for E2E setup. The seeded records use fixed IDs and E2E-only emails so the setup can clean up and rerun safely.

**Tradeoffs:** The E2E setup knows database details. That is acceptable for initial bootstrap only and should be replaced if the product later adds a supported first-admin setup flow.

**Consequences:** `tests/e2e/specs/mvp-happy-path.spec.ts` owns the deterministic seed/cleanup routine. Runtime product code still owns all workflow behavior after bootstrap.

## 2026-05-18 - Task 17 Viewer Reload Behavior

**Context:** Viewer exchange codes are intentionally one-time. During E2E verification, the docs frontend successfully exchanged the code and set a host-only viewer cookie, but a browser reload re-used the still-visible `?code=` query parameter and failed with `VIEWER_CODE_USED`.

**Options Considered:** Make exchange codes reusable during their TTL, let the frontend keep the code in the URL and tolerate the error, or remove the code from the URL after successful exchange.

**Decision:** Keep exchange codes single-use and remove `code` from the docs URL with `history.replaceState` after successful exchange.

**Rationale:** The real viewer token is already stored as an `HttpOnly` cookie. Keeping the consumed exchange code in the URL creates a reload hazard without adding security or usability value.

**Tradeoffs:** The visible URL no longer contains the original code after the document opens. That is desirable because the code is no longer useful.

**Consequences:** Reloading an already-open docs page uses the existing viewer cookie and preserves authorized document access, including after AI budget exhaustion blocks new paid chat requests.

## 2026-05-18 - Task 17 Budget Exhaustion E2E Scope

**Context:** The architecture already states that AI budget exhaustion blocks new paid chat/RAG work but must not revoke authorized document viewing. Task 17 needed an executable full-stack verification of that boundary.

**Options Considered:** Verify only API responses, verify only frontend budget messaging, or verify the full browser workflow before and after lowering the user's AI budget to zero.

**Decision:** The E2E happy path lowers the viewer's AI budget through the management UI, verifies the next chat request shows the budget-limited state, and then verifies the already-open document remains accessible through the viewer cookie.

**Rationale:** This is the highest-risk cross-service boundary in the MVP: FastAPI enforces paid AI usage, `.NET` owns budget configuration and viewer documents, and Caddy/frontends must preserve their separate access paths.

**Tradeoffs:** The E2E test depends on a live Compose stack and a configured OpenAI key for the chat/indexing path. Unit and integration tests remain the faster deterministic layer for everyday development.

**Consequences:** Future budget changes must preserve the distinction between AI spend controls and document authorization.

## 2026-05-18 - Task 17 Local Compose Deep Links And Viewer Host

**Context:** The E2E path opens docs deep links such as `/open?code=...` and relies on `.NET` generating viewer URLs for the active local docs host.

**Options Considered:** Configure each frontend nginx container independently, let nginx return 404 for unknown paths, or add a shared SPA fallback config. For viewer links, rely on production defaults or set the local docs base URL explicitly in Compose.

**Decision:** Use a shared `infra/compose/frontend-nginx.conf` for all frontend containers with SPA fallback and IPv4/IPv6 localhost listeners. Set `.NET` `Viewer__DocsBaseUrl` in local Compose to `https://docs.${PUBLIC_DOMAIN}`.

**Rationale:** React frontends must handle browser deep links after nginx serves `index.html`. Compose health checks also need `localhost` to work whether the container resolves it to IPv4 or IPv6. `.NET` should generate viewer links for the local docs host during Compose verification instead of the production placeholder domain.

**Tradeoffs:** The Dockerfiles now depend on the shared Compose nginx config. That is acceptable because these images are currently part of the local Compose deployment baseline.

**Consequences:** Local E2E and manual browser tests can open management, chat, and docs deep links reliably through Caddy.

## 2026-05-18 - Task 17.5 First-Run Setup And UI Stabilization

**Context:** Task 17 verified the full MVP path by seeding deterministic users and pricing directly into PostgreSQL from Playwright. User acceptance review after Task 17 found that this is not sufficient for product usability: a clean deployment has no human first-run admin setup, incomplete login/register surfaces, no friendly local demo bootstrap, and a UI that is too scaffold-like for a credible MVP demonstration.

**Options Considered:** Proceed directly to documentation, rely on SQL seed scripts and explain them in docs, or insert a product stabilization task before handoff.

**Decision:** Insert Task 17.5 before Task 18. Task 17.5 adds a production-safe first-run admin setup flow, real login/bootstrap UI, optional local demo seed support, and UI polish across management, chat, and viewer. The approved visual direction is captured in Stitch project `projects/544909270556047969` with design system `assets/df5cbb6e08e34c07abdf928b24898e57`.

**Rationale:** Documentation should describe a usable product flow, not freeze a hidden SQL-bootstrap workaround. A sellable single-tenant product needs an explicit first-run path for the first administrator and a management UI that can be used from a browser without internal test knowledge. UI polish is not cosmetic here; it is necessary to make the MVP demonstrable and operable.

**Tradeoffs:** This delays final handoff documentation and expands the roadmap after Task 17. The tradeoff is acceptable because the missing first-run setup and rough UI would otherwise become support burden and undermine confidence in the MVP.

**Consequences:** Task 18 is blocked until Task 17.5 is implemented and verified. The new acceptance bar is a clean deployment that can create the first admin from `https://manage.localhost`, log in, manage users/groups/documents through the UI, reach chat/viewer surfaces, and pass first-run E2E verification. SQL seeding remains acceptable for technical E2E setup or optional local demo data, but not as the only way for a human to start using the product.

## 2026-05-19 - Task 17.5 UI Polish Skill Adaptation

**Context:** The user asked whether the Cult UI Claude skills under `nolly-studio/cult-ui` could be adapted for the Task 17.5 redesign of the web apps. The upstream guidance is useful for component architecture and animation performance, but the project already has stricter product direction: dense operational SaaS screens, Spanish es-AR UI strings, React 18, Tailwind CSS, shadcn-style local components, and no new unapproved UI dependencies.

**Options Considered:** Copy the upstream Claude skills verbatim, install Cult UI components as a dependency, install generic UI design skills directly, ignore the upstream skills and rely only on existing context, or create a local Codex adaptation that uses the useful component, anti-generic-UI, direction-setting, validation, and motion rules while preserving Advanced RAG source-of-truth precedence.

**Decision:** Create the local Codex skill `advanced-rag-product-ui-polish` as a Task 17.5 quality gate and add the effective Task 17.5 UI polish rules to `context/ui-context.md`. Cult UI, Uncodixfy, and `codex-design-skill` are treated as selective references, not installed project dependencies.

**Rationale:** A local adaptation gives the implementation a concrete checklist for accessibility, component boundaries, Tailwind styling, state completeness, motion restraint, anti-generic-UI review, visual direction, and final validation without weakening the approved architecture or introducing a new component dependency.

**Tradeoffs:** The skill lives in the local Codex skills directory, so the canonical project rules still need to live in `context/ui-context.md`. The skill should guide implementation discipline, but project context remains authoritative.

**Consequences:** Task 17.5 should use the skill before and during UI edits. Cult UI remains inspiration/process guidance only unless a later implementation decision explicitly approves copying a specific component or adding a dependency. Uncodixfy contributes anti-pattern guardrails. `codex-design-skill` contributes the direction-setting and validation-pass pattern only; its Next.js and 21st.dev assumptions do not apply to this Vite React project.

## 2026-05-19 - Task 17.5 Bootstrap Concurrency And Base Roles

**Context:** A clean deployment has no users and no guaranteed role rows. First-run setup must be public enough for an operator to create the first administrator, but it must close permanently once an Admin exists and must not allow two concurrent first-admin creations.

**Options Considered:** Seed roles/admin users through SQL, create roles in migrations, create roles during first-run setup without a lock, or create roles during first-run setup under a database transaction lock.

**Decision:** `POST /api/setup/admin` creates the first active Admin under a PostgreSQL transaction-level advisory lock, ensures the base roles `Admin`, `DocumentManager`, and `Viewer` exist, assigns the Admin role and the default USD 5 monthly AI budget, then permanently blocks future setup attempts with stable code `SETUP_ALREADY_COMPLETED`.

**Rationale:** Setup is the only human-safe bootstrap path for clean deployments. Creating base roles during setup keeps migrations neutral and avoids hidden SQL seed requirements. The advisory lock prevents concurrent setup requests from creating multiple first administrators.

**Tradeoffs:** The setup repository now uses a PostgreSQL-specific advisory lock, which is acceptable because PostgreSQL is the approved database for the MVP. Local technical E2E can still seed data directly, but product first-run no longer depends on SQL seeding.

**Consequences:** Management UI can start from `/api/setup/status`; once any Admin role assignment exists, setup is closed even if that user is later deactivated. Operators must recover from a lost/deactivated only-admin scenario through an explicit operational procedure rather than reopening public setup.

## 2026-05-20 - Task 17.5 Management Information Architecture

**Context:** User review found that the management app had duplicate or misplaced screens: `Usuarios y grupos` and `Presupuestos IA` showed the same user-budget surface, `Feedback` was a standalone screen even though feedback is audit evidence, and `Documentos` lacked a create action, a stronger HTML editor, and filters across the visible document attributes.

**Options Considered:** Keep the Task 15 navigation unchanged, add more separate management screens, or consolidate duplicate surfaces into the workflow where operators naturally need them.

**Decision:** Keep management navigation focused on `Documentos`, `Usuarios y grupos`, `Auditoria`, and `Configuracion`. Per-user AI budget controls remain inside `Usuarios y grupos`. Feedback review is embedded inside `Auditoria`. The document list exposes filters for search text, lifecycle state, indexing state, document type, audience, and access-group coverage. The document summary API includes `documentType`, `audience`, and `allowedGroupIds` so the frontend can filter without opening each document. The Task 17.5 editor uses a dependency-free local HTML editor toolbar as an interim implementation until the approved TipTap dependencies are installed.

**Rationale:** The consolidated navigation removes duplicate destinations and aligns screens with operator mental models: budgets are user configuration, while chat feedback belongs to audit/reporting. Document filtering needs list-level metadata to stay fast and predictable. The local editor improves the MVP immediately without adding a dependency installation checkpoint in the middle of the user review pass.

**Tradeoffs:** The interim editor is less robust than the approved TipTap target and should be replaced when frontend dependency installation is scheduled. Adding document metadata to the list response slightly expands the `.NET` document summary contract, but it avoids inefficient client-side detail fetching for basic filters.

**Consequences:** Future management UI work should not reintroduce separate `Feedback` or `Presupuestos IA` navigation entries unless the product gains distinct workflows. Task 17.5 E2E/browser verification must cover document creation and audit feedback from the consolidated navigation.

**Evidence:** Verified on 2026-05-20 with `pnpm.cmd --dir apps\manage-web test -- --run App.test.tsx` (`24 passed`), `pnpm.cmd --dir apps\manage-web typecheck`, `pnpm.cmd --dir apps\manage-web build`, `dotnet build services\dotnet-api\AdvancedRag.sln`, and `dotnet test services\dotnet-api\AdvancedRag.sln --filter Document` (`22 passed`). `.NET` commands emitted the existing `NU1900` warnings because NuGet vulnerability metadata could not be fetched from `https://api.nuget.org/v3/index.json`; build and tests passed.

## 2026-05-20 - Task 17.5 Management Workspace Refinement And TipTap

**Context:** Follow-up user review found that document creation should not be a modal because it is a complex screen, requested TipTap in the editor, requested logical user deactivation, asked for clearer audit meaning, and asked to move session identity/logout from the top bar into the lower sidebar.

**Options Considered:** Keep the document editor as a modal and only swap the inner editor, create a separate navigation item for document creation, or keep creation inside `Documentos` as a full workspace tab.

**Decision:** `Documentos` now uses internal `Listado` and `Editor` tabs. Creating or editing a document opens the full workspace editor tab. The editor uses TipTap with starter kit, link, image, underline, and table extensions. Users can be logically deactivated/reactivated from `Usuarios y grupos` through the existing `.NET` `PATCH /api/users/{id}/status` endpoint. The management shell shows session identity and logout at the bottom of the sidebar. `Auditoria` now labels functional management events separately from embedded chat feedback evidence.

**Rationale:** Document authoring needs enough room for metadata, group access, import, validation, and rich editing controls, so a modal creates unnecessary cramped workflow. TipTap is the approved editor foundation and table/underline extensions close the editor capability gap already documented in UI context. Logical deactivation preserves auditability and recovery. Moving session controls into the sidebar removes a redundant top chrome row and leaves more vertical space for dense workspaces.

**Tradeoffs:** TipTap increases the management bundle size and Vite reports a chunk-size warning during build. This is acceptable for the MVP because the editor is core management functionality; later optimization can lazy-load the editor workspace if startup weight becomes a user-visible problem. Functional audit events still need a dedicated read model before the table can show real event rows.

**Consequences:** Future management UI work should treat document authoring as a workspace-level flow rather than a modal. User status changes must remain logical, not physical deletes. Audit work should connect a functional-event read model instead of mixing placeholder rows with chat feedback data.

**Evidence:** Verified on 2026-05-20 with `pnpm.cmd --dir apps\manage-web test -- --run App.test.tsx` (`26 passed`), `pnpm.cmd --dir apps\manage-web typecheck`, and `pnpm.cmd --dir apps\manage-web build`. The build passed with the expected Vite chunk-size warning after adding TipTap.

## 2026-05-20 - Task 17.5 Feedback And Functional Audit Separation

**Context:** User review found that the audit workspace looked empty from an activity perspective while also containing chat feedback. The embedded feedback table made it harder to understand that functional audit events are still missing a dedicated read model.

**Options Considered:** Keep feedback embedded in `Auditoria`, rename the embedded section, or restore `Feedback` as a separate management destination while reserving `Auditoria` for functional activity.

**Decision:** Restore `Feedback` as a separate management navigation entry and route. Remove the embedded feedback table from `Auditoria`. Keep `Auditoria` focused on functional management activity and leave its current empty state until a real audit-event read model is implemented.

**Rationale:** Feedback is audit evidence, but it is not a substitute for functional activity logs. A separate feedback workspace makes the available reporting explicit and keeps the audit placeholder honest about the missing event stream.

**Tradeoffs:** Management navigation now has one more item again. This is preferable to implying that chat feedback is the same thing as functional audit activity.

**Consequences:** Future functional audit work should add a real event list under `Auditoria`. Feedback review remains backed by `.NET` `/api/reporting/feedback` and should not be embedded back into audit unless functional audit rows exist and the combined view is clearly useful.

**Evidence:** Verified on 2026-05-20 with `pnpm.cmd --dir apps\manage-web test -- --run App.test.tsx` (`29 passed`).

## 2026-05-20 - Task 17.5 Functional Audit Read Model

**Context:** After feedback was separated from `Auditoria`, the audit workspace still did not show any functional activity. The system already persisted document lifecycle events in `app.audit_events`, but the management app had no API or frontend read model for those rows.

**Options Considered:** Keep the audit placeholder until a larger reporting task, show locally mocked events in the frontend, or expose a scoped `.NET` read endpoint over `app.audit_events`.

**Decision:** Add `.NET` `GET /api/audit/events` for `Admin` and `DocumentManager`, backed by `app.audit_events`, and update the management audit workspace to load, filter, and render those functional events. The local demo seed with `-WithSampleDocument` inserts a matching `document.created` row so the screen can show a visible audit event without using chat feedback as a substitute.

**Rationale:** Functional audit is already persisted by document lifecycle workflows and is part of the management service boundary. Exposing it through `.NET` keeps the management frontend on the approved same-origin API path and gives operators a real activity view immediately.

**Tradeoffs:** The first read model is intentionally narrow: it shows document lifecycle events already written to `app.audit_events`. User/group/budget mutations still need their own audit writes in a later backend hardening pass before those event types appear naturally.

**Consequences:** `Auditoria` should now remain a real functional event view and must not embed feedback reporting. Document create/update/review/archive/restore actions generate visible rows, and demo environments can seed one with `infra/compose/Seed-LocalDemoData.ps1 -WithSampleDocument`.

**Evidence:** Verified on 2026-05-20 with `dotnet test services\dotnet-api\AdvancedRag.sln --filter ManagementAudit` (`2 passed`), `pnpm.cmd --dir apps\manage-web test -- --run App.test.tsx` (`29 passed`), `pnpm.cmd --dir apps\manage-web typecheck`, `pnpm.cmd --dir apps\manage-web build`, and `dotnet test services\dotnet-api\AdvancedRag.sln --filter "ManagementAudit|Document"` (`24 passed`). `.NET` commands emitted the existing `NU1900` warnings because NuGet vulnerability metadata could not be fetched; tests passed.

## 2026-05-20 - Task 17.5 Management Usability Refinement

**Context:** User review found that the users/groups workspace did not expose enough editing capability, group data was not directly visible, users/audit search controls were oversized, audit event type filtering could overflow, feedback rows were too wide with request ID and citations, and document lifecycle states needed stronger visual differentiation.

**Options Considered:** Keep the current tables and rely on backend APIs only, add separate screens for each administration concern, or keep the existing management destinations while improving the dense table workflows in place.

**Decision:** Keep the existing management destinations and refine the in-place workflows. `Usuarios y grupos` now exposes user role/group editing, AI budget editing as a separate action, and a visible groups table with group rename support through `.NET` `PUT /api/groups/{id}`. Feedback review hides request ID and citations from the normal table but adds an Excel-compatible CSV export with all available reporting fields. Audit filters and management search controls are compact and bounded. Document lifecycle states use semantic color badges. Embedding chat directly inside `manage-web` is deferred until a management-preview contract is approved because direct manage-to-FastAPI calls would violate current service-boundary rules.

**Rationale:** These changes address the usability blockers without adding more navigation or weakening service boundaries. Group rename belongs to the .NET-owned users/groups contract. Feedback scanning benefits from fewer columns, while export preserves the full audit/reporting payload for offline review.

**Tradeoffs:** Feedback export is CSV-compatible with Excel rather than a native `.xlsx` file, avoiding a new frontend dependency during Task 17.5. User role and group updates still use the existing separate endpoints, so a partial failure can occur if one update succeeds and the next fails; this is acceptable for the MVP UI pass and can be replaced by a combined backend command later if needed. Deferring the management chat preview leaves one user-requested workflow incomplete, but avoids shipping an iframe or direct API call that would conflict with same-origin auth, Caddy routing, and backend ownership.

**Consequences:** Future feedback UI work should keep the normal table compact and put forensic fields in export/detail surfaces. Future users/groups work should preserve direct group visibility and avoid hiding group management inside user creation dialogs only.

**Evidence:** Verified on 2026-05-20 with `pnpm.cmd --dir apps\manage-web typecheck`, `pnpm.cmd --dir apps\manage-web test -- --run App.test.tsx` (`31 passed`), `dotnet test services\dotnet-api\AdvancedRag.sln --filter UserAdministration` (`8 matching tests passed`; existing NU1900 warnings), and `dotnet build services\dotnet-api\AdvancedRag.sln` (build passed; existing NU1900 warnings).

## 2026-05-20 - Document Domain Vocabulary Rename

**Context:** The product had already exposed `/api/documents` routes and Spanish `Documentos` UI, but the durable domain model, database schema, RAG schema columns, audit event types, tests, scripts, and project context still used the older `instruction` vocabulary. The user explicitly approved changing both the database and code so the domain is called `documents`.

**Options Considered:** Keep only external UI/API wording as documents, add compatibility views over old instruction tables, or perform a structural rename across app, RAG, code, scripts, tests, and context.

**Decision:** Perform the structural rename to `documents`. The effective schema now uses `app.documents`, `app.document_versions`, `app.document_permissions`, `app.document_tags`, `document_id`, `document_version_id`, and `document_type`. RAG-owned storage uses `document_id` and `document_version_id`. Audit events use `document.*`. Public API payload fields use `documentId`, `documentVersionId`, and `documentType`.

**Rationale:** A split vocabulary would create avoidable maintenance risk and make operators, implementers, and final documentation reason about two names for the same product object. The MVP is still early enough to correct the domain language directly.

**Tradeoffs:** The rename touches many files and requires compatibility migrations for existing local databases that already applied the earlier schema. The migration files necessarily reference the legacy `instruction_*` names as old database identifiers, but new runtime code and new schema objects use `document*`.

**Consequences:** Future product, database, API, RAG, frontend, and documentation work must use `documents` vocabulary. Any remaining `instruction_*` references should be limited to compatibility migration logic that renames old database objects forward or downgrades them.

**Evidence:** Verified on 2026-05-20 with `dotnet test services\dotnet-api\AdvancedRag.sln` (`70 passed`), `uv run pytest -q` (`32 passed`), `uv run ruff check .`, `uv run mypy src tests`, `pnpm.cmd -r test -- --run` (`50 frontend tests passed; E2E package intentionally skipped in recursive unit run`), `pnpm.cmd -r typecheck`, `pnpm.cmd -r build`, `docker compose --env-file infra/compose/.env.example -f infra/compose/compose.yaml config`, and `git diff --check`. `.NET` commands emitted the existing NU1900 warnings because NuGet vulnerability metadata could not be fetched; Vite emitted the known management bundle-size warning after TipTap.

## 2026-05-20 - App Table Grants Applied By EF Migrations

**Context:** After the documents vocabulary rename, local Compose startup failed with `postgres-init` exit 3. The init script ran before `.NET` EF migrations but attempted to grant `SELECT` on `app.document_permissions`, which may not exist yet in a clean database and may still be named `app.instruction_permissions` in an existing local database until the compatibility EF migration runs.

**Options Considered:** Ask operators to run manual `ALTER TABLE` statements, make `postgres-init` conditionally grant whichever legacy or new table exists, or move table-level grants to the `.NET` migration owner after table creation/rename.

**Decision:** Keep `postgres-init` responsible for database creation, extension setup, roles, schemas, passwords, schema ownership, and schema USAGE only. Apply the `rag_owner` table-level `SELECT` grants for `app.document_permissions` and `app.user_ai_budget_limits` from `.NET` EF migrations after those tables exist.

**Rationale:** `.NET` owns the `app` schema and knows when its tables have been created or renamed. Moving grants to EF removes the startup ordering bug without requiring manual database surgery or broad app-schema privileges.

**Tradeoffs:** The EF migration currently targets the approved Compose role name `rag_owner` and guards the grant when the role is absent, which keeps non-Compose tests safe. If deployments later rename the RAG database role, migration configuration must be revisited.

**Consequences:** `postgres-init` must not grant table-level access to `.NET`-owned tables. Future `.NET` migrations that add new app tables needed by FastAPI must include conditional grants after creating those tables.

**Evidence:** Verified on 2026-05-20 with `uv run pytest tests/test_migrations.py -q` (`3 passed`) and `dotnet test services\dotnet-api\tests\AdvancedRag.Infrastructure.Tests\AdvancedRag.Infrastructure.Tests.csproj --filter EfMigration_CreatesOnlyAppSchemaTables` (`1 passed`). `.NET` emitted existing NU1900 warnings because NuGet vulnerability metadata could not be fetched.

## 2026-05-20 - Post-Rename Reporting And Audit Compatibility

**Context:** After the structural rename from `instruction` to `documents`, the management feedback view did not load and the audit workspace still displayed legacy `instruction.*` events. Investigation found that the RAG rename migration recreated reporting views without restoring `app_reporting_reader` grants, and existing `.NET` audit rows retained old event/entity vocabulary.

**Options Considered:** Leave local data stale and ask operators to reset volumes, map legacy labels only in the UI/API, or add explicit compatibility migrations that repair permissions and persisted data.

**Decision:** Add post-rename compatibility migrations. FastAPI Alembic restores `SELECT` grants on `rag.v_query_audit_with_citations` and `rag.v_feedback_summary` to `app_reporting_reader` after the views are recreated. `.NET` EF migrates existing `app.audit_events` rows from `instruction.*`, `instruction`, and `instructionId` to `document.*`, `document`, and `documentId`.

**Rationale:** The product should upgrade existing local/customer data without requiring volume deletion or manual SQL. Fixing the data and grants at the migration layer keeps the frontend simple and preserves the documented service boundaries.

**Tradeoffs:** Compatibility migrations necessarily reference legacy `instruction` identifiers. Those references are acceptable only in migration code that upgrades or downgrades existing data.

**Consequences:** Future schema/view recreation migrations must reapply dependent grants in the same migration or a follow-up migration. Future domain-vocabulary changes must include persisted audit data as part of the migration checklist.

**Evidence:** Verified on 2026-05-20 with `dotnet test services\dotnet-api\AdvancedRag.sln` (`72 passed`; existing NU1900 warnings), `uv run pytest -q` (`32 passed`), `uv run ruff check .`, and `docker compose --env-file infra/compose/.env.example -f infra/compose/compose.yaml config`.

## 2026-05-22 - Embedding Dimension Upgrade Preserves Historical Citations

**Context:** The migration from `vector(1536)` to `vector(1024)` intentionally invalidates existing embeddings and requires reindexing. Local Compose startup failed when the migration attempted to cast existing `rag.document_chunks.embedding` values to `NULL` while the column still had the original `NOT NULL` constraint. Deleting old chunks was not acceptable because `rag.query_audit_citations.chunk_id` uses a restrictive foreign key to preserve historical citation audit evidence.

**Options Considered:** Delete old chunks before resizing, keep old 1536-dimensional values, backfill fake 1024-dimensional values, or keep historical chunks inactive with `embedding = NULL`.

**Decision:** Preserve historical chunk rows, mark them inactive, allow `rag.document_chunks.embedding` to be nullable for inactive historical rows, and require reindexing to create new active chunks with valid 1024-dimensional embeddings.

**Rationale:** This preserves audit/citation integrity while preventing retrieval from stale embeddings with incompatible dimensions and semantics.

**Tradeoffs:** The database no longer enforces non-null embeddings on all chunk rows. Runtime indexing still writes embeddings for active chunks, and retrieval already filters active chunks, so the weaker column constraint is limited to historical inactive data.

**Consequences:** Operators upgrading from the older 1536-dimensional schema must reindex before serving chat traffic. Future retrieval queries and indexes must tolerate inactive historical chunks with `embedding = NULL`.

**Evidence:** Verified on 2026-05-22 with `uv run pytest tests/test_migrations.py -q`, `uv run pytest -q`, `uv run ruff check .`, `uv run mypy src tests`, and `docker compose --env-file infra/compose/.env.example -f infra/compose/compose.yaml -f infra/compose/compose.override.yaml build rag-api`.

## 2026-05-22 - BM25 Extensions Installed By Postgres Init

**Context:** The BM25 migration adds `content_tsv` and trigram search over `rag.document_chunks`. Local full Compose startup failed because `rag-api` ran Alembic against an existing database where `public.unaccent` was not installed. The migration tests installed `unaccent` and `pg_trgm` in their bootstrap path, but the real Compose `postgres-init` script still installed only `vector`.

**Options Considered:** Require manual extension installation before upgrades, let the runtime `rag_owner` migration create extensions, or extend `postgres-init` to install all database extensions needed by the product.

**Decision:** Keep database extension installation in `postgres-init`. It now installs `vector`, `pg_trgm`, and `unaccent` idempotently before service migrations run. The BM25 migration uses an explicit `regdictionary` cast when wrapping `public.unaccent`.

**Rationale:** `postgres-init` already runs with administrative database credentials and is the correct Compose boundary for extension setup. Runtime service migrations should not depend on superuser privileges.

**Tradeoffs:** Non-Compose deployments must ensure the same extensions exist before running RAG migrations.

**Consequences:** Future RAG migrations that need database extensions must add them to `postgres-init` or document the external deployment prerequisite before using them in service-owned migrations.

**Evidence:** Verified on 2026-05-22 with `uv run pytest tests/test_migrations.py -q`, `uv run ruff check .`, `uv run mypy src tests`, and `docker compose --env-file infra/compose/.env.example -f infra/compose/compose.yaml -f infra/compose/compose.override.yaml up -d --build --force-recreate`.

## 2026-05-22 - Shared UI Wiring Uses Workspace Package Source

**Context:** `packages/shared-ui` was scaffolded as a pnpm workspace package, but the three SPAs still rendered their local shells. Simply importing the package would compile but not style correctly because the shared components use Tailwind utility classes and the apps were not yet running Tailwind through Vite or scanning the shared package source. Docker frontend builds also copied only each app directory, so workspace imports would fail inside image builds.

**Options Considered:** Keep the shared-ui package unused until the full SPA refactor, duplicate shell code in each app, prebuild shared-ui as a separate published artifact, or wire each SPA directly to the workspace package source.

**Decision:** Wire the existing SPA shells directly to `@helpcenter/shared-ui` source through the pnpm workspace. Each app imports shared fonts/tokens/globals, enables `@tailwindcss/vite`, and uses `@source "../../../packages/shared-ui/src"` in its local CSS. Each frontend Dockerfile copies `packages/shared-ui` before running `pnpm --dir <app> install --frozen-lockfile` and `pnpm --dir <app> build`.

**Rationale:** Direct workspace-source consumption is the smallest step that makes the design system real while preserving current app workflows. It avoids publishing/building an internal package before the component API has stabilized.

**Tradeoffs:** The apps now compile shared-ui source as part of their own builds, so React type versions and Tailwind source scanning must stay aligned across the workspace. The full per-SPA UX refactor remains separate a later UI refactor work.

**Consequences:** New shell-level UI should use `AppShell`, `Header`, `Sidebar`, and `DarkModeToggle` from `@helpcenter/shared-ui`. Frontend Dockerfiles must include workspace packages consumed by each app. The React type packages in the SPAs are aligned to React 18 to match the React 18 runtime and shared-ui peer dependency.

**Evidence:** Verified on 2026-05-22 with `pnpm --dir packages/shared-ui typecheck`, `pnpm --dir apps/manage-web typecheck`, `pnpm --dir apps/chat-web typecheck`, `pnpm --dir apps/docs-web typecheck`, all three app Vitest suites, all three app builds, all three app lint commands, `docker compose --env-file infra/compose/.env.example -f infra/compose/compose.yaml -f infra/compose/compose.override.yaml build manage-web chat-web docs-web`, `docker compose --env-file infra/compose/.env.example -f infra/compose/compose.yaml -f infra/compose/compose.override.yaml up -d --build --force-recreate manage-web chat-web docs-web`, `docker compose ... ps`, and HTTPS 200 checks for `manage.localhost`, `chat.localhost`, and `docs.localhost`.

## 2026-05-22 - Clean Compose Startup Seeds Default Admin And Pricing

**Context:** A clean database could create schemas automatically, but it still required either first-run setup or manual demo seeding before the user could log in, and RAG chat could fail because `rag.model_pricing` had no active rows for the configured default models.

**Options Considered:** Keep first-run setup only, use a local-only seed script, or seed minimal operational data through migrations.

**Decision:** Seed minimal operational defaults through migrations. `.NET` EF migrations create `admin@admin.com` with initial password `admin`, the `Admin` role assignment, and a default monthly AI budget. FastAPI Alembic migrations create active pricing rows for `gpt-4.1-nano` and `text-embedding-3-small`.

**Rationale:** The user's deployment model is controlled and prioritizes the fewest first-build steps. Migration-owned defaults make a fresh `docker compose up -d --build` usable without hidden SQL or a separate seed command.

**Tradeoffs:** A default administrator password is a security liability if a deployment is exposed before the password is changed. The operational documentation now explicitly requires changing it immediately after first login outside throwaway local testing.

**Consequences:** Clean databases have an administrator and baseline RAG pricing immediately after service migrations run. Operators still own secret creation, OpenAI key configuration, and post-login password rotation.

**Evidence:** Verified on 2026-05-22 with `dotnet test services\dotnet-api\tests\AdvancedRag.Infrastructure.Tests\AdvancedRag.Infrastructure.Tests.csproj --filter "EfMigration_SeedsDefaultAdminUser"`, `dotnet test services\dotnet-api\tests\AdvancedRag.Infrastructure.Tests\AdvancedRag.Infrastructure.Tests.csproj --filter "EfMigration"`, `uv run pytest tests/test_migrations.py -q`, `uv run ruff check .`, `docker compose --env-file infra/compose/.env.example -f infra/compose/compose.yaml -f infra/compose/compose.override.yaml config`, and `git diff --check`. Pricing values were taken from official OpenAI model/pricing pages visible on 2026-05-22: `gpt-4.1-nano` at USD 0.10 input, USD 0.025 cached input, USD 0.40 output per 1M tokens, and `text-embedding-3-small` at USD 0.02 per 1M tokens.

## 2026-05-22 - Local Startup Script Trusts Docker Caddy CA

**Context:** After deleting Compose volumes, Caddy regenerates its internal CA in the `caddy-data` volume. Browsers and Windows clients then reject `https://manage.localhost` until the new Docker-generated root certificate is trusted. Running `caddy trust` on the host is insufficient because it trusts a separate host Caddy instance, not the Compose container CA.

**Options Considered:** Keep manual copy/import steps in troubleshooting only, silently import the CA during Compose startup, or add an explicit local startup script with opt-in certificate trust.

**Decision:** Add `infra/compose/Start-Local.ps1`. The script verifies local secret files, runs Docker Compose with the local override, waits for Caddy, copies `/data/caddy/pki/authorities/local/root.crt` from the Caddy container, and imports it into `Cert:\CurrentUser\Root` only when `-TrustCaddyCertificate` is passed.

**Rationale:** This reduces first-run local setup to one command while keeping host trust-store mutation explicit and auditable.

**Tradeoffs:** It is Windows/PowerShell-oriented. Developers using other operating systems still need equivalent manual trust-store commands.

**Consequences:** The recommended local first-start command is `.\infra\compose\Start-Local.ps1 -TrustCaddyCertificate`. The command must be rerun after deleting `caddy-data`.

## 2026-05-22 - Task 17.5 Product Surface Session And UI Consolidation

**Context:** The first shared-ui wiring pass applied a global product header to all three SPAs and left several surfaces visually inconsistent. User review found that the shared top bar was inappropriate for independent web apps, chat could not ask questions because session bootstrap expired, `docs.localhost` still behaved like a narrow viewer instead of a document portal, dark mode left white cards/forms inside black pages, account self-service was missing, dialogs had redundant header close buttons, and the i18n setup lacked visible language controls.

**Options Considered:** Keep the shared header and polish it, split all three apps into completely custom shells, or keep shared tokens/components while giving each app its own workflow-local navigation and session bootstrap.

**Decision:** Keep `@helpcenter/shared-ui` as the shared token/component source, but remove the global `Header` from manage, chat, and docs product surfaces. Management uses a sidebar-only shell with session, theme, language, logout, and account controls in the sidebar. Chat and docs use local workflow headers. Chat validates the .NET session and renews a chat token before accepting questions. `docs.localhost` root is an authenticated document portal backed by a .NET viewer catalog endpoint, while exchange-code URLs continue to render the focused viewer. Language selectors are visible in all three apps. Dark mode must style workspace backgrounds, panels, cards, inputs, tables, dialogs, badges, and local headers through shared tokens.

**Rationale:** The three SPAs are separate product surfaces, not pages inside one website. A shared global top bar duplicated navigation and made chat/docs feel like unfinished management pages. Session bootstrap belongs near the workflow that needs it: chat must repair or request auth before asking, and docs needs a portal that reflects document access by role/group.

**Tradeoffs:** Some shell code remains app-specific instead of fully centralized. This is acceptable because the shell responsibilities differ across management, chat, and document portal/viewer workflows.

**Consequences:** Future shared-ui work should provide primitives and tokens, not force one global navigation frame onto every SPA. `docs.localhost` should expose a browsable catalog for authenticated users; admin and document managers can see management-scope documents, while viewers see only documents allowed by their groups and published state. Account self-service is owned by `.NET` account endpoints. UI language defaults to Spanish but can be changed from each app's visible selector. This 2026-05-22 note was superseded on 2026-05-26 when runtime cleanup removed chat-token renewal and viewer exchange-code flows.

**Evidence:** Verified on 2026-05-22 with `pnpm.cmd --dir apps\chat-web test -- --run App.test.tsx`, `pnpm.cmd --dir apps\docs-web test -- --run App.test.tsx`, `pnpm.cmd --dir apps\manage-web test -- --run App.test.tsx`, builds for `apps/chat-web`, `apps/docs-web`, and `apps/manage-web`, `dotnet test services\dotnet-api\tests\AdvancedRag.App.Tests\AdvancedRag.App.Tests.csproj --filter "UserAccount|ViewerDocumentCatalog"`, `dotnet test services\dotnet-api\tests\AdvancedRag.App.Tests\AdvancedRag.App.Tests.csproj --no-build`, `dotnet test services\dotnet-api\tests\AdvancedRag.Api.Tests\AdvancedRag.Api.Tests.csproj --no-build --filter "FullyQualifiedName~ViewerEndpointTests"`, and `dotnet build services\dotnet-api\AdvancedRag.sln --no-restore`. A broader API filter run for `Viewer|Auth|User` exposed existing duplicate-role seed failures in auth/rate-limit test fixtures; focused viewer/API and application tests pass.

## 2026-05-26 - Unified Session Removes Browser Token Flows

**Context:** The unified-session design requires `__Host-session` to become the only browser auth cookie across manage, chat, and docs. The previous Phase 1.5 state still exposed `.NET` `POST /api/auth/chat-token`, `.NET` `/api/viewer/exchange`, `__Host-chat-token`, `__Host-viewer-token`, and FastAPI accepted chat/feedback mutations without locally validating the shared CSRF cookie/header pair.

**Options Considered:** Keep viewer exchange codes as a defense-in-depth exception, replace them with a second docs-scoped JWT cookie, or complete the unified-session cleanup by using document-id locator links plus session revalidation.

**Decision:** Complete the unified-session cleanup for browser runtime. Remove the `.NET` chat-token endpoint and token issuer. Remove the viewer exchange endpoint and viewer-token service. Viewer links now use `https://docs.<domain>/open?documentId=<id>` and `GET /api/viewer/document?documentId=<id>` revalidates the current `.NET` session, role, document state, and permissions before returning content. FastAPI validates the `__Host-CSRF` cookie and `X-CSRF-Token` header locally before validating the `__Host-session` cookie through `.NET`.

**Rationale:** A document-id URL is only a locator, not authorization. It preserves the v2 one-session model while keeping authorization server-side. A second viewer JWT cookie would recreate the removed token flow under another name, and keeping exchange codes would leave the auth model only partially implemented.

**Tradeoffs:** Users must have an authenticated docs-host session before a document-id link can render content. The old exchange-code flow provided a short-lived cross-host access bridge; removing it simplifies auth but shifts link opening to normal session bootstrap/login behavior. A later 2026-05-26 decision removed the deprecated viewer exchange/audit tables from the current EF model and initial app-schema migration instead of keeping them as compatibility tables.

**Consequences:** Browser runtime must not call `/api/auth/chat-token` or `/api/viewer/exchange`. FastAPI browser mutations fail closed with `CSRF_TOKEN_INVALID` before session validation when the CSRF pair is missing or invalid. Future docs UX should treat `documentId` links as authenticated document locators and show login/access-denied/not-found states instead of expired-code/token states.

**Evidence:** Verified on 2026-05-26 without Playwright using `uv run pytest -q` (`39 passed`), `uv run ruff check .`, `uv run mypy src tests`, `dotnet test services\dotnet-api\tests\AdvancedRag.App.Tests\AdvancedRag.App.Tests.csproj --filter ViewerAccessServiceTests` (`4 passed`), `dotnet test services\dotnet-api\tests\AdvancedRag.Api.Tests\AdvancedRag.Api.Tests.csproj --filter "AuthEndpointTests|ViewerEndpointTests|RateLimitEndpointTests"` (`14 passed`), `dotnet build services\dotnet-api\AdvancedRag.sln --no-restore`, `pnpm.cmd --dir apps\chat-web test -- --run App.test.tsx`, `pnpm.cmd --dir apps\manage-web test -- --run App.test.tsx`, `pnpm.cmd --dir apps\docs-web test -- --run App.test.tsx`, app typechecks for chat/manage/docs, `pnpm.cmd --dir apps\docs-web build`, and `docker compose --env-file infra/compose/.env.example -f infra/compose/compose.yaml config`.

## 2026-05-26 - Deprecated Viewer Exchange Tables Removed From Current Schema

**Context:** After browser chat-token and viewer exchange flows were removed, the current runtime no longer read or wrote the old viewer exchange code or viewer token audit tables. The remaining open decision was whether to keep those tables as deprecated compatibility tables or remove them from the current migration path.

**Decision:** Remove the deprecated viewer exchange/audit tables from the current EF model, the initial app-schema migration, and the raw cleanup script list. Viewer document links remain session-authenticated `documentId` locators.

**Rationale:** Keeping unused credential-flow tables in the fresh schema preserves a discontinued auth design and creates misleading migration surface. Removing them makes the database match the unified-session runtime.

**Tradeoffs:** Existing databases that already applied older migrations may still contain those tables until a deliberate cleanup migration is introduced for that environment. The current local migration path and EF model no longer create or track them.

**Consequences:** Future viewer work must not add exchange-code or viewer-token persistence unless a new architecture decision reopens that model. Fresh app-schema migrations create only currently owned app tables.

**Evidence:** Verified on 2026-05-26 with `dotnet build services\dotnet-api\AdvancedRag.sln --no-restore`, `dotnet test services\dotnet-api\tests\AdvancedRag.App.Tests\AdvancedRag.App.Tests.csproj --filter ViewerAccessServiceTests --no-build` (`4 passed`), `dotnet test services\dotnet-api\tests\AdvancedRag.Infrastructure.Tests\AdvancedRag.Infrastructure.Tests.csproj --filter "AppDbContextMappingTests" --no-build` (`2 passed`), and `dotnet test services\dotnet-api\tests\AdvancedRag.Infrastructure.Tests\AdvancedRag.Infrastructure.Tests.csproj --filter "EfMigration_CreatesOnlyAppSchemaTables" --no-build` (`1 passed`). Initial parallel test runs failed with a transient file-lock on `AdvancedRag.App.dll`; the sequential reruns passed.

## 2026-05-27 - Frontend Runtime Locales Limited To Spanish And English

**Context:** User review found that the visible language selectors did not make the UI meaningfully switch languages and that Portuguese should not be part of the product surface.

**Decision:** Support only `es-AR` and `en-US` in runtime UI and RAG prompt loading. Spanish remains the frontend fallback/default locale. Portuguese resources, selector options, and prompt files are removed.

**Rationale:** The current MVP only needs Spanish and English. Keeping Portuguese in the selector creates a false product promise and adds translation maintenance without a requirement.

**Tradeoffs:** Some deeper feature screens still contain hardcoded Spanish literals from earlier MVP work. The locale infrastructure and shell-level behavior are now corrected; remaining literals should move into `src/i18n/` incrementally as each surface is touched.

**Consequences:** New frontend UI text must be added to both `es-AR` and `en-US` resources. RAG prompts must have Spanish and English variants. Visible language selectors expose only ES and EN. Portuguese must not be reintroduced without a new product decision.

**Evidence:** Verified on 2026-05-27 with focused App tests for manage, chat, and docs, focused RAG locale-support tests, app typechecks for all three SPAs, and app builds for all three SPAs.

## 2026-05-29 - Management Feedback Review Includes No-Feedback Queries

**Context:** User review found that the management Feedback workspace only showed chat queries that already had submitted feedback. That hid unanswered quality evidence: operators also need to inspect questions and answers that users did not rate.

**Decision:** The management feedback reporting endpoint continues to use `/api/reporting/feedback`, but it now returns all chat query audit rows exposed by `rag.v_query_audit_with_citations`. `feedbackValue`, `feedbackComment`, and `feedbackUpdatedAt` are nullable in the .NET and frontend contracts. The management UI renders rows without feedback as a clear no-feedback state and falls back to the query creation timestamp when no feedback timestamp exists.

**Rationale:** Keeping all chat questions in the same review workspace lets document managers audit coverage and answer quality without relying on users to submit thumbs feedback.

**Tradeoffs:** The route name still says `feedback`, even though it now behaves more like a chat question review report. Renaming it would be cleaner semantically but would require a wider contract migration; the current change preserves compatibility while fixing the product behavior.

**Consequences:** Negative-only filters still restrict results to explicit negative feedback rows. The unfiltered Feedback workspace is now a complete question/answer review list, not a feedback-only list. Future reporting copy and exports should treat feedback fields as optional.

**Evidence:** Verified on 2026-05-29 with `dotnet test services\dotnet-api\tests\AdvancedRag.Infrastructure.Tests\AdvancedRag.Infrastructure.Tests.csproj --filter NpgsqlFeedbackReportingServiceTests`, `dotnet test services\dotnet-api\tests\AdvancedRag.Api.Tests\AdvancedRag.Api.Tests.csproj --filter FeedbackReportingEndpointTests`, `pnpm.cmd --dir apps\manage-web test -- --run App.test.tsx`, `pnpm.cmd --dir apps\manage-web typecheck`, `pnpm.cmd --dir apps\manage-web build`, `dotnet build services\dotnet-api\AdvancedRag.sln --no-restore`, and `git diff --check`.

## 2026-05-31 - Role Acceptance Matrix Promotes Limited Viewer Management Access

**Context:** The user updated `pruebas.md` because a pure "Viewer cannot access manage" rule prevents Viewers from updating their own password/email and seeing their AI balance. The same acceptance matrix also requires DocumentManagers to inspect users and balances, manage groups, and assign users to groups without gaining full user administration or budget mutation rights.

**Decision:** Treat `pruebas.md` as the functional acceptance source for role behavior. Viewers can access a limited `manage.client.com` self-service surface for their own account, own AI balance, and safe read-only configuration. DocumentManagers can view all users and balances, create/edit groups, assign users to groups, and view audit/feedback/configuration read models, but cannot publish documents, create users, change user roles/status, or modify AI budget limits. The management sidebar must render only entries allowed by the current user's role; backend authorization remains mandatory for direct URL/API access.

**Rationale:** Account self-service and budget transparency are ordinary authenticated-user needs, not administrator-only workflows. Hiding role-inapplicable navigation improves UX, but API authorization must remain the enforcement layer.

**Tradeoffs:** The management app now needs a finer-grained role model than the earlier Admin/DocumentManager-only console. Existing endpoints and E2E tests must be split between self-service read models, DocumentManager read/group-management paths, and Admin-only mutations.

**Consequences:** Backend authorization, management UI navigation/action visibility, and E2E role matrix coverage must remain aligned with `pruebas.md`. Existing context entries that said Viewer has no management access are superseded by this decision.

**Evidence:** `pruebas.md` was expanded on 2026-05-31 with role-specific acceptance criteria and negative tests. Implementation was aligned on 2026-05-31 with .NET API authorization changes, management UI navigation/action visibility, and focused Playwright role-matrix coverage.

## 2026-05-31 - Obsolete Refactor Documentation Removed

**Context:** The user decided that the currently implemented product is the source of truth and requested removal of documentation for a separate refactor track before deciding how document images should work.

**Decision:** Remove obsolete refactor documentation and branch references from the repository. Keep implemented runtime behavior documented as current product behavior, not as a future-track plan.

**Rationale:** Parallel future-state documentation was creating ambiguity about what is actually true. The next image-handling decision should be made against the implemented system and authoritative context files only.

**Tradeoffs:** Historical rationale for abandoned future plans is intentionally removed from the active repository. If a similar idea is reconsidered later, it must be re-decided and documented from the current implementation state.

**Consequences:** `context/architecture.md`, `context/rag-spec.md`, `context/code-standards.md`, and `context/progress-tracker.md` should be treated as the current source of truth. The next image decision must update these files before implementation.

**Evidence:** Removed obsolete refactor documentation directories, obsolete refactor context files, the obsolete refactor handoff, and the obsolete refactor execution plan on 2026-05-31.

## 2026-05-31 - Document Images Will Use MinIO Object Storage

**Context:** The current product stores normalized document HTML in Postgres and the management editor supports TipTap images, but the active architecture did not yet define durable image storage, authorization, RAG indexing, or multimodal provider behavior. Persisting image bytes as base64 inside `app.document_versions.content_html` would bloat Postgres, complicate backups, make sanitization harder, and create inefficient OpenAI request payloads.

**Decision:** Document image bytes must not be persisted as base64 in Postgres. MinIO is the target object storage service for document image bytes in the Docker Compose deployment. Postgres stores image metadata, ownership, object keys, MIME type, size, hash, audit data, and stable application-controlled image URLs, but not the binary image content. Canonical HTML stores stable same-origin image URLs such as `/api/document-images/{imageId}/content`; it must not store raw MinIO URLs, raw AWS S3 URLs, expiring presigned URLs, arbitrary external URLs, or base64 data URLs. The first implementation slice covers secure upload, storage, sanitizer enforcement, and rendering. Query-time OpenAI multimodal image inputs are deferred to the second slice.

**Rationale:** MinIO provides an S3-compatible object store that fits the single-tenant Compose deployment model and keeps large binary assets out of relational document rows while preserving isolated customer storage. Keeping only metadata in Postgres lets document lifecycle, permissions, audit, and published-version immutability remain owned by the `.NET` document domain. Stable app URLs preserve authorization and make a later migration from MinIO to AWS S3 an implementation/configuration change rather than an HTML data migration. `.NET` uses `AWSSDK.S3` so the runtime client can target either MinIO through `ServiceURL` or AWS S3 through region-based configuration.

**Tradeoffs:** This adds another stateful service, secrets, backups, readiness checks, object lifecycle cleanup, and storage access policies. Postgres and MinIO backups must be coordinated because document HTML and image metadata are incomplete without the referenced objects.

**Consequences:** `.NET` owns document image upload, authorization, metadata, serving, and object lifecycle. FastAPI must not write image metadata or objects. The first slice rejects invalid HTML image sources with `DOCUMENT_IMAGE_SOURCE_INVALID`, supports PNG, JPEG/JPG, WEBP, and GIF uploads up to 5 MB, and keeps the MinIO bucket private. The next slice should add RAG image-reference indexing and then optional query-time multimodal OpenAI image inputs with strict caps, audit fields, and cache behavior.

**Evidence:** Checked MinIO documentation on 2026-05-31: MinIO is S3-compatible object storage, supports container deployment, file-based environment variables for container credentials, policy-based access control, healthcheck endpoints, object versioning, and SDK/presigned URL operations. Checked OpenAI developer documentation on 2026-05-31: image inputs can be supplied as URLs, base64 data URLs, or file IDs; image inputs are metered as tokens; and the Responses API is recommended for new multimodal work while Chat Completions remains supported.

**Implementation Evidence:** First-slice implementation was verified on 2026-05-31 with focused .NET lifecycle tests for image-source rejection, API endpoint tests for upload/content serving, configuration and health tests, EF mapping/migration tests for `app.document_images`, `dotnet build services\dotnet-api\AdvancedRag.sln --no-restore`, manage-web typecheck/tests/build, Compose config validation, and `git diff --check`.

## 2026-06-01 - Text-First RAG Image Indexing

**Context:** The first MinIO document image slice stores image bytes correctly and keeps canonical document HTML on stable app-controlled image URLs. The RAG service still consumed only plain text extracted from saved HTML, so accessible image descriptions risked being lost during indexing even when editors supplied useful `alt` text or captions.

**Decision:** Keep the RAG image update text-first. FastAPI chunking indexes accessible image text from saved HTML, using `alt`, then `aria-label`, then `title`, and indexes nearby captions as ordinary chunk text. FastAPI must not index image URLs, fetch MinIO/S3 object bytes, write document image metadata, or send image inputs to OpenAI in this slice.

**Rationale:** This gives immediate RAG value for documents that include meaningful image descriptions while preserving the existing service boundary: `.NET` owns image storage, authorization, metadata, and serving. It also avoids adding multimodal provider cost, cache semantics, audit fields, byte-size caps, and image authorization rules before those are explicitly designed.

**Tradeoffs:** The chat can only reason over images whose authors provide useful textual descriptions or captions. It cannot inspect visual content directly yet, so diagrams or screenshots without meaningful `alt` text remain effectively invisible to retrieval.

**Consequences:** Editors and document managers should provide descriptive image `alt` text when the image carries business meaning. A later multimodal slice must make a separate decision for provider API choice, max images per query, max bytes, image detail level, cache behavior, and audit fields before fetching image bytes or sending image inputs.

**Evidence:** Verified on 2026-06-01 from `services/rag-api` with `uv run pytest tests/test_chunking.py tests/test_indexing.py -q`, `uv run ruff check .`, `uv run pytest -q`, and `uv run mypy src tests`. Repository `git diff --check` returned only line-ending warnings.

## 2026-06-01 - Query-Time Multimodal RAG

**Context:** The user approved changing the next image strategy from text-only image descriptions to query-time multimodal RAG. The prior text-first slice remains useful as a retrieval signal, but it cannot answer visual questions when evidence exists only in image pixels.

**Options Considered:** Query-time multimodal RAG after textual retrieval, offline visual caption generation during publication, or a hybrid of offline captions plus query-time image inputs.

**Decision:** Implement query-time multimodal RAG first. The service keeps text retrieval as the primary filter, stores FastAPI-owned `chunk -> image_id` references during indexing, selects only images associated with final retrieved chunks, fetches authorized bytes through a `.NET` internal endpoint, and sends a capped set of images to OpenAI through the Responses API.

**Rationale:** This gives direct visual reasoning while preserving the current retrieval, access, and storage boundaries. It avoids sending every document image to OpenAI and avoids committing to offline visual-caption persistence before quality and cost are measured.

**Tradeoffs:** Chat latency and provider cost increase for multimodal requests. The first slice also requires a new RAG table, a `.NET` internal endpoint, a Responses API provider path, additional audit fields, and stricter readiness/pricing checks. Multimodal answers are not cached initially, so repeated visual questions may cost more until cache semantics include image evidence identity.

**Consequences:** `.NET` remains the owner of image metadata, authorization, object storage access, and public serving. FastAPI may store `chunk -> image_id` references in the `rag` schema but must not write `app.document_images`, expose object keys, or read MinIO directly. Initial limits are 3 images, 5 MB total image bytes, and `detail: "low"` per chat request. Multimodal generation uses OpenAI Responses API with `store: false` and in-memory base64 data URLs.

**Evidence:** OpenAI documentation checked on 2026-06-01 states that the Responses API supports text and image inputs, that image inputs may be provided as URLs, base64 data URLs, or file IDs, that images count as tokens, and that Responses is recommended for new projects while Chat Completions remains supported.

## 2026-06-01 - Demo Host Caddy Domain Uses PUBLIC_DOMAIN

**Context:** The Compose Caddyfile still hardcoded `manage.localhost`, `chat.localhost`, and `docs.localhost`. That is acceptable for same-machine local development, but it prevents a Raspberry Pi demo host from being reached correctly from another computer because `.localhost` resolves to the client machine, not the Raspberry Pi.

**Decision:** Caddy site labels now use `manage.{$PUBLIC_DOMAIN}`, `chat.{$PUBLIC_DOMAIN}`, and `docs.{$PUBLIC_DOMAIN}`. The default `.env.example` value remains `PUBLIC_DOMAIN=localhost` for local development, while a demo host can set `PUBLIC_DOMAIN` to a LAN DNS name, a split-horizon domain, or a real domain pointed at the host.

**Rationale:** This preserves the same-origin host topology and `__Host-` cookie model while allowing the same Compose file to serve local workstation development and a separate demo host.

**Tradeoffs:** A LAN demo now needs explicit hostname resolution from the client machine, such as router DNS, `/etc/hosts`, Windows `hosts`, or a real domain. Direct IP-only access is not appropriate for the current multi-host Caddy routing and cookie model.

**Consequences:** Raspberry Pi demos should use hostnames such as `manage.rag-demo.lan`, `chat.rag-demo.lan`, and `docs.rag-demo.lan` or an equivalent domain. Operators must set `PUBLIC_DOMAIN` accordingly before starting Compose.

**Evidence:** Updated `infra/compose/Caddyfile` on 2026-06-01 to consume `PUBLIC_DOMAIN` in all three frontend site labels.

## 2026-06-02 - Raspberry Pi Demo Uses breuerai.com

**Context:** The Raspberry Pi demo stack is working on the LAN with `PUBLIC_DOMAIN=ragpi.lan`. External demo access needs stable public hostnames without exposing router ports directly.

**Decision:** Use the `breuerai.com` domain for the Raspberry Pi demo deployment. Public hostnames should follow the existing same-origin topology: `manage.breuerai.com`, `chat.breuerai.com`, and `docs.breuerai.com`. Cloudflare Tunnel is the preferred exposure mechanism for the demo host.

**Rationale:** A real domain keeps the MVP's host-based routing and host-only cookie model intact while avoiding IP-based access. Cloudflare Tunnel avoids inbound port forwarding and gives public TLS at the edge for a demo-grade deployment.

**Tradeoffs:** The demo now depends on Cloudflare DNS/Tunnel availability and a running `cloudflared` connector on the Raspberry Pi. Caddy still routes by host internally, so all three public hostnames must be configured consistently.

**Consequences:** The Raspberry Pi `.env` should set `PUBLIC_DOMAIN=breuerai.com`. Cloudflare should route `manage.breuerai.com`, `chat.breuerai.com`, and `docs.breuerai.com` to the local Caddy origin.

**Evidence:** User confirmed ownership of `breuerai.com` on 2026-06-02.

## 2026-06-03 - Interim Manual Source-Based Service Updates

**Context:** The user wants a practical way to update the Raspberry Pi/local-server deployment while the GHCR-based CI/CD flow is still being investigated and not yet integrated. The current authoritative deployment model remains Docker Compose with local secret files and persistent Postgres/MinIO volumes.

**Options Considered:** Keep running ad hoc `git pull` and `docker compose up -d --build` commands, implement the full GHCR deployment workflow immediately, or add an interim manual helper script.

**Decision:** Add a repository-root `updateService.sh` helper for interim manual source-based updates on the Linux deployment host.

**Rationale:** A scripted flow reduces operator mistakes during the temporary manual phase. It preserves local secrets and volumes, creates a Postgres backup before code updates when the stack is already running, refuses tracked local changes, uses `git pull --ff-only`, validates Compose, rebuilds/recreates services, waits for health checks, and prints relevant logs.

**Tradeoffs:** The deployment host still compiles `.NET`, FastAPI, and frontend images locally, which is slower and less reproducible than GHCR-published images. Automatic rollback is intentionally not included because database migrations may have already changed schema.

**Consequences:** Operators can run `./updateService.sh --env-file infra/compose/.env.pi` on the demo host until GHCR/CD replaces this path. The helper must not delete volumes, reset local Git state, or manage real secret values.

**Evidence:** Added `updateService.sh`, `docs/operations/manual-service-update.md`, and `tests/operations/test_update_service_script.py`. Verified on 2026-06-03 with `python -m unittest tests.operations.test_update_service_script`, which checks required safety steps and Bash syntax.

## 2026-06-03 - Demo Host systemd Autostart

**Context:** The Raspberry Pi/local-server demo should recover after a host shutdown or reboot without requiring an operator to run Docker Compose manually.

**Options Considered:** Rely only on Docker container restart policies, add a cron `@reboot` command, or install a `systemd` unit that runs Docker Compose after Docker and the network are available.

**Decision:** Add a repository-root `installServiceAutostart.sh` helper that installs `advanced-rag.service` as a systemd oneshot unit. The unit runs `docker compose up -d` with the selected Compose environment file after `docker.service` and `network-online.target`.

**Rationale:** systemd is the correct host-level boot coordinator on Ubuntu Server. It records status and logs, starts after Docker, can be enabled or disabled cleanly, and avoids ad hoc shell startup behavior. `docker compose up -d` preserves Compose dependency and health-check semantics better than relying only on daemon restart of old containers.

**Tradeoffs:** The unit is not fine-grained per-container monitoring because Docker Compose exits after starting containers. Container-level crash recovery remains a separate decision, such as Compose restart policies, if needed later.

**Consequences:** Operators can run `./installServiceAutostart.sh --env-file infra/compose/.env.pi` on the Linux demo host. The script writes `/etc/systemd/system/advanced-rag.service`, runs `systemctl daemon-reload`, enables the unit for boot, and starts it immediately. It does not store secret values or modify Compose secrets.

**Evidence:** Added `installServiceAutostart.sh`, `docs/operations/systemd-autostart.md`, and `tests/operations/test_install_service_autostart_script.py`. Verified on 2026-06-03 with `python -m unittest tests.operations.test_install_service_autostart_script`, which checks required systemd/Compose steps and Bash syntax.

## 2026-06-03 - Generic Internal Services Demo Corpus

**Context:** The product needs a realistic demo document library that can validate management workflows, group-based document access, RAG retrieval, citations, feedback, and no-answer behavior. The current local demo seed creates only one group and an optional minimal draft document, which is not enough to exercise the chat or permission model.

**Decision:** Design the demo corpus as a generic internal services company rather than a specialized industry corpus or a meta corpus about the Advanced RAG product itself.

**Rationale:** A generic internal services company is immediately understandable to most prospects and supports clear document groups such as HR, IT, Finance, Security, Operations, Sales Support, and Leadership. It also lets the demo include policies, procedures, checklists, troubleshooting guides, and escalation paths without needing domain-specific regulatory accuracy.

**Tradeoffs:** The corpus will be less differentiated than an industry-specific demo, but it is safer for an MVP because the questions and expected answers can stay broadly credible and easier to verify.

**Consequences:** The demo design must separate access groups from document categories. Groups are authorization boundaries; document types, audiences, and tags are classification metadata. The final corpus should include published documents, at least one restricted document, and a small set of draft or in-review records so the management lifecycle remains visible without leaking unpublished content into public chat.

## 2026-06-03 - Secure Cross-Subdomain Viewer SSO Handoff

**Context:** The management and chat apps create links to `docs.*` document viewer URLs. The current browser auth cookie is `__Host-session`, which is intentionally host-only. A session created on `manage.*` or `chat.*` therefore does not automatically authenticate the user on `docs.*`. This preserves the host-only cookie model but creates a poor user experience when a user follows a viewer link and appears logged out.

**Options Considered:** Keep the current behavior and require a separate manual login on `docs.*`, introduce a secure cross-subdomain handoff that mints a host-only docs session after server-side validation, or weaken the cookie topology by using a shared parent-domain session cookie.

**Decision:** Design a secure cross-subdomain viewer SSO handoff. Do not switch to parent-domain cookies. The handoff should let `docs.*` establish its own host-only `__Host-session` after validating a short-lived, server-owned handoff flow initiated by an authenticated source host. On 2026-06-04, the user approved DB-backed one-time handoff codes with expiration and consumed/used state.

**Rationale:** This preserves the security benefits of host-only cookies while removing the confusing "lost login" experience when users open document links. It also keeps document viewer access authorization server-side: the link remains a locator, not an authorization grant.

**Tradeoffs:** The implementation is more complex than a parent-domain cookie. It needs a short-lived handoff artifact, replay protection or narrow validity, audit/logging, CSRF-safe initiation, safe error states, and tests across management, chat, docs, and the .NET API.

**Consequences:** The existing `__Host-session` rule remains intact. Viewer link generation and docs app bootstrapping need a new handoff path before claiming login is shared across product surfaces. Implementation should add persisted one-time handoff state, safe replay failure handling, and audit/logging around handoff issuance and consumption.

**Implementation Evidence:** Implemented on 2026-06-04 with `.NET` app-layer `ViewerAccessService` handoff issuance/consumption, EF-owned `app.viewer_session_handoff_codes`, `POST /api/viewer/session-handoff`, docs-web handoff bootstrap, and URL cleanup through `history.replaceState`. Verified with focused viewer service tests, viewer API endpoint tests, EF mapping/migration tests, docs-web App tests, docs-web typecheck/build, and .NET solution build.

## 2026-06-03 - Deferred MVP Document Tags

**Context:** The database schema includes `app.document_tags` and older documentation mentions optional tags, but the current API contracts, document lifecycle service, management UI, viewer UI, and RAG retrieval flow do not expose or use tags. Implementing tags now would add backend and frontend scope without improving access control or RAG behavior in the MVP.

**Options Considered:** Implement optional editorial tags now, remove tags entirely from the model and documentation, or defer tags in product documentation while leaving the unused schema table in place.

**Decision:** Defer document tags for the MVP. Documentation should not describe tags as an available product capability. The existing unused table can remain as dormant schema history until a future cleanup or product decision.

**Rationale:** Groups are the current authorization boundary and document type/audience already cover the MVP's visible classification needs. Adding tag UI/API now would widen the document workflow without a clear operational payoff.

**Tradeoffs:** The schema contains an unused table for now. This is acceptable because removing it would require migration churn and does not improve the user-facing fixes currently needed.

**Consequences:** The document editor fixes should not add tag UI. Authoritative context and user-facing documentation should describe tags as deferred or remove active-tag wording.

**Implementation Evidence:** On 2026-06-04, active context was updated so document tags are described as deferred/out of scope for the MVP product surface. The document editor fixes did not add tag UI, API fields, lifecycle behavior, filters, or RAG behavior.

## 2026-06-03 - Assisted Import Moves From Plain Text To Safer Structured HTML

**Context:** PDF/DOCX import currently returns plain text only. DOCX extraction uses OpenXML `Body.InnerText`; PDF extraction concatenates PdfPig `page.Text`; the management frontend wraps the resulting text in paragraphs. This is safe but loses headings, lists, tables, and paragraph structure, making imports uncomfortable to edit.

**Options Considered:** Keep plain-text assisted extraction and improve only copy, use structured DOCX-to-HTML conversion plus conservative PDF layout extraction, or attempt full high-fidelity HTML conversion for both DOCX and PDF.

**Decision:** Improve imports using structured DOCX-to-HTML conversion and conservative PDF formatting. DOCX may produce sanitized semantic HTML for common headings, paragraphs, lists, links, emphasis, and tables. PDF should improve reading order and paragraph grouping where reliable, but must not promise faithful HTML reconstruction.

**Rationale:** DOCX stores semantic document structure, so a converter such as Mammoth can preserve useful HTML shape. PDF is primarily a presentation format and often lacks semantic structure, so aggressive conversion would produce misleading or brittle HTML. Imports remain assisted drafts: the user still reviews and edits before saving.

**Tradeoffs:** Adding DOCX-to-HTML conversion introduces a new dependency and requires security controls. PDF results remain imperfect, especially for scanned, multi-column, or heavily designed PDFs.

**Consequences:** Import responses should carry HTML content rather than only raw text, or carry both text and HTML during a compatibility transition. Converted HTML must be sanitized server-side before returning to the browser and again on save. On 2026-06-04, the user approved using Mammoth for DOCX-to-HTML conversion, with final NuGet version/license validation during implementation, and approved not importing embedded DOCX images in this slice.

**Evidence:** Tiptap docs checked on 2026-06-03 show `StarterKit` includes headings, lists, blockquote, code block, horizontal rule, and undo/redo. Mammoth for .NET documentation states it converts DOCX to simple semantic HTML including headings, lists, tables, images, emphasis, links, and line breaks, but performs no sanitization and warns about untrusted input. PdfPig documentation checked on 2026-06-03 warns against using `page.Text` directly for extraction and recommends text/layout analysis tools such as `ContentOrderTextExtractor`, `NearestNeighbourWordExtractor`, and page segmenters. On 2026-06-04, NuGet validation pinned Mammoth `1.11.0` for the .NET import slice.

**Implementation Evidence:** Implemented on 2026-06-04 with backend import responses returning `contentHtml` plus fallback `text`, Mammoth DOCX conversion sanitized through `HtmlSanitizer`, embedded DOCX image output removed/skipped, PDF paragraph HTML generation through PdfPig reading-order extraction, and manage-web insertion of `contentHtml` when present. Verified with focused import extraction tests, manage-web App tests, manage-web typecheck/build, and .NET solution build.

## 2026-06-04 - TipTap Text Color Uses Narrow Sanitized CSS

**Context:** The management document editor needed a text-color selector in the TipTap toolbar. TipTap's official color support emits inline `style="color: ..."` on text-style spans, while the project must continue rejecting arbitrary inline styling in saved document HTML.

**Options Considered:** Skip text color, add a custom semantic color mark/class, or use TipTap's official `Color` and `TextStyle` extensions with a tightly configured backend sanitizer.

**Decision:** Use TipTap `@tiptap/extension-color` with `@tiptap/extension-text-style` for the toolbar color selector. Persist only the `color` CSS property through the `.NET` `Ganss.Xss` document sanitizer and strip every other inline CSS property.

**Rationale:** This follows the editor's native extension model and keeps the stored HTML compatible with TipTap without broadening the document sanitizer to arbitrary styles.

**Tradeoffs:** Stored HTML now permits one inline CSS property. That increases sanitizer responsibility, so the allowed CSS surface must stay explicit and covered by tests.

**Consequences:** Future rich-text formatting that requires inline styles must go through the same decision process. Do not add broad style support to solve editor formatting issues.

**Evidence:** Tiptap documentation checked on 2026-06-04 confirms `Color` with `TextStyle` and `setColor`/`unsetColor` commands. The npm registry check on 2026-06-04 reported `@tiptap/react` latest as `3.25.0`, so the management TipTap packages were refreshed from `3.23.5` to `3.25.0`. Implementation uses `@tiptap/extension-color` and `@tiptap/extension-text-style`, a native toolbar color input with a clear-color action, and a sanitizer regression test proving safe color survives while unsafe CSS properties are stripped. After Docker/Compose exposed a frozen-lockfile mismatch, the `apps/manage-web` pnpm lockfile importer was regenerated so every TipTap specifier matches the exact `3.25.0` manifest entries. Verification passed with focused manage-web tests, `pnpm.cmd --dir apps\manage-web install --frozen-lockfile`, `pnpm.cmd --dir apps\manage-web typecheck`, `pnpm.cmd --dir apps\manage-web build`, focused .NET sanitizer test, and `.NET` solution build.

## 2026-06-04 - TipTap Highlight And Alignment Stay Narrowly Sanitized

**Context:** User review of the management document editor toolbar requested a cleaner heading control, a highlighter, and text-alignment controls. TipTap `TextAlign` emits inline `style="text-align: ..."` on paragraphs/headings, while the project still needs to reject arbitrary inline styling in stored document HTML.

**Options Considered:** Keep the toolbar limited to text color, add alignment with broad inline-style preservation, use custom CSS classes for alignment/highlight, or use TipTap's official `Highlight` and `TextAlign` extensions with narrowly configured sanitization.

**Decision:** Use TipTap `@tiptap/extension-highlight` for semantic `<mark>` highlights and `@tiptap/extension-text-align` for paragraph/heading alignment. Persist only `color` and `text-align` CSS properties through the `.NET` `Ganss.Xss` document sanitizer, preserve `<mark>`, and continue stripping every other inline CSS property.

**Rationale:** This follows TipTap's official extension model while keeping saved HTML predictable for management editing and docs rendering. Avoiding multicolor highlights prevents the MVP from allowing `background-color` or wider style support.

**Tradeoffs:** Alignment now adds a second approved inline CSS property. Any future rich-text control that needs styles must still be reviewed and tested explicitly.

**Consequences:** The toolbar uses fixed `P`, `H1`, `H2`, and `H3` buttons instead of a narrow heading select, plus icon buttons for alignment and highlight. Docs rendering styles `<mark>` consistently, but highlight colors are intentionally fixed for now.

**Evidence:** Tiptap documentation checked on 2026-06-04 confirms `Highlight`, `TextAlign.configure({ types: ["heading", "paragraph"] })`, `toggleHighlight`, and `setTextAlign` commands. Implementation pins `@tiptap/extension-highlight` and `@tiptap/extension-text-align` at `3.25.0` and adds a sanitizer regression test for `color`, `text-align`, `<mark>`, and unsafe CSS removal.

## 2026-06-04 - Cross-App Document Links Open In New Tabs

**Context:** Management and chat can hand off authenticated document-viewer sessions to `docs-web`. These links leave the current app context, so replacing the current page makes it harder to keep the management or chat workflow open while reviewing evidence.

**Options Considered:** Continue navigating the current tab, add explicit copy-link controls, or open cross-app document handoffs in a new browser tab.

**Decision:** Open manage-to-docs and chat-to-docs viewer handoff links in a new tab using `window.open(url, "_blank", "noopener,noreferrer")`.

**Rationale:** The current app remains available while `docs-web` shows the document, and the `noopener,noreferrer` feature string avoids giving the new tab access to the opener.

**Tradeoffs:** The handoff URL is created after an API request, so real-browser acceptance should confirm the browser does not block the popup in the deployed flow.

**Evidence:** Implemented on 2026-06-04 in manage-web document viewer links and chat-web citation links. Verified with focused App tests for both apps, full manage-web and chat-web App test files, typechecks, production builds, and `git diff --check` with line-ending warnings only.

## 2026-06-04 - Persisted Chat Sessions And Bounded Conversation Memory

**Context:** User review found that chat conversations were stored only in frontend React state, so the left conversation rail disappeared after page reload. The chat also behaved as independent one-shot questions, which made follow-up questions brittle and prevented a conversational product experience.

**Options Considered:** Keep chat history local-only, store a separate `chat_sessions` table now, use OpenAI-hosted conversation state, or reuse FastAPI-owned RAG query audit rows as the persisted source of chat turns.

**Decision:** Use `rag.query_audit_events` as the MVP source for persisted chat sessions. The chat frontend creates and sends a stable `sessionId`; FastAPI stores it on each audit row, exposes user-scoped read-only session list/history endpoints, and performs bounded same-user question condensation from prior audited turns in that `session_id`. The original question remains in `question`, and the standalone retrieval query is stored in `rewritten_question`.

**Rationale:** RAG query audit already owns chat questions, answers, citations, feedback, cost, cache state, and request evidence. Reusing it keeps the Slice A data model small, makes reloadable history align with audit, and avoids adding a second source of truth before the MVP proves the conversation workflow.

**Tradeoffs:** A dedicated chat-session table would support richer titles, archival, pinning, and empty draft sessions more cleanly. Those features are deferred. Audit-backed history also means a new conversation appears in the server list only after the first successful audited answer.

**Consequences:** Conversation memory is strictly scoped to the same authenticated user and explicit `session_id`; cross-user memory, cross-session memory, and provider-hosted conversation state remain out of scope. Retrieval and cache use `rewritten_question` when available, while transcript UI and reporting show the original user wording.

**Evidence:** Implemented on 2026-06-04 with FastAPI session list/history endpoints, bounded `conversation_memory` integration in `ChatService`, `rewritten_question`/`previous_event_id` audit persistence, and transcript-oriented `chat-web` state. Verification passed with focused RAG chat/locale tests, FastAPI ruff, chat-web typecheck, chat-web App tests, chat-web production build, and `git diff --check` with line-ending warnings only.

## 2026-06-05 - Chat UI Simplifies To Left Drawer Layout

**Context:** User review found the current chat UI too visually complex. The approved direction is closer to ChatGPT, but with a management-style fixed drawer on the left instead of a right-side history rail.

**Options Considered:** Keep the previous three-pane layout with left history and right citations, move history to the right, hide history in a drawer, or use a fixed left drawer aligned with the management app.

**Decision:** Redesign `chat-web` around a fixed left drawer. The drawer owns conversation history, the new-conversation action, language selection, dark-mode toggle, active-session user context, and logout. The main area focuses on the active transcript, answer-level citation summary buttons, usage, feedback, error states, and the bottom composer. Citation cards live in a collapsible right drawer opened from each answer instead of rendering inline below the answer.

**Rationale:** This reduces visual noise, keeps the product aligned with the management app's navigation model, and preserves fast access to conversation history without adding a third panel.

**Tradeoffs:** Citations are one interaction away instead of always visible below each answer, but this keeps the transcript cleaner and avoids repeated source cards dominating short answers.

**Consequences:** `chat-web` owns this shell locally rather than using the shared `AppShell`, because the shared sidebar behavior hides sidebars at smaller breakpoints and does not match the approved fixed chat drawer. Cache-hit citation reconstruction and frontend rendering should deduplicate citations by document/version before showing them in the drawer.

**Evidence:** Updated on 2026-06-05 with chat UI polish for right-drawer citations, fixed viewport drawers, deselectable feedback buttons, single-line autosizing composer, chat login parity with management auth controls, cache-hit citation deduplication, and mojibake fixes. Verification passed with focused `chat-web`, shared-ui, and RAG tests, chat/shared typechecks, chat production build, and `git diff --check` with Windows line-ending warnings only.

## 2026-06-05 - Cross-App Product Links Use One-Time Session Handoff

**Context:** The management sidebar needs direct links to Chat and Docs. Users should not pass through the target login page when they already have an active Manage session, but the MVP still requires host-only `__Host-session` cookies and must avoid broad parent-domain cookies or credential-bearing URLs.

**Options Considered:** Share cookies with `Domain=.client.com`, pass the session token in the URL, use browser storage, or issue a short-lived one-time handoff code that the target host consumes through same-origin `.NET`.

**Decision:** Use a separate general session handoff flow for product-surface navigation. Manage creates a one-time code scoped to `chat` or `docs`; the target app consumes it through `POST /api/auth/session-handoffs/consume`, receives its own host-only session cookie, removes the handoff code from the URL, and then loads normally.

**Rationale:** This preserves the current host-only cookie security model, avoids persistent browser-accessible credentials, and matches the existing document-viewer handoff pattern without mixing document authorization state into root product navigation.

**Tradeoffs:** Navigation requires an API request before opening the target app, and the code is briefly visible in the target tab URL until consumption. The code is short-lived, one-time, target-scoped, and not the main session token.

**Consequences:** Root links from Manage to Chat/Docs open in a new tab with `noopener,noreferrer`. Chat and Docs must attempt handoff consumption on boot before showing login when a `handoff` query parameter is present.

## 2026-06-10 - Docs Host Gains Document-Scoped Mini Chat Routed Directly To FastAPI

**Context:** The docs-web redesign adds an ephemeral mini chat that answers questions strictly from the open document. docs-web previously called only the .NET API; the RAG pipeline lives in FastAPI.

**Options Considered:** Route doc-chat paths on the docs host directly to FastAPI through Caddy (same pattern as `chat.*`), or keep "docs -> .NET only" and make .NET an SSE pass-through proxy to FastAPI.

**Decision:** Caddy routes `docs.<domain>/api/chat` and `/api/feedback*` to FastAPI. `POST /api/chat` accepts an optional `documentId` that scopes retrieval to one document. Doc-scoped requests force the `published` corpus, bypass the semantic cache (read and write), persist `scope_document_id` on `rag.query_audit_events`, and are excluded from the chat session list so chat-web's drawer never shows ephemeral doc-chat turns. Spec: `docs/superpowers/specs/2026-06-10-docs-web-redesign-and-doc-chat-design.md`.

**Rationale:** FastAPI already validates the `__Host-session` cookie against .NET on every request and owns budget, audit, and streaming; reusing the chat-host pattern adds no new auth mechanism. A .NET streaming proxy would duplicate error surface in the backend that by design never touches RAG.

**Tradeoffs:** The routing invariant relaxes from "docs -> .NET only" to "docs -> .NET, plus FastAPI for doc-chat/feedback only". Cache bypass forgoes reuse for repeated per-document questions, in exchange for provable scope correctness without re-keying the cache.

**Consequences:** The branch-aware access predicate still applies inside retrieval SQL, so inaccessible documents yield zero chunks and a generic no-info answer (no existence leak). The mini chat renders only on Published documents. `architecture.md` and `ui-context.md` must be updated when the implementation lands.

## 2026-06-11 - Chat Citations Use A Non-Modal Inline Right Panel And Feedback Moves Per Answer

**Context:** `ui-context.md` specifies that `Ver citas (n)` opens a collapsible right drawer fixed to the viewport height while the transcript scrolls independently. chat-web instead used the shared `CitationDrawer`, built on the Radix modal `Dialog`, which renders a blocking dark overlay. Separately, one global feedback form at the bottom of the workspace applied only to the latest answer, while the spec assigns feedback to each assistant turn.

**Options Considered:** Make the shared `Drawer` non-modal (changes semantics for every consumer), portal a floating fixed panel (overlaps the transcript on wide screens), or render a chat-local non-modal panel as a third column of the chat layout grid.

**Decision:** chat-web renders a workflow-local `CitationsPanel` (`role="dialog"`, non-modal, Esc and button close) as a third grid column (`286px / 1fr / 340px`); below 1100px it floats as a fixed right overlay without backdrop. The shared `CitationDrawer`/`Drawer` stay modal for other use cases. Feedback becomes per assistant turn: icon thumb toggles plus an inline comment/submit expansion, with drafts keyed by turn id and submitted values persisted on the turn. Shared `ChatMessage` gains an optional `showAuthor` prop (default `true`) so transcript layouts can drop the per-message author badge.

**Rationale:** A layout column is the only shape that satisfies "transcript scrolls independently while the panel stays fixed" without covering content or blocking interaction. Per-turn feedback keeps feedback attached to the answer that produced it, as the spec and the FastAPI data model (`queryAuditEventId` per turn) already assume.

**Tradeoffs:** chat-web now owns a local panel component instead of reusing the shared modal drawer; the shared `CitationDrawer` keeps no chat consumer. Per-turn drafts add a small client-state map keyed by turn id.

**Consequences:** Dead render branches tied to the old single-answer state (`answer`, `usage`, `cacheHit`, global feedback state) were removed; the transcript auto-scrolls while streaming when the user is pinned to the bottom; the chat empty state, composer, sidebar, citations, and feedback strings moved to the existing i18n resources (es-AR values preserved verbatim for test stability).

## 2026-06-11 - RAG Hardening Roadmap: Lifecycle Predicate In Retrieval SQL, Best-Effort Cache Invalidation, And Scale Strategy

**Context:** A deep review of `services/rag-api` found that archived/restored documents remained retrievable (no lifecycle check in retrieval SQL), republishing never deactivated the previous version's chunks, `.NET` never called the existing `POST /internal/cache-invalidations`, the specified query-time multimodal slice was unimplemented, and several scale hazards (HNSW post-filter recall collapse, per-row permission predicates, Python full-scan semantic-cache lookup, unbounded bloat) would surface as the corpus grows.

**Options Considered:** For lifecycle correctness: event-driven chunk deactivation only (`.NET` calls a deactivation endpoint) vs. a SQL predicate against `app.documents` vs. both. For cache invalidation failure handling: fail the lifecycle operation vs. best-effort. For partial-index usability: bound `corpus` parameter vs. whitelisted inline literal.

**Decision:** Seven-slice roadmap in `docs/superpowers/plans/2026-06-11-rag-hardening-roadmap.md`, one session per slice. Retrieval gains a lifecycle predicate reading `app.documents` through a new read-only grant: published-corpus chunks require `current_state = 'Published'` AND `current_published_version_id = document_version_id`; preview requires non-archived. Indexing deactivates prior chunks by `(document_id, corpus)`. `.NET` calls cache invalidation best-effort after archive/republish/restore/access-rule changes (failure logs, never blocks the operation). Scale slices: materialized allowed-documents CTE, `SET LOCAL hnsw.ef_search` + pgvector 0.8 iterative scans, per-corpus partial HNSW indexes with the corpus literal inlined from a whitelist, SQL nearest-neighbor cache lookup with citations stored on entries, embedding-model guards, purge endpoint, and true SSE token streaming. Multimodal follows the approved 2026-06-01 plan plus a delta file (revision rechaining, parser-level image-ref extraction because the chunker discards `<img>` tags, capability-based provider protocol).

**Rationale:** The SQL predicate is self-healing (no event ordering, covers missed deactivations); pairing it with deactivation keeps dead vectors out of the index. Best-effort invalidation is safe because retrieval correctness no longer depends on the cache and entries expire by TTL. Inlining the corpus literal is required because generic prepared-statement plans cannot match partial-index predicates on bound parameters.

**Tradeoffs:** Retrieval SQL now reads four `app` tables (one more grant); a republish briefly serves no chunks for the document between `.NET` state commit and indexing success (already true today via `is_active`). Cache entries grow by a citations jsonb payload; pre-existing entries are deleted on migration rather than served with degraded citations.

**Consequences:** `rag.document_chunks.is_active` is demoted to an index-hygiene flag; correctness never depends on it. `context/rag-spec.md` must be updated per slice (lifecycle predicate, pgvector index section, semantic cache section, streaming). Deferred explicitly: background indexing worker, AND-between-dimensions filter semantics, tiktoken chunk sizing, offline image captioning.

## 2026-06-12 - Chunk Uniqueness Is Scoped To Active Chunks (Partial Unique Index)

**Context:** Pre-publication indexing failed with `INDEXING_FAILED` whenever the same document version was indexed a second time (publish retry, or editing a draft then re-publishing — `UpdateDraftAsync` reuses the draft version id). The in-request pipeline deactivates a version's prior chunks (`is_active = false`) but retains the rows for query-audit / citation history; the `uq_document_chunks_version_chunk` constraint enforced uniqueness of `(document_version_id, chunk_index)` across *all* rows, so re-inserting `chunk_index = 0` collided with the retained inactive row and raised `asyncpg.UniqueViolationError`. The failure was previously invisible: the in-request pipeline catches the exception, marks the job `Failed`, and returns HTTP 200, while `.NET` surfaces only the stable error code (no message). The bare `except` is now instrumented (`logger.exception` + the real message persisted on `rag.indexing_jobs.error_message`).

**Options Considered:** (a) Partial unique index `(document_version_id, chunk_index) WHERE is_active = true`; (b) delete the same version's prior chunks before re-inserting; (c) `ON CONFLICT DO UPDATE` upsert keyed on the existing constraint.

**Decision:** Replace the global `uq_document_chunks_version_chunk` constraint with a partial unique index `uq_document_chunks_version_chunk_active` scoped to `is_active = true` (migration `20260612_120000`). No service-code change: the pipeline already deactivates prior chunks within the same transaction before inserting, so at most one active row per `(version, chunk_index)` ever exists.

**Rationale:** Option (a) matches the `is_active` partial-index pattern already adopted for the per-corpus HNSW indexes and preserves the deliberate "retain historical chunks for audit/citations" design (the v2 dimension migration made `embedding` nullable for exactly this reason). Option (b) would delete chunks that query-audit citations may reference; option (c) mutates historical rows in place and mishandles a re-index that yields fewer chunks than before (stale high-index rows stay active).

**Tradeoffs:** Inactive historical chunks may now share `(document_version_id, chunk_index)`; this is intended (one inactive set retained per re-index). The migration `downgrade` is best-effort: restoring the global constraint requires no duplicate inactive rows exist.

**Consequences:** Re-indexing the same version is now idempotent (publish retry and edit-then-republish both work). Retrieval and citations already filter on `is_active`, so they are unaffected. Regression test: `test_reindexing_same_version_succeeds_and_retains_history` in `services/rag-api/tests/test_indexing.py`. `context/rag-spec.md` should note the uniqueness scope when the indexing section is next revised.

## 2026-06-13 - Management Report Exports Use Client-Side XLSX (exceljs)

**Context:** The management Feedback review screen exported a client-side CSV that dumped 12 raw fields — internal UUIDs (`queryAuditEventId`, `userId`, `requestId`) plus a pipe/semicolon-delimited `citations` blob — which read poorly in Excel and diverged from the on-screen table. The Audit (functional events) screen had no export at all. Both screens already hold their rows client-side: Feedback from the filtered server query, Audit filtered in the browser over a `limit=100` fetch.

**Options Considered:** (a) keep and extend the hand-rolled CSV; (b) generate XLSX server-side in the `.NET` reporting endpoints (new server dependency such as ClosedXML, and shifts the contract from "export what you see" to "export a query"); (c) generate XLSX client-side from the already-loaded rows with a browser library.

**Decision:** Adopt client-side XLSX (option c) via **`exceljs`** (`^4.4.0`, MIT), **dynamically imported on click** so it stays out of the initial bundle. A shared helper `apps/manage-web/src/lib/xlsx.ts` (`buildXlsxBuffer` / `exportRowsToXlsx`) takes localized column definitions `{ header, value, width?, numFmt? }` plus rows and produces a styled sheet: bold frozen header row, autofilter, column widths, and real Excel date cells (`dd/mm/yyyy hh:mm`). Feedback and Audit each declare columns that mirror their visible table, splitting the merged question/answer and event/entity cells into dedicated columns and dropping the internal IDs. Headers reuse the existing i18n table-header keys; new keys added: `feedback.answer_column`, `audit.export`, `audit.event_type_column`, `audit.entity_type_column`, `audit.entity_id_column`, `audit.request_id_column`.

**Rationale:** Reusing the rows already loaded in the browser keeps the change small and literally satisfies "columns like the on-screen table" without new backend surface or server CPU for spreadsheet rendering. exceljs supplies the styling (frozen header, autofilter, widths, typed date cells) the previous CSV lacked. Lazy import avoids bundle bloat in an admin-only app.

**Tradeoffs:** exceljs is a heavier dependency (it bundles a zip writer); mitigated by the dynamic import. The export covers only the rows currently loaded/filtered in the browser (Feedback: the server-filtered result; Audit: the `limit=100` fetch after client filters), not the full historical table — a server-side export remains the path if full-dataset exports are later required. Dates are written as real date cells (locale-rendered by Excel) rather than the table's pre-formatted string.

**Consequences:** New `apps/manage-web/src/lib/xlsx.ts` and `xlsx.test.ts`; `FeedbackReviewPage.tsx` replaces the CSV path with the helper; `AuditPage.tsx` gains an "Exportar a Excel" action shown when filtered rows exist; `apps/manage-web/package.json` adds `exceljs`; i18n keys added in `es-AR`/`en-US`. The `.NET` reporting endpoints are unchanged. Server-side document **PDF export** (docs app) was discussed alongside this work and **suspended** for now — no library or endpoint was added; if revived, note that HiQPdf on the Linux `.NET` container runs on Chromium (not lightweight) and is commercial, so OSS headless Chromium (PuppeteerSharp) or browser print-to-PDF are the leading alternatives.

## 2026-06-13 — DOCX import extracts images to MinIO and creates the draft

**Decision:** Importing a `.docx` (`POST /api/documents/imports/docx`) now extracts embedded images, normalizes them with **SkiaSharp**, stores them in MinIO via the existing document-image pipeline, and **creates the draft document immediately** (filename → title, no access rules yet) with the images referenced through stable `/api/document-images/{id}/content` URLs. Layering: a thin App orchestrator `DocumentImportService` generates the `documentId`, calls the Infrastructure `DocumentImportExtractionService.ExtractDocxWithImagesAsync` (Mammoth `ImageConverter` + SkiaSharp normalization, returning final sanitized stable-URL HTML + normalized image bytes), persists the draft via `IDocumentRepository.SaveAsync`, then writes each image to object storage + `app.document_images`. The import-created draft is **role-gated only** (controller `[Authorize(Roles="Admin,DocumentEditor,DocumentPublisher")]`) and deliberately bypasses `CanManageDraftAsync`, which returns `false` for empty rules. PDF import is unchanged (text-only prefill); the manage-web frontend sets the PDF title from the filename client-side and routes DOCX to the new endpoint, navigating to the created draft.

**Rationale:** This feeds the already-specified query-time multimodal RAG pipeline (Slice 3), which consumes images referenced by stable URLs — previously those entered only via manual editor upload. Creating the draft at import time mirrors the existing editor rule "save before uploading images" (`RichTextEditor` disables image upload until a `documentId` exists), so it extends the established model instead of inventing a new one and resolves the chicken-and-egg of needing a `documentId` for the image FK and stable URLs.

**Tradeoffs:** A new MIT-licensed dependency, **SkiaSharp** `2.88.x` (+ `SkiaSharp.NativeAssets.Linux.NoDependencies` for the container), was chosen over ImageSharp to avoid the Six Labors commercial-license threshold (≥ USD 1M revenue, closed source). SkiaSharp decodes PNG/JPEG/GIF/BMP/WebP only — EMF/WMF/TIFF and decode failures are skipped best-effort (the `<img>` is dropped). Image persistence runs best-effort after the draft row exists (object store is not transactional). This supersedes the prior code-standards stance "Embedded DOCX images are not imported" and partially "do not persist imported file bytes" (we persist extracted, normalized images — not the original upload). Phase 1 does **not** dedup identical images and does **not** clean up MinIO objects on `<img>` removal/archive/hard-delete — those remain follow-ups.

**Consequences:** New `DocumentImageObjectKey` helper (App, DRY with `DocumentImageService`), `ImportImageNormalizer` (Infrastructure, SkiaSharp), `DocumentImportService` (App orchestrator), `IDocumentImportService` + `ExtractDocxWithImagesAsync` contracts, and `POST /api/documents/imports/docx`. No new migration (`app.document_images` already exists; empty-rules drafts persist via existing `SaveAsync`). Frontend `documents.ts` gains `importDocx`; `DocumentsPage.runImport` branches by MIME. Plan: `docs/superpowers/plans/2026-06-13-docx-image-extraction-import.md`.

## 2026-06-14 — In-editor image resize (drag handles) + editor follow-up fixes

**Decision:** The document editor gains drag-to-resize images via a custom `ResizableImage` TipTap NodeView (bottom-right drag handle, no new dependency) that persists the chosen size as a **`width` HTML attribute** on `<img>` — not inline CSS. This is the only resize persistence that survives the Ganss server sanitizer (which strips all inline CSS except `color`/`text-align`) and renders unchanged in the docs viewer, which renders server-sanitized HTML directly (there is no client-side DOMPurify in code today, despite the CLAUDE.md mention). A sanitizer test guards `width` preservation. Two editor bugs were fixed on the same branch: the toolbar block-style control is now a `<select>` ("Estilo de bloque") replacing the P/H1/H2/H3 buttons (completing the incomplete `c880971` rediseño that the editor tests already expected), and `<DocumentEditor>` is keyed by document id so a just-imported/created draft renders immediately instead of staying blank until a manual reload.

**Rationale:** Width-as-attribute avoids widening the CSS allowlist (which would weaken the sanitization invariant). A custom NodeView avoids adding a resize-image library (stack guardrail). The remount fix addresses the import flow: `upsertDocument` switched `editorState` to the persisted draft, but the reused `DocumentEditor` instance kept its empty create-mode `useState`, so the imported content only appeared after navigating away and back.

**Consequences:** New `apps/manage-web/src/features/documents/ResizableImage.ts`; `RichTextEditor` uses it instead of the base Image extension; `App.css` gains handle/select styles; `DocumentHtmlSanitizerTests` gains a width-preservation test; i18n key `documents.toolbar_block_style` added. Drag interaction is verified manually (jsdom cannot simulate layout/pointer drag).

**Follow-up (same day):** the editor toolbar is centered (`justify-content: center`), and editor images are now **inline** (`ResizableImage` configured `inline: true`, NodeView wrapper is a `<span>`) so the existing text-align buttons align the containing paragraph — and thus the image. Alignment persists as the paragraph's `text-align` (the only block-alignment CSS the Ganss sanitizer keeps); the docs viewer renders `.document-content img` as `inline-block` so paragraph `text-align` centers it. No image-node `textAlign` attribute is stored (text-align on the `<img>` itself would not center it in the viewer).

## 2026-06-14 — Multimodal answers stream token-by-token

**Context:** Query-time multimodal RAG (2026-06-01) generated the answer with a single-shot OpenAI Responses call and emitted the whole answer as one SSE `answer-token`. After the DOCX image-import slices (2026-06-13/14), real queries began retrieving image-bearing chunks, so both the chat and docs mini-chats stopped "typing" answers gradually whenever an image was selected — the multimodal branch never streamed, only cache hits/no-results and now multimodal emitted a whole-answer blob. Users reported the lost gradual typing as a regression.

**Decision:** Replace the one-shot multimodal path with streaming and unify it with the text path. The `IMultimodalLlmProvider` capability is now `multimodal_stream` (replacing `multimodal_complete`); `OpenAILlmProvider.multimodal_stream` calls `client.responses.create(stream=True)` and maps `response.output_text.delta` → content deltas and `response.completed` → usage. A shared `_stream_answer_from_deltas` helper drives both `generate_answer_stream` (text) and the new `generate_multimodal_answer_stream` through the same incremental `AnswerStreamParser`, so the `{"answer","cited_chunk_ids"}` JSON contract, citation parsing, and `RAG_PROVIDER_UNAVAILABLE` error handling stay identical. `chat_service.answer_stream` selects the multimodal-or-text delta source and forwards `answer-token` events through one shared loop; the capability probe checks `multimodal_stream`. Only cache hits and no-results answers still emit a single whole-answer token (no generation occurs there).

**Rationale:** The text path already streamed via a source-agnostic incremental JSON parser, and the multimodal path returned the same JSON payload, so reusing the parser was the smallest change that restored gradual typing for both apps. Unifying the branches also deleted a duplicated SSE-forwarding/error-handling block and a now-dead one-shot path instead of maintaining two multimodal code paths.

**Tradeoffs:** Streaming surfaces answer characters before the full JSON is parsed, but usage/cost are still finalized from the `response.completed` event, so audit accuracy is unchanged. `multimodal_complete`, `generate_multimodal_answer`, and the `_extract_output_text` helper were removed since the converted branch was their only consumer; a non-streaming multimodal call would have to be re-added if a future caller needs one.

**Consequences:** Touched `providers/base.py` (capability rename), `providers/openai_provider.py` (`multimodal_stream`; `Any`/`_extract_output_text` dropped), `rag/answer_generator.py` (`_stream_answer_from_deltas` + `generate_multimodal_answer_stream`), `rag/chat_service.py` (unified branch, probe, docstrings). Tests: `test_openai_responses_multimodal.py` rewritten for streaming events; `MultimodalFakeLlmProvider` now implements `multimodal_stream` (splitting deltas); new `generate_multimodal_answer_stream` unit test. Multimodal answers remain uncached; the 3-image / 5 MB / `detail:"low"` caps are unchanged. The `/api/chat` SSE contract (`answer-token` events) is unchanged for the frontend.

## 2026-06-14 — Document type is an admin-managed catalog (FK), not free text

**Context:** A document's "type" was free-text `document_versions.document_type varchar(80)` set per version through a plain text input in the editor. There was no controlled vocabulary, so values drifted by spelling/casing; the docs portal derives an icon/tint by substring-matching the free string, and the manage list builds its type filter from whatever distinct values exist. The user asked to make Type a managed dropdown with an admin ABM, seeded with Articulo/Instructivo/Procedimiento.

**Options Considered:** (a) keep the free string with editor autocomplete from existing values; (b) denormalized snapshot — store both a catalog id and a copied name on each version; (c) move type up to the `documents` row (not versioned); (d) a normalized catalog table referenced by FK from `document_versions`, keeping type versioned.

**Decision:** Option (d). New `app.document_types` table (`Id`, unique `name`, `is_active`, `sort_order`, timestamps) seeded with the three canonical types; `document_versions.document_type` (string) is replaced by a **nullable** `document_type_id` FK (`ON DELETE RESTRICT`). Type stays versioned (a property of `DocumentVersion`). The catalog name follows the existing taxonomy convention — a single, non-localized name like Groups/Org-Units/Tags, not bilingual. Read DTOs expose both `documentTypeId` and the resolved `documentType` **name**, so docs-web and the viewer/catalog (which still receive a name string) are unchanged; only the editor write path switched to `documentTypeId`. The FK is nullable on purpose to preserve "save an incomplete draft": the unset type is rejected only at send-to-review/publish (same `missing.Add("documentType")` guard, now an id-null check) and assisted import creates drafts with no type. Admin ABM: `GET/POST/PUT/DELETE /api/document-types` (list readable by any manager for the editor dropdown; mutations Admin-only). Hard `DELETE` is blocked when any version references the type (new `DOCUMENT_TYPE_IN_USE` 409); the "baja" is `is_active=false`, which hides the type from the editor dropdown for new selections while existing documents keep it.

**Rationale:** A normalized FK gives one source of truth (rename once, applies everywhere) and matches how the other admin-defined taxonomy already works, including a single non-localized name. Keeping type on the version preserves existing versioning semantics and is the least invasive change. Exposing the resolved name in read DTOs meant zero changes to docs-web and the viewer. rag-api never read the type, so retrieval/indexing is unaffected.

**Tradeoffs:** A rename propagates to already-published versions' displayed type (desired for a controlled vocabulary; the stable FK id keeps the classification fixed even when the label changes). Case-insensitive uniqueness is enforced in the service (`NameExistsAsync` lowercases) over a plain unique index on `name`, since the sole writer is the service. The docs-web icon heuristic still substring-matches the resolved name instead of a per-type icon (deliberate non-goal). Audience — the structurally identical sibling free-text field — was removed entirely the next day (see the 2026-06-15 entry below).

**Migration safety (the user's main concern for `updateService.sh`):** Hand-written idempotent migration `20260614223958_AddDocumentTypeCatalog`. The repo writes migrations by hand and does **not** maintain the EF model snapshot, so `dotnet ef migrations add` produced a garbage full-schema diff that was discarded (Designer deleted, snapshot reverted) and the migration was authored in the repo's `migrationBuilder.Sql` idempotent style. Steps: create the table + unique index, seed the three types with fixed UUIDs (`ON CONFLICT DO NOTHING`), add the nullable FK column + index + constraint (guarded via `pg_constraint`), then **backfill non-destructively** — create a catalog row per distinct existing free-text value (one per case-insensitive value via `DISTINCT ON (lower(...))`, `gen_random_uuid()`), map each version by name, leave empty/whitespace as null, and finally drop the old `document_type` column. Because the FK is nullable and only points at rows the migration created, it cannot fail on legacy data. Verified by `AppDbContextMigrationTests` (all migrations on a fresh DB and the legacy-upgrade path: table present, `document_type` gone, `document_type_id` present).

**Consequences:** Backend — new `DocumentType` entity + DbContext config; `DocumentTypeService` / `IDocumentTypeRepository` / `EfDocumentTypeRepository`; `DocumentTypesController` (+ request/response models); lifecycle commands/records carry `DocumentTypeId` (write) and resolved `DocumentType` name (read); `DocumentLifecycleService` resolves+validates the type via an injected `IDocumentTypeRepository`; `EfDocumentRepository` and `EfViewerAccessRepository` resolve the name by join; assisted import creates drafts with a null type; new error code `DOCUMENT_TYPE_IN_USE`. Frontend (manage-web) — `api/documentTypes.ts`; the editor Type input became a `<select>` of active types (plus the current one if it was deactivated); a new Admin-only `DocumentTypesPage` ABM with a `document-types` nav section; i18n keys in `es-AR`/`en-US`. docs-web, chat-web, and rag-api unchanged. Seed labels are `Artículo`, `Instructivo`, `Procedimiento` (es-AR accented form) and are admin-editable.

## 2026-06-15 — Removed the document Audience field; document-type ABM uses icon-only row actions

**Context:** Follow-up to the document-type catalog. The user asked to (a) drop the document **Audience** field entirely, (b) make the Document Types ABM row actions match the Users ABM (icon-only buttons with the label in a tooltip), and (c) decide where the catalog lives in the nav.

**Decision:** (a) **Audience is removed end to end** — the `document_versions.audience` column (migration `20260615120000_RemoveDocumentAudience`, idempotent `drop column if exists`), the entity/records/commands/DTOs, the send-to-review required-field guard, the manage-web editor field + list filter + table column, and the docs-web portal/viewer display (card + chips), plus its i18n keys and the search-placeholder wording. The JWT `aud` claim in rag-api is unrelated and untouched. (b) `DocumentTypesPage` row actions now use the shared `Button` `icon-button` + `tooltip` pattern (icon only, descriptive per-row `aria-label`, action word in the tooltip), matching `UsersBudgetPage`. (c) The catalog stays a **sidebar** section (Admin-only), consistent with Organizational Units, rather than a tab inside Documents — keeping the Admin-only catalog out of the editor-facing workspace and symmetric with the other admin catalog.

**Rationale:** Audience was an unused free-text classifier with no retrieval or authorization role; removing it is simpler than catalog-izing a field nobody asked to keep. The idempotent FK-style drop means `updateService.sh` applies it cleanly. The sidebar placement avoids gating a Documents tab by role and mirrors the existing taxonomy admin surface.

**Tradeoffs / fixups:** Removing two columns surfaced that the demo seed (`Seed-LocalDemoData.ps1`) and the Playwright e2e specs still wrote the old free-text `document_type` string and `audience` — both would now fail (columns gone / field ignored), so they were corrected to use `document_type_id` (seeded `Artículo` id) and drop `audience`; this also closes a gap left by the document-type catalog work. Historical design entries that list audience as a required/classification field are left as-is (this entry supersedes them).

**Consequences:** Backend, manage-web, and docs-web all drop Audience. Full backend suite (App 69, Api 69, Infrastructure 28 incl. the migration test), manage-web (54) and docs-web (19) vitest suites, typecheck, lint, and build are green. e2e and the demo seed are user-run (Docker). New migration `20260615120000_RemoveDocumentAudience`.

## 2026-06-15 — Feedback report shows reviewer name + email via a column-level cross-schema grant

**Context:** The manage-web Feedback review table showed the reviewer as a raw user GUID because `NpgsqlFeedbackReportingService` selected `event.user_id::text as user_display_name` from `rag.v_query_audit_with_citations`. The rag query-audit data only stores `user_id`; the human-readable name/email live in the `app` schema (`app.users.display_name` / `.email`), which the .NET reporting role (`app_reporting_reader`) could not read — `init.sql` grants it only `USAGE ON SCHEMA rag` plus SELECT on the rag reporting views. The user asked for "nombre y mail como en tab usuarios" instead of the id.

**Options Considered:** (a) enrich at the API/service layer by looking up users through the EF `app` DbContext (app_user role) after the report query; (b) extend the SQL to LEFT JOIN `app.users` and grant the reporting role read access; (c) denormalize display name/email into the rag audit table at write time.

**Decision:** Option (b). The reporting connection already sets `SearchPath = "rag,app,public"`, so cross-schema reads were the intended design; only the grant was missing. The report SQL now `left join app.users account on account."Id" = event.user_id` and selects `coalesce(account.display_name, event.user_id::text) as user_display_name` plus `account.email as user_email`. A new idempotent EF migration `20260615130000_GrantReportingReaderUsersReadAccess` grants `app_reporting_reader` `USAGE ON SCHEMA app` and **column-level** `SELECT ("Id", display_name, email) ON app.users` — never the whole table, because `app.users` also holds `password_hash`. `FeedbackReportItem`/`FeedbackReportItemResponse` gained a nullable `UserEmail`; manage-web renders the user cell as stacked `user-name` + `user-email` spans like the Users tab and adds an Email column to the XLSX export.

**Rationale:** A single SQL join keeps the report in one query and matches the existing reporting design (the reporting role and SearchPath already anticipate reading `app`). Column-level GRANT keeps the role least-privileged and avoids exposing the password hash, unlike a table-wide grant. The `coalesce` preserves the old GUID fallback for orphaned rows (deleted users), and the LEFT JOIN never drops feedback rows.

**Tradeoffs:** The reporting role now depends on `app.users` column names (`"Id"`, `display_name`, `email`); renaming those columns must update the grant. This keeps FastAPI's "never write app / never read across except via granted reporting surfaces" rule intact — it is the **.NET** reporting role reading **its own** `app` schema, not a rag→app write. The migration is guarded by `to_regrole('app_reporting_reader')`, so it no-ops on the Testcontainers DB (role created only by Compose `init.sql`).

**Consequences:** Backend — `FeedbackReportingTypes.cs`, `NpgsqlFeedbackReportingService.cs` (SQL + reader + builder), `FeedbackReportResponses.cs`, `ReportingController.cs`, new migration; tests `NpgsqlFeedbackReportingServiceTests` (asserts join/select) and `FeedbackReportingEndpointTests` (fake returns + asserts email). Frontend — `api/reporting.ts` (`userEmail`), `FeedbackReviewPage.tsx` (two-span cell + export column), `feedback.email_column` i18n (es/en). Also in this branch: client-side pagination — Feedback 10/page, Audit 15/page — via the shared `Pagination` component with a new `.table-pagination` style; pagination renders only when the (filtered) result set exceeds one page and resets to page 1 on reload/filter change. manage-web typecheck/lint/test (54) and `.NET` build + `NpgsqlFeedbackReportingServiceTests` are green; the migration and full integration suite are Docker/user-run.

## 2026-06-15 - Configurable Operational Settings

**Context:** The "Configuración" screen was read-only and env-sourced (`GET /api/configuration`). A fully-built but dormant tenant-config layer (`app.tenant_config` singleton + `ITenantConfigService` + `GET/PUT /api/v1/config`) existed but was unused by any frontend and not consumed by FastAPI. Operators could not change the chat model, semantic-cache TTL/threshold, default monthly budget, customer timezone, import size limit, or max chat-question length without redeploying.

**Options Considered:** (a) keep env-only and require redeploys; (b) expose the full fat `/api/v1/config` DTO to the screen; (c) wire a scoped, editable subset of the dormant tenant-config layer into the existing single screen endpoint and into the two real consumption points, and have FastAPI read tenant_config live.

**Decision:** Option (c). `app.tenant_config` becomes the source of truth for the editable operational subset, surfaced through `GET /api/configuration` (editable values from tenant_config; embedding/secrets from env) and a new Admin-only `PUT /api/configuration` (validated by `TenantConfigService.UpdateAsync`). `.NET` consumes the editable values at their real points — document import size limit (`DocumentImportExtractionService`) and the new-user budget default (`UserAdministrationService`). FastAPI reads `app.tenant_config` through a read-only `GRANT SELECT` to `rag_owner` and refreshes its in-memory `Settings` on a short TTL (30s) at the start of every chat request (`ChatService.precheck`), so edits propagate live without restart and without touching ~15 `settings.*` call sites. Embedding model/dimensions and the provider stay fixed (env-sourced, read-only); provider API keys remain Compose secrets, never in the DB. A one-time `TenantConfigEnvSeeder` copies the live env values into the singleton (guarded by `seeded_from_env`) before it becomes authoritative.

**Rationale:** Reusing the dormant layer avoids a parallel config system. Keeping embedding fixed preserves vector comparability (changing embedding invalidates all indexed vectors until re-index). Mutating the shared `Settings` instance is the lowest-risk way to make edits live in FastAPI without rewriting every consumer. The env-seed-once step prevents a silent chat-model switch on upgrade, because the dormant `tenant_config` defaults (`llm_model='gpt-4o-mini'`) diverge from the MVP env (`OPENAI_CHAT_MODEL='gpt-4.1-nano'`) and no admin had ever written the row.

**Tradeoffs:** FastAPI adds one `app.tenant_config` SELECT per chat request, TTL-gated to ~once/30s per process. The fat `/api/v1/config` endpoint is now superseded by `GET/PUT /api/configuration` and is a future cleanup candidate. The refresher mutates a process-local `Settings`, so propagation is per-process and bounded by the TTL rather than instant across replicas (acceptable for the single-tenant deployment).

**Consequences:** New error code `CHAT_QUESTION_TOO_LONG` (400). New columns on `app.tenant_config` (`customer_timezone`, `import_max_file_size_mb`, `chat_max_question_chars`, `seeded_from_env`) via migration `20260615140000`; read grant via `20260615140500`. manage-web's Configuración screen is Admin-editable (`updateOperationalConfiguration`). Migrations remain hand-written raw SQL — the EF model snapshot is unmaintained and unused by `MigrateAsync`, so it was intentionally left unchanged. `.NET` build + mapping/migration/endpoint/seeder/import/user-admin tests green; FastAPI pytest/ruff/mypy (incl. refresher + chat over-long) green; manage-web typecheck/build/lint/test (56) green. User-owned: Compose-stack browser acceptance and live-propagation checks.
