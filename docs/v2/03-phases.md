# 03 — Phases & Tasks

The v2 refactor is broken into six phases. Each phase has a goal, a checklist, and a
"demoable outcome". Resume from the first un-ticked task.

> Legend: `[x]` done in this handoff session, `[~]` partial scaffold, `[ ]` not started.

---

## Phase 0 — Preparation

**Goal**: repository ready, dependencies installed (next PC), baseline runnable.

- [x] Re-init git on new working copy, baseline commit on `main`, working branch `feature/v2-generic`
- [x] Write `HANDOFF.md`
- [x] Scaffold `docs/v2/` and `docs/adr/`
- [x] Append v2 entries to `context/design-decisions.md`, create `context/v2-overview.md` and `context/v2-progress.md`
- [x] **On next PC** — `pnpm install`, `uv sync`, `dotnet restore`
- [x] **On next PC** — Bring up MVP baseline with `docker compose up -d --build`
- [x] **On next PC** — Run existing tests, record green/red baseline before applying v2 changes

---

## Phase 1 — Foundations generic (multilingual + multi-provider + i18n)

**Goal**: same MVP features but with all provider/locale assumptions pluggable.

### 1.1 Tenant config (singleton)
- [~] Migration `app.tenant_config` (file scaffolded, needs `dotnet ef migrations add` on next PC)
- [ ] Setup wizard endpoint extended to write tenant_config in one transaction
- [ ] `GET /api/v1/config` returns public-safe subset (no secrets); `PUT` admin-only
- [ ] Tests: setup happy path, validates LLM API key with a probe call

### 1.2 Embedding migration
- [x] Schema: change `rag.document_chunks.embedding` to `VECTOR(1024)`
- [~] Pass `dimensions=1024`; `OPENAI_EMBEDDING_MODEL` default remains pending OQ-002
- [ ] Reindex script + admin-triggered job
- [ ] Doc: `docs/operations/reindex.md`

### 1.3 Provider abstraction
- [~] `services/rag-api/src/advanced_rag/providers/base.py` — protocols
- [~] `openai_provider.py`, `anthropic_provider.py`, `azure_openai_provider.py`, `ollama_provider.py`
- [~] `tei_embedding_provider.py`, `tei_reranker_provider.py`, `cohere_reranker.py`
- [~] `factory.py` — reads `tenant_config`, instantiates correct providers
- [ ] Refactor `chat_service.py` to consume `ILlmProvider`; remove direct `AsyncOpenAI`
- [ ] Refactor `indexing_service.py` similarly
- [ ] Tests with fake providers for each implementation

### 1.4 i18n frontends
- [~] `react-i18next` installed (next PC) in all 3 SPAs
- [~] `apps/<spa>/src/i18n/{es-AR,en-US}.json` seeded
- [ ] Extract every literal string in `chat-web/src/**` and `docs-web/src/**`
- [ ] Extract every literal string in `manage-web/src/**`
- [ ] Locale picker in `<Header>` (shared-ui)
- [ ] Tests with `screen.getByText` using the translation key, not the literal

### 1.5 Multi-language prompts
- [ ] `services/rag-api/src/advanced_rag/rag/prompts/system_{es-AR,en-US,pt-BR}.md`
- [ ] Loader chooses based on detected/requested language
- [ ] Audit records `prompt_version` and `prompt_locale`

### 1.6 Token counting
- [ ] Replace `len(text.split())` with `tiktoken.encoding_for_model` in `chunking.py`
- [ ] Tests: known fixtures, byte-exact chunk boundaries

### 1.7 Retry/timeout wrapper
- [ ] `services/rag-api/src/advanced_rag/providers/_retry.py` with `tenacity` policies
- [ ] Apply to all `IEmbeddingProvider.embed`, `ILlmProvider.chat_*`, `IRerankerProvider.rerank`
- [ ] Tests: simulated 429, 503, timeout, ConnectionError

### 1.8 Branding (basic)
- [~] `shared-ui` theme provider reads `tenant_config` via `GET /api/v1/config`
- [~] Logo upload endpoint stores in MinIO `tenant/brand/`
- [ ] Setup wizard step for branding inputs
- [ ] Tests: custom brand_name appears in `<Header>`, `--brand-primary` overrides default

**Demoable outcome**: clean install → setup wizard → admin uploads 5 docs in ES + EN → chat
answers in language of question → logo customised.

