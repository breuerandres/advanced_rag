# Advanced RAG Document Platform

## Overview

Advanced RAG Document Platform is a single-tenant corporate document management and RAG product. It lets customer administrators and document managers create, review, publish, archive, and audit internal documents, while viewers use a secure chat frontend and token-gated document viewer to access only the published content allowed by their role, groups, and document attributes.

## Goals

1. Provide a secure document lifecycle from draft to published content with explicit roles, audit, and publication control.
2. Serve end users with a RAG chat experience that only retrieves content matching their effective access scope.
3. Keep deployment sellable as a per-customer Docker Compose product with isolated data, secrets, logs, and configuration.
4. Capture operational and quality signals, including query audit, token/cost metadata, cache behavior, citations, and chat feedback.
5. Treat chat response speed as a first-class quality attribute for the MVP.
6. Let administrators control AI spend through configurable per-user monthly monetary budgets.

## Core User Flow

1. An `Admin` configures users, roles, groups/departments, access attributes, and AI budget limits.
2. A `DocumentManager` creates an document manually or imports a PDF/DOCX to prefill the editor with extracted text, then edits normalized HTML and metadata, manages groups and user group assignments, and sends the draft to review.
3. An `Admin` publishes from `In Review`; publication is blocked until FastAPI indexes the document successfully.
4. A `Viewer` asks questions in `chat.client.com`; FastAPI retrieves only published content matching the viewer's effective access scope.
5. The viewer opens cited documents through short-lived scoped links to `docs.client.com`.
6. The viewer can submit thumbs up/down feedback with an optional comment for each chat answer.
7. Any authenticated user can use the limited management self-service area to update their own account and inspect their own AI usage balance.

## Features

### Document Management

- Local user, role, and group/department administration through the .NET management API.
- Document creation, import, metadata editing, review transitions, publication, archive, restore, and audit.
- Document images uploaded from the editor are stored in private S3-compatible object storage and referenced through authorized app-controlled URLs.
- Document access is assigned by groups/departments and document attributes in the MVP; per-user document exceptions are handled by creating dedicated groups, not by direct user ACLs.
- Assisted PDF/DOCX import that extracts text into the editor while leaving final formatting and attributes under user control.
- Simple formal versioning where every successful publication creates an immutable published version.
- Pre-publication indexing through FastAPI before content becomes public.

### Chat And RAG

- Public chat frontend that talks directly to FastAPI.
- FastAPI validates short-lived signed chat access tokens issued by .NET.
- Chat access tokens are issued or renewed by .NET from the main secure session, stored only as host-only `HttpOnly` cookies on `chat.client.com`, and validated locally by FastAPI.
- Default MVP OpenAI models are `gpt-4.1-nano` for chat and `text-embedding-3-small` for embeddings with native 1536 dimensions to reduce cost until the MVP is running end to end. Models remain configurable per deployment.
- Query-time multimodal RAG can attach a capped set of authorized document images from retrieved chunks so chat can answer visual questions without exposing raw object storage URLs or sending every document image to the model.
- Published-only retrieval for normal viewers and internal preview retrieval for authorized management users.
- Semantic cache with `access_scope_hash`, conservative default similarity threshold, TTL, citations, and source-document invalidation.
- Thumbs up/down answer feedback with optional comment tied to RAG query audit.
- The same user can update their feedback for an answer in the MVP; feedback history is not retained.
- Feedback review in the management app for `Admin` and `DocumentManager`, with filters by negative feedback, cited document, user, and date range.
- Per-user monthly AI usage budgets in monetary value, configurable by administrators from the management app.
- Role-limited management self-service lets `Viewer`, `DocumentManager`, and `Admin` users update their own account and view their own AI balance without exposing unauthorized management workspaces.
- `DocumentManager` can view all users and AI balances, create/edit groups, and assign users to groups, but cannot create users, change roles, deactivate users, or modify AI budget limits.
- The default monthly AI usage budget is USD 5 per user.
- Monthly AI usage budgets reset by customer calendar month using the deployment's configured timezone.
- Budget exhaustion blocks new paid chat/RAG usage, not document viewing or management access.
- Over-budget users do not receive semantic cached answers in the MVP because semantic cache lookup requires a paid embedding request.

### Document Viewer

- Token-gated document viewer at `docs.client.com`.
- Viewer links use document-id URL locators; the docs frontend revalidates the authenticated `.NET` session and document permissions before content is returned.
- Credential-bearing viewer tokens are not used in browser URLs or JavaScript.
- Viewer links from chat limited to `Published` documents; management links may allow draft/review access for authorized users.
- Chat access uses short-lived tokens signed by .NET and validated locally by FastAPI to preserve chat latency.

### Operations And Audit

- One PostgreSQL database per customer with separate `app` and `rag` schemas.
- Functional audit in Postgres and daily structured JSON technical logs per service.
- Configurable operational defaults for request lengths, rate limits, log retention, token TTLs, semantic cache, timezone, and AI budgets.
- Versioned model pricing and query-level cost snapshots.
- Per-user monthly budget enforcement uses the cost snapshots stored in RAG query audit and configurable limits managed by the .NET management API.
- RAG chunks and embeddings are stored together in `rag.document_chunks`.
- Chat answer citations are stored as child rows of RAG query audit, and simple thumbs feedback is stored on the query audit event for the MVP.
- Read-only management reporting over RAG audit for feedback review and usage analysis.
- Docker Compose deployment with Caddy, Compose secrets, health checks, and service readiness.

## Scope

### In Scope

- Three independent React frontends: management, chat, and document viewer.
- Tailwind CSS, `shadcn/ui`, and `lucide-react` as the MVP UI foundation for all frontends.
- .NET 8 API for identity, document lifecycle, assisted PDF/DOCX text extraction, viewer access tokens, management audit, and user/group administration.
- FastAPI service for public chat, retrieval, embeddings, semantic cache, RAG audit, indexing jobs, and chat feedback.
- Management configuration for per-user monthly AI usage budgets and read-only budget usage reporting.
- PostgreSQL with pgvector, EF Core migrations for `app`, and Alembic migrations for `rag`.
- Docker Compose single-tenant deployment behind Caddy.

### Out of Scope

- Multi-tenant SaaS deployment.
- Kubernetes deployment for the MVP.
- External SSO/OIDC for the MVP.
- Dedicated `Reviewer` role for the MVP.
- Retaining original PDF/DOCX import files in the MVP.
- OCR for scanned PDFs or images in the MVP.
- One-time-use viewer access tokens; the current browser runtime uses session-authenticated document-id links instead of viewer tokens.
- Separate soft delete workflow beyond `Archived`.

## Success Criteria

1. An `Admin` can publish an document only after successful pre-publication indexing.
2. A `DocumentManager` can manage draft/review content but cannot publish.
3. A `Viewer` can access only role-limited management self-service plus chat and allowed published documents; full management workspaces remain hidden and forbidden.
4. Public chat never retrieves non-`Published` content.
5. RAG query audit records question, answer, citations, cache hit, feedback, token usage, estimated cost, latency, request ID, and access scope.
6. A single customer deployment can run with Docker Compose using isolated secrets, data, logs, and health checks.
7. Chat response latency is measured and can be evaluated when tuning model and cache defaults.
8. An `Admin` can set or adjust a user's monthly AI budget, all users can inspect their own budget state, and the chat service blocks new paid AI usage when that user reaches the configured budget without blocking authorized document viewing or account self-service.
