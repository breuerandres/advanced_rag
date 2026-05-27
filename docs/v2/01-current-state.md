# 01 — Current State of the MVP (Baseline at v2 Start)

Snapshot of what the MVP delivered before the v2 refactor started. This file is
historical baseline context, not the current v2 implementation inventory. For current
v2 status, read `HANDOFF.md`, `docs/v2/03-phases.md`, and `context/v2-progress.md`.

For the full original spec, read `context/architecture.md`, `context/rag-spec.md`,
`context/code-standards.md`, and `context/ui-context.md`. This page is the abridged version.

## MVP Baseline Stack

| Layer | Tech | Source path |
|---|---|---|
| Reverse proxy | Caddy 2 | `infra/compose/Caddyfile` |
| Management API | .NET 8 + ASP.NET MVC + EF Core | `services/dotnet-api/` |
| RAG API | FastAPI 0.115 + SQLAlchemy 2 async + Pydantic 2 | `services/rag-api/` |
| Database | PostgreSQL 16 + pgvector | `infra/compose/postgres-init/` |
| Frontends | React 18 + TS + Vite + Tailwind + shadcn/ui | `apps/manage-web/`, `apps/chat-web/`, `apps/docs-web/` |
| Orchestration | Docker Compose v2 | `infra/compose/compose.yaml` |

## Feature inventory (MVP delivered)

**Auth & session**
- Local users (email + PBKDF2 password)
- `__Host-session` cookies, CSRF double-submit, RS256 JWT signing with rotation
- Roles: `Admin`, `DocumentManager`, `Viewer`
- Chat token (15 min, set as `__Host-chat-token` for `chat.localhost`)
- Viewer exchange codes (60 s) → `__Host-viewer-token` (15 min) for `docs.localhost`

**Documents**
- Lifecycle: Draft → In Review → Published → Archived
- Per-publication version is immutable (`v1`, `v2`, …)
- Permissions by group + attributes (no per-user ACLs)
- Assisted PDF/DOCX import (DocumentFormat.OpenXml, PdfPig) — text extracted into the
  TipTap editor, user finalises HTML
- HtmlSanitizer + DOMPurify on render

**Indexing (RAG)**
- HTML-aware chunker: 500-token target, 800 hard cap, 80 overlap
- Embedding: `text-embedding-3-small` @ 1536 dims, batches of 100
- Storage: `rag.document_chunks (id, document_id, version_id, embedding VECTOR(1536), …)`
- HNSW index `m=16 ef_construction=64 ef_search=40`
- Per-version: `is_active` flag flipped on republish

**Retrieval**
- `k = 8` cosine kNN
- SQL-level permission filter (`EXISTS` against `app.document_permissions`)
- No reranker, no BM25, no query rewrite, no multi-turn

**Generation**
- `gpt-4.1-nano` @ temp=0.1, max_tokens=900
- Structured output JSON for citations
- SSE streaming with custom parser
- Spanish (`es-AR`) hard-coded in `system_v1.md` prompt

**Semantic cache**
- Key: `(corpus, access_scope_hash, question_embedding)` with cosine ≥ 0.90
- TTL: 24 h
- Invalidation: per-document on archive/republish

**Audit & cost**
- `rag.query_audit_events`: tokens, cost USD, latency, prompt_version, chunker_version
- `rag.query_audit_citations`: chunk-level child rows
- `rag.model_pricing`: snapshots per query

**Budgets**
- `app.user_ai_budget_limits`: USD/month per user
- Default USD 5
- Enforced before any paid call (embedding, chat)

**Feedback**
- Thumbs ±, single comment, overwritable
- Stored on `rag.query_audit_events`
- Management reporting view + CSV export

**Frontends**
- `manage-web`: documents tab (TipTap editor + filters), users & groups, audit, feedback,
  configuration, setup
- `chat-web`: single-page question/answer with citations, feedback, viewer link exchange
- `docs-web`: viewer with code exchange + safe states for expired/invalid

**Operations**
- Per-service JSON logs (`Serilog`/`structlog`), daily rotation 30 days
- Per-process rate limits (login/IP, login/user, chat/user, import/user, viewer-exchange)
- Health endpoints `/health/live` (process) and `/health/ready` (deps)
- Compose secrets, pg_dump backup container
- Caddy + auto-TLS for `*.localhost` via Caddy internal CA
- Setup wizard endpoint (`/api/setup/admin`) + partial UI

## What the MVP was missing (motivation for v2)

| Gap | v2 Solution | Phase |
|---|---|---|
| Embedding model assumed Spanish-only | Multilingual embedding | 1 |
| OpenAI hard-coded | Provider abstraction | 1 |
| No BM25, no reranker | Hybrid retrieval | 2 |
| No conversational memory | Multi-turn session | 5 |
| Three separate auth tokens | Unified session cookie | 1.5 |
| Three SPAs with diverging UX | `packages/shared-ui` design system | 1.5 |
| UX was MVP-functional, not pro | Linear/Vercel-style refactor | 1.7 |
| No dark mode | Theme system in `shared-ui` | 1.5 |
| No command palette | Cmd+K via `cmdk` | 1.5 |
| Images stored as base64 inline | MinIO S3-compat with signed URLs | 3 |
| Single-language UI | `react-i18next` with ES/EN | 1 |
| No favourites, reactions, view tracking | Ported from CentroDeAyuda | 4 |
| No tags/dimensions | Configurable dimensions | 2 |
| No API keys (server-to-server) | API keys + rate-limit-per-key | 4 |
| No eval framework | RAGAS continuous in CI | 5 |
| No OTel | Optional OTel collector overlay | 5 |
| No bulk import | ZIP import endpoint + job queue | 3 |
| No deep-link filters | Chat accepts `?dim_x=val` filters | 2 |

## Tests in MVP

- `.NET`: xUnit + Testcontainers Postgres + FluentAssertions — 72 tests passing at handoff
- FastAPI: pytest + Testcontainers — 32 tests passing
- Frontends: Vitest + Testing Library — 50+ tests passing
- E2E: Playwright single happy-path scenario passing (`tests/e2e/specs/`)

All test infrastructure stays. New tests authored during v2 add to these directories.