---

## Phase 1.5 — Unified auth + design system base

**Goal**: a single session serves all 3 SPAs + base shared UI in place.

### 1.5.1 Roles
- [~] Migration adds `app.users.role` (text, default 'viewer')
- [ ] Backfill from existing user_roles table
- [ ] Deprecate `user_roles` and `roles` tables (compatibility view for 1 release)

### 1.5.2 Eliminate token flows
- [ ] Remove `POST /api/auth/chat-token` controller
- [ ] Remove `POST /api/viewer/exchange-*` controllers
- [ ] Remove `viewer_exchange_codes` table (migration drops it)
- [ ] Update Caddyfile to forward session cookie to FastAPI on chat routes

### 1.5.3 FastAPI cookie validation
- [ ] FastAPI middleware extracts session cookie → fetches/caches user claims
- [ ] Implementation choice (record in `open-questions.md`):
  - Option A: Caddy injects `X-User-Claims` from a Redis/Memcached shared with .NET
  - Option B: FastAPI calls `.NET /api/v1/session/validate` (one cached call per N sec/user)
  - Option C: .NET signs short-lived in-memory JWT mirroring session, set as another cookie
- [ ] Tests: invalid cookie → 401, valid cookie → user_id resolves

### 1.5.4 Endpoint authorisation
- [ ] All controllers updated to use `[Authorize(Roles="admin,editor")]` or `Roles="admin"`
- [ ] FastAPI dependency `require_role(min_role)` for chat/feedback endpoints
- [ ] Audit logs include `role_at_request`

### 1.5.5 `packages/shared-ui` scaffold
- [~] `packages/shared-ui/package.json` with workspace metadata
- [~] `src/styles/tokens.css` (light + dark)
- [~] `src/styles/fonts.css` (Inter via `@fontsource/inter`)
- [~] Hooks: `useTheme`, `useShortcut`
- [~] Layout: `AppShell`, `Sidebar`, `Header`
- [~] Inputs: `Button`, `Input`, `Textarea`, `Select`, `Switch`
- [ ] Inputs (rest): `Checkbox`, `RadioGroup`
- [ ] Overlays: `Dialog`, `HoverCard`, `Tooltip`, `Popover`, `DropdownMenu`, `Drawer`
- [ ] Data: `DataTable` (wraps TanStack Table), `Pagination`, `Badge`, `Avatar`
- [ ] Feedback: `Toast` (Sonner), `Skeleton`, `EmptyState`
- [ ] Markdown render: `Markdown` (`react-markdown` + `rehype-sanitize`)
- [ ] Chat: `ChatMessage`, `ChatComposer`, `ConversationList`, `CitationCard`, `CitationDrawer`
- [ ] Power: `CommandPalette` (cmdk), `DarkModeToggle`
- [ ] Each component has a vitest test colocated

### 1.5.6 Apply shared-ui to existing SPAs (shell only)
- [ ] Replace manage-web `<App>` shell with `<AppShell>` + `<Sidebar>` + `<Header>`
- [ ] Replace chat-web shell
- [ ] Replace docs-web shell
- [ ] Dark mode toggle visible in all 3
- [ ] Storybook (optional but recommended for `packages/shared-ui`)

**Demoable outcome**: login on `manage.localhost` → open `chat.localhost` in another tab,
no re-login → idem `docs.localhost` → dark mode toggle persists across all 3.

---

## Phase 1.7 — UX refactor per SPA

**Goal**: each SPA looks and feels like a pro product.

### 1.7.1 chat-web
- [ ] 3-pane `<AppShell>` (sidebar | main | right panel)
- [ ] `<ConversationList>` in sidebar; group by Today/Yesterday/Week/Older
- [ ] `<ChatMessage>` with markdown render, copy button, inline feedback
- [ ] `<ChatComposer>` autosize, Enter to send / Shift+Enter newline, filter chips
- [ ] Streaming cursor animation
- [ ] `<CitationCard>` with hover preview
- [ ] `<CitationDrawer>` right panel with `text_quote` highlight
- [ ] Cmd+K command palette wired
- [ ] Empty state when no conversations
- [ ] Tabular-nums for tokens/cost footer

