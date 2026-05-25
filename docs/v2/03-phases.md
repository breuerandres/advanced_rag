# 03 - Phases & Tasks

The v2 refactor is broken into six phases. This checklist was reconciled on 2026-05-25
against the current repository state.

Legend: `[x]` implemented in current code, `[~]` partial, `[ ]` pending.

## Phase 0 - Preparation

**Goal:** repository ready, v2 docs/ADRs in place, baseline branch merged.

- [x] v2 docs and ADRs exist under `docs/v2/` and `docs/adr/`
- [x] `context/v2-overview.md` and `context/v2-progress.md` exist
- [x] `feature/v2-generic` has been merged into `mvp-implementation`
- [x] Current reconciliation started from a clean working tree

## Phase 1 - Foundations Generic

**Goal:** same MVP features, but provider, locale, and embedding assumptions are
pluggable.

### 1.1 Tenant config

- [~] Raw SQL script for `app.tenant_config` exists in `services/dotnet-api/v2-migrations-sql/001_add_tenant_config.*.sql`
- [ ] EF Core migration for `app.tenant_config`
- [ ] Setup wizard endpoint writes `tenant_config` in the same transaction as setup
- [ ] `GET /api/v1/config` returns public-safe config; `PUT` is admin-only
- [ ] Tests for setup/config behavior and safe secret handling

### 1.2 Embedding migration

- [x] RAG Alembic migration changes `rag.document_chunks.embedding` to `vector(1024)`
- [x] Runtime settings and `infra/compose/.env.example` use embedding dimensions `1024`
- [~] Default Compose embedding model is still `text-embedding-3-small`; final default is blocked by `OQ-002`
- [ ] Reindex script and admin-triggered reindex job
- [ ] `docs/operations/reindex.md`

### 1.3 Provider abstraction

- [x] Provider protocols exist in `providers/base.py`
- [x] OpenAI, Azure OpenAI, Anthropic, Ollama, TEI embedding/reranker, and Cohere reranker provider files exist
- [x] `ProviderFactory` exists and is wired from `Settings`
- [x] `ChatService` consumes `ILlmProvider`, `IEmbeddingProvider`, and optional `IRerankerProvider`
- [x] `InternalIndexingService` consumes `IEmbeddingProvider`
- [~] Tests use fake LLM/embedding providers for chat/indexing paths
- [ ] Provider factory reads `app.tenant_config`
- [ ] Runtime dependencies for optional Anthropic/Cohere/httpx-backed providers are promoted from lazy/import-time assumptions into package requirements before those providers are enabled

### 1.4 i18n frontends

- [x] `i18next`, `react-i18next`, and `i18next-browser-languagedetector` are declared in all three SPA package manifests
- [x] `es-AR`, `en-US`, and `pt-BR` catalogs exist for all three SPAs
- [x] Visible language selectors exist in manage/chat/docs through `LanguageSelect`
- [~] Some screen strings use `t(...)`
- [ ] Full literal extraction from TSX is incomplete
- [ ] Tests consistently assert translated output rather than hard-coded Spanish literals

### 1.5 Multi-language prompts

- [x] `system_{es-AR,en-US,pt-BR}.md` exists
- [x] `condenser_{es-AR,en-US,pt-BR}.md` exists
- [x] `rewriter_{es-AR,en-US,pt-BR}.md` exists
- [x] Answer generation loads `system_<locale>.md` with `en-US` fallback
- [ ] Audit records `prompt_locale`

### 1.6 Token counting

- [ ] `chunking.py` still uses whitespace splitting, not `tiktoken.encoding_for_model`
- [ ] Tests for known token fixtures and chunk boundaries

### 1.7 Retry/timeout wrapper

- [x] `providers/_retry.py` exists with `tenacity` retry helpers
- [x] Provider implementations import and use `retry_async`
- [ ] Dedicated retry tests for simulated 429, 503, timeout, and connection errors

### 1.8 Branding

- [ ] Setup wizard branding step
- [ ] Tenant brand config persistence
- [ ] Logo upload endpoint
- [ ] Header/brand token override from tenant config

## Phase 1.5 - Unified Auth + Shared UI

**Goal:** a single browser session serves manage, chat, and docs; shared UI primitives are
available.

### 1.5.1 Roles

- [~] Raw SQL script exists for `app.users.role`
- [ ] EF Core migration for `app.users.role`
- [ ] Backfill from `user_roles`
- [ ] Compatibility/deprecation plan for `user_roles` and `roles`

### 1.5.2 Eliminate token flows