### 1.7.2 docs-web
- [ ] Sidebar nav tree grouped by configured dimensions
- [ ] Right TOC with scroll-spy
- [ ] Breadcrumbs + previous/next
- [ ] Metrics panel (views, likes, favourites, citations)
- [ ] Footer "Was this helpful?" thumbs + comment
- [ ] Sticky favourite (heart) button
- [ ] Global search using shared command palette
- [ ] Sanitised HTML render with image lazy-loading + zoom
- [ ] Sticky-header tables, scrollable on mobile

### 1.7.3 manage-web
- [ ] Dashboard with KPI cards + charts (recharts)
- [ ] `DataTable` for documents with bulk actions
- [ ] Editor: TipTap with slash commands, autosave indicator
- [ ] Forms with React Hook Form + Zod + inline error UX
- [ ] Users table with role dropdown + bulk CSV import
- [ ] Dimensions editor with drag-to-reorder (@dnd-kit)
- [ ] Analytics page (queries, cost, top docs, unresolved questions)
- [ ] Feedback table with filters + CSV export (existing in MVP, polished)

**Demoable outcome**: side-by-side comparison video (MVP vs v2) shows the visual jump.

---

## Phase 2 — Hybrid retrieval + dimensions

**Goal**: state-of-the-art retrieval precision + flexible categorisation.

### 2.1 Dimensions schema
- [x] Migrations: `app.dimensions`, `app.dimension_values`, `app.document_dimension_values`
- [ ] EF Core entities + DbContext mappings
- [x] FastAPI cross-schema read grant for filter resolution

### 2.2 Dimensions CRUD
- [~] `DimensionsController.cs` (.NET) with CRUD + reorder
- [ ] Validation: unique keys, parent-id cycle detection
- [ ] manage-web pages: list + editor + assign-to-document

### 2.3 BM25 columns + indexes
- [~] Migration: `rag.document_chunks.content_tsv tsvector GENERATED ALWAYS AS ...`
- [~] Migration: GIN indexes for `content_tsv` and `content gin_trgm_ops`
- [ ] Re-chunking script for existing chunks (or document opt-in)

### 2.4 RRF combinator
- [~] `services/rag-api/src/advanced_rag/rag/hybrid_retrieval.py` with the RRF SQL
- [ ] Plug into `chat_service.py` retrieval step
- [ ] Audit: store `vector_top_k`, `bm25_top_k` per query
- [ ] Tests: golden set queries see ≥+10% nDCG vs vector-only

### 2.5 Reranker
- [~] `IRerankerProvider` interface + 3 implementations (BGE-TEI, Cohere, Voyage stub)
- [ ] Plug between RRF and final top-K in `chat_service.py`
- [ ] `tenant_config.enable_reranker` toggle
- [ ] Per-query `rerank=false` flag honored
- [ ] Tests with mocked reranker

### 2.6 Filters in chat
- [ ] `POST /api/v1/chat` body extended: `filters.dimensions: { key: value[] }`
- [ ] Deep-linking: chat-web reads `?modulo=IMA001` from URL
- [ ] Filter chips in `<ChatComposer>`
- [ ] Cache key hashes filters into `filter_hash`

**Demoable outcome**: query `IMA001` returns only docs tagged with that module; precision
metric on golden set ≥ specified baseline.

---

## Phase 3 — Object storage + bulk import

**Goal**: assets out of the DB; onboarding from a folder of PDFs is one click.

### 3.1 MinIO
- [~] `infra/compose/compose.yaml` adds `minio` service
- [~] `infra/compose/minio/init-bucket.sh` runs on first up
- [ ] Caddy `/storage/*` reverse-proxy with signed-URL injection
- [ ] `tenant_config.s3_endpoint` for non-MinIO deployments

### 3.2 Object storage abstraction
- [ ] `services/dotnet-api/Services/ObjectStorageService.cs` (S3 SDK)
- [ ] `services/rag-api/src/advanced_rag/storage/minio_storage.py`
- [ ] Filesystem fallback for dev

### 3.3 PDF/DOCX improvements
- [ ] Evaluate Unstructured.io vs PyMuPDF + python-docx (test fixtures provided)
- [ ] Extract tables as markdown
- [ ] Images uploaded to MinIO, replaced in HTML by URL
- [ ] Optional VLM (Claude 3.5 Sonnet) description of figures, embedded in chunk

### 3.4 Editor uploads
- [ ] TipTap image extension wires to MinIO upload
- [ ] Drag-and-drop + paste handlers