- [ ] Remove `POST /api/auth/chat-token`
- [ ] Remove `/api/viewer/exchange`
- [ ] Remove `viewer_exchange_codes` runtime usage
- [~] Update Caddy/FastAPI/frontend code for unified session auth

Current code is transitional:

- `.NET` issues the browser session cookie as `__Host-session`.
- `.NET` exposes internal `GET /internal/session/validate` guarded by
  `X-Internal-Service-Token`.
- FastAPI chat and feedback read `__Host-session` through the configured session validator.
- `chat-web` no longer calls `POST /api/auth/chat-token` before chat requests.
- Legacy `POST /api/auth/chat-token`, `/api/viewer/exchange`, and
  `viewer_exchange_codes` runtime usage still exist.
- `docs-web` still calls `/api/viewer/exchange`.

### 1.5.3 FastAPI cookie validation

- [x] Resolve `OQ-001` (2026-05-25: internal .NET session validation + 60s FastAPI cache)
- [x] Implement selected FastAPI session validation
- [x] Tests for fail-closed invalid validation, cache behavior, and valid session resolution

### 1.5.4 Endpoint authorization

- [ ] Update role authorization model to v2 roles (`admin`, `editor`, `viewer`)
- [ ] FastAPI role dependency for chat/feedback where needed
- [ ] Audit records `role_at_request`

### 1.5.5 `packages/shared-ui`

- [x] Package manifest and workspace wiring
- [x] Light/dark tokens, fonts, and globals
- [x] Hooks: `useTheme`, `useShortcut`
- [x] Layout primitives: `AppShell`, `Sidebar`, `Header`
- [x] `Button`
- [x] `LanguageSelect`
- [x] `DarkModeToggle`
- [x] `CommandPalette`
- [ ] Inputs: `Input`, `Textarea`, `Select`, `Switch`, `Checkbox`, `RadioGroup`
- [ ] Overlays: `Dialog`, `HoverCard`, `Tooltip`, `Popover`, `DropdownMenu`, `Drawer`
- [ ] Data: `DataTable`, `Pagination`, `Badge`, `Avatar`
- [ ] Feedback: `Toast`, `Skeleton`, `EmptyState`
- [ ] Markdown: `Markdown`
- [ ] Chat components: `ChatMessage`, `ChatComposer`, `ConversationList`, `CitationCard`, `CitationDrawer`
- [ ] Colocated component tests for shared-ui primitives

### 1.5.6 Apply shared-ui to SPAs

- [x] Shared styles imported by all three SPAs
- [x] `manage-web` uses `AppShell`, `Sidebar`, `DarkModeToggle`, and `LanguageSelect`
- [x] `chat-web` uses `AppShell`, `DarkModeToggle`, and `LanguageSelect`
- [x] `docs-web` uses `AppShell`, `DarkModeToggle`, and `LanguageSelect`
- [ ] Full v2 shell refactor using shared UI data/overlay primitives
- [ ] Storybook

## Phase 1.7 - UX Refactor Per SPA

**Goal:** each SPA reaches the target v2 product UX.

### 1.7.1 chat-web

- [ ] Three-pane layout
- [ ] Conversation list
- [ ] Shared `ChatMessage`
- [ ] Shared `ChatComposer`
- [ ] Streaming cursor animation
- [ ] Citation preview/drawer
- [ ] Command palette wiring
- [ ] Filter chips

### 1.7.2 docs-web

- [ ] Dimension-grouped sidebar tree
- [ ] Right TOC with scroll-spy
- [ ] Breadcrumbs and previous/next
- [ ] Metrics panel
- [ ] Helpful reaction/comment UI
- [ ] Favorites
- [ ] Global search
- [ ] Sanitized HTML render with image behavior

### 1.7.3 manage-web

- [ ] Dashboard with KPI cards/charts
- [ ] Shared `DataTable` for documents
- [ ] Editor autosave/slash-command polish
- [ ] React Hook Form + Zod form refactor
- [ ] Users role dropdown and bulk CSV import
- [ ] Dimensions editor
- [ ] Analytics page

## Phase 2 - Hybrid Retrieval + Dimensions

**Goal:** improve retrieval precision and support flexible categorization.

### 2.1 Dimensions schema

- [x] EF migration exists for `app.dimensions`, `app.dimension_values`, and `app.document_dimension_values`
- [x] `rag_owner` read grants for dimension tables exist in the EF migration
- [ ] EF Core entities and DbContext mappings for dimensions

### 2.2 Dimensions CRUD

- [ ] `DimensionsController`
- [ ] Validation for unique keys and parent cycles
- [ ] Management UI for dimensions
- [ ] Document assignment UI

### 2.3 BM25 columns + indexes

- [x] Alembic migration adds BM25/trigram columns and indexes
- [ ] Re-chunk/reindex path for existing chunks beyond the migration itself

### 2.4 RRF combinator

- [x] `hybrid_retrieval.py` exists
- [x] `ChatService` calls `hybrid_retrieve`
- [x] Query audit stores vector/BM25 top-k fields
- [ ] Golden-set retrieval metric validation

### 2.5 Reranker

- [x] `IRerankerProvider` exists
- [~] Reranker provider files exist, but optional runtime dependencies must be verified before enabling non-default providers
- [x] `ChatService` runs `rerank_candidates` after hybrid retrieval when a reranker is configured
- [~] Settings include `enable_reranker`; tenant_config toggle is not implemented
- [ ] Per-query `rerank=false` flag
- [ ] Dedicated mocked-reranker tests

### 2.6 Filters in chat

- [x] FastAPI chat schema accepts `filters.dimensionValueIds`
- [x] Backend tests cover dimension filters partitioning cache separately
- [x] Cache/audit paths include `filters_hash`
- [ ] Chat-web deep-link parsing and filter chips

## Phase 3 - Object Storage + Bulk Import

**Goal:** move assets out of Postgres and support bulk onboarding.

### 3.1 MinIO

- [x] `compose.v2-extras.yaml` exists with MinIO
- [x] `infra/compose/minio/init-bucket.sh` exists
- [ ] Caddy `/storage/*` route with signed URL behavior
- [ ] Tenant storage config

### 3.2 Object storage abstraction

- [ ] .NET object storage abstraction
- [ ] FastAPI object storage abstraction
- [ ] Filesystem fallback for development

### 3.3 PDF/DOCX improvements

- [ ] Table extraction
- [ ] Image upload to object storage
- [ ] Optional VLM image descriptions

### 3.4 Editor uploads

- [ ] TipTap image upload integration
- [ ] Drag/drop and paste upload handlers

### 3.5 Bulk import

- [ ] ZIP import endpoint
- [ ] Indexing job enqueue per imported file
- [ ] Progress UI

## Phase 4 - CdA Features

**Goal:** add selected help-center features.

### 4.1 Schema

- [~] Raw SQL scripts exist for document views, reactions, favorites, API keys, webhooks, and metrics
- [ ] EF Core migrations for those scripts
- [ ] Refresh strategy for metrics

### 4.2 Endpoints

- [ ] Document view tracking endpoint
- [ ] Document reactions endpoint
- [ ] Favorites endpoint
- [ ] User favorites endpoint
- [ ] Analytics endpoints
- [ ] API key CRUD and middleware

### 4.3 UI

- [ ] Favorite control
- [ ] Document reactions
- [ ] My favorites
- [ ] Top-read landing
- [ ] Analytics dashboard

## Phase 5 - Quality

**Goal:** reliable RAG iteration and operational visibility.

### 5.1 RAGAS evals

- [x] `evals/golden.jsonl`
- [x] `evals/ragas_runner.py`
- [x] `evals/baseline_metrics.json`
- [x] `.github/workflows/eval.yml`
- [ ] Current CI execution status is not verified in this reconciliation

### 5.2 Conversational memory

- [~] `conversation_memory.py` and condenser prompts exist
- [~] `session_id` is accepted by FastAPI and stored in audit
- [ ] Condensation is not called by `ChatService.answer`
- [ ] Cache key does not use a condensed standalone question

### 5.3 Query rewrite

- [~] `query_rewrite.py` and rewriter prompts exist
- [ ] Tenant toggle
- [ ] Chat path integration

### 5.4 Citations with span

- [x] Alembic migration adds `text_quote` and `page_number`
- [ ] Runtime citation generation does not populate those fields yet
- [ ] Viewer highlighting is pending

### 5.5 OTel overlay

- [x] Compose observability overlay exists
- [ ] .NET OpenTelemetry SDK instrumentation
- [ ] FastAPI OpenTelemetry SDK instrumentation
- [ ] Grafana dashboards

### 5.6 Unresolved questions

- [x] Alembic migration adds `rag.unresolved_questions`
- [ ] Clustering job
- [ ] Management dashboard

### 5.7 Webhooks

- [~] Raw SQL script exists
- [ ] CRUD
- [ ] Delivery worker
- [ ] HMAC signature and retry behavior

## Phase 6 - Ship Readiness

- [ ] Lighthouse audit
- [ ] Load test
- [ ] Install guide
- [ ] Admin guide
- [ ] API reference
- [ ] Backup/restore scripts polished