### 3.5 Bulk import
- [ ] `POST /api/v1/documents/bulk-import` accepts ZIP
- [ ] Enqueues one indexing job per file (uses existing `rag.indexing_jobs`)
- [ ] manage-web progress UI

**Demoable outcome**: zip of 100 PDFs → progress bar → indexed → MinIO populated; chunks
clean of base64.

---

## Phase 4 — CdA features

**Goal**: feature parity with the legacy CentroDeAyuda for end users.

### 4.1 Schema
- [~] Migrations: `document_views`, `document_reactions`, `document_favorites`
- [~] Materialised view `mv_document_metrics`
- [ ] Refresh schedule (nightly via pg_cron or external worker)

### 4.2 Endpoints
- [ ] `POST /api/v1/documents/{id}/view`
- [ ] `POST /api/v1/documents/{id}/reactions`
- [ ] `POST /api/v1/documents/{id}/favorite` (toggle)
- [ ] `GET /api/v1/users/me/favorites`
- [ ] `GET /api/v1/analytics/documents` (admin)
- [ ] `GET /api/v1/analytics/queries` (admin)
- [ ] `GET /api/v1/analytics/unresolved` (admin)

### 4.3 UI
- [ ] Favourite heart in docs-web
- [ ] Like/dislike in docs-web (independent of chat feedback)
- [ ] "My favourites" page in docs-web
- [ ] "Top read" landing in docs-web
- [ ] Analytics dashboard in manage-web

### 4.4 API keys
- [~] Migration: `app.api_keys`
- [ ] CRUD endpoints + UI (manage-web)
- [ ] `X-Api-Key` middleware in both .NET and FastAPI
- [ ] Per-key rate limiter (separate buckets from per-user)
- [ ] Per-key monthly budget USD

**Demoable outcome**: user marks favourites → sees them; admin sees top 10 most-viewed and
top 10 with negative reactions; API key works against `/api/v1/chat` without browser.

---

## Phase 5 — Quality (evals + observability + memory)

**Goal**: confident iteration loop.

### 5.1 RAGAS evals
- [~] `evals/golden.jsonl` seeded with 20 example Q&A
- [ ] `evals/ragas_runner.py`
- [ ] GitHub Actions workflow `eval.yml`
- [ ] CI fails on >5% regression vs baseline

### 5.2 Conversational memory
- [ ] `session_id` propagated client → server
- [ ] Condensation prompt in `prompts/condenser_<locale>.md`
- [ ] LLM-cheap (gpt-4o-mini / Haiku) reformulates with history
- [ ] Cache key uses the standalone (reformulated) question

### 5.3 Query rewrite (opt-in)
- [ ] `tenant_config.enable_query_rewrite` toggle
- [ ] LLM-cheap generates 1–3 reformulations
- [ ] Each rewrite contributes to RRF candidate pool

### 5.4 Citations with span
- [ ] LLM prompt instructs return of `text_quote` per citation
- [ ] Validator confirms quote is substring of chunk content
- [ ] Viewer highlights span on open

### 5.5 OTel overlay
- [~] `infra/compose/compose.observability.yaml` (otel-collector + tempo + prom + loki + grafana)
- [ ] .NET OTel instrumentation (`OpenTelemetry.Instrumentation.AspNetCore`)
- [ ] FastAPI OTel instrumentation (`opentelemetry-instrumentation-fastapi`)
- [ ] Grafana dashboards JSON in `infra/grafana/`

### 5.6 Unresolved questions
- [ ] Nightly job clusters audit events with no/low-quality answers
- [ ] Dashboard with cluster → suggest "create article on X"

### 5.7 Webhooks
- [ ] CRUD + outbound delivery worker
- [ ] HMAC signature, retry with backoff
- [ ] Events: `document.published`, `query.feedback.negative`, `query.answered`

**Demoable outcome**: RAGAS report attached to PRs; OTel traces visible in Grafana; multi-
turn conversation works end-to-end.

---

## Phase 6 — Ship readiness (optional, post-MVP-of-v2)

- [ ] Lighthouse audit ≥ 90 perf, ≥ 95 a11y across SPAs
- [ ] Load test with k6 (chat 100 RPS, p95 < 3s)
- [ ] Install guide `docs/install-guide.md`
- [ ] Admin guide `docs/admin-guide.md`
- [ ] API reference (OpenAPI generated)
- [ ] Backup/restore scripts polished
