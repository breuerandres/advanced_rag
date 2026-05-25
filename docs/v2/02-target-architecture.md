# 02 — Target Architecture (After v2 Refactor)

This is what the system looks like once Phases 0 through 5 are complete. Use it as the
"north star" while implementing — every individual file you add or modify should bring the
system closer to this picture.

## 1. Component diagram

```
┌─────────────────────────────────────────────────────────────────────────┐
│  Caddy (reverse proxy + TLS local + same-origin /api/*)                 │
└──────────────┬──────────────────────────┬───────────────────────────────┘
               │                          │
               ▼                          ▼
       ┌─────────────────┐        ┌─────────────────┐
       │  manage-web     │        │  chat-web       │       ┌─────────────────┐
       │  (admin SPA)    │        │  (consumer SPA) │       │  docs-web       │
       │  shared-ui      │        │  shared-ui      │       │  (viewer SPA)   │
       │  i18n + dark    │        │  3-pane layout  │       │  shared-ui      │
       └────────┬────────┘        │  Cmd+K          │       │  TOC + browse   │
                │                 └────────┬────────┘       └────────┬────────┘
                │  /api/*                  │  /api/v1/chat/*          │  /api/v1/documents/*
                ▼                          ▼                          ▼
       ┌──────────────────────┐   ┌──────────────────────┐   (same .NET, same cookie)
       │  .NET API (mgmt)     │◀──┤  FastAPI RAG (query) │
       │  Auth, sessions,     │   │  Hybrid retrieval,   │
       │  CRUD, dimensions,   │   │  rerank, generation, │
       │  api-keys, webhooks, │   │  semantic cache,     │
       │  permissions,        │   │  query audit,        │
       │  setup wizard        │   │  feedback            │
       └──────────┬───────────┘   └──────────┬───────────┘
                  │                          │
                  ▼                          ▼
   ┌─────────────────────────────────────────────────────────────┐
   │  PostgreSQL 16  +  pgvector  +  pg_trgm  +  unaccent        │
   │  Schemas: app, rag                                          │
   └─────────────────────────────────────────────────────────────┘
                  │
       ┌──────────┴───────────┬────────────────────┬──────────────┐
       ▼                      ▼                    ▼              ▼
   ┌────────┐         ┌────────────┐        ┌──────────┐   ┌────────────┐
   │ MinIO  │         │  LLM       │        │  Embed   │   │  Reranker  │
   │ (S3)   │         │  Provider  │        │  Provider │   │  Provider  │
   └────────┘         └────────────┘        └──────────┘   └────────────┘
                       OpenAI |               OpenAI |        BGE local |
                       Anthropic |            BGE local        Cohere
                       Azure |
                       Ollama
                       
       ┌──────────────────────────────┐
       │  OTel Collector (opt-in)     │ ──▶ Tempo (traces)
       └──────────────────────────────┘ ──▶ Prometheus (metrics)
                                          ──▶ Loki (logs)
                                          ──▶ Grafana (dashboards)
```

## 2. Key invariants

These rules cannot be violated by any change without an ADR amendment.

1. `.NET` owns schema `app`. FastAPI owns schema `rag`. No cross-schema writes.
2. Embedding column is fixed at `VECTOR(1024)`. Provider changes that need a different
   dimension require reindex + migration.
3. The chunk text and its embedding live on the same row (`rag.document_chunks`).
4. Permissions are resolved at SQL level via `EXISTS` against `app.document_permissions`;
   the chat token never carries a precomputed document allowlist.
5. The session cookie `__Host-session` is the **only** browser auth artefact. Chat-token
   and viewer-exchange flows are removed.
6. Cache key includes `(corpus, access_scope_hash, question_embedding, filters_hash)`.
   Filter-aware partitioning prevents cross-filter leak.
7. UI strings live in `apps/<spa>/src/i18n/<locale>.json`, **never** inline in components.
8. Prompts live in `services/rag-api/src/advanced_rag/rag/prompts/system_<locale>.md` and
   carry a version number recorded in audit.
9. All paid LLM calls go through `ILlmProvider.chat_*` or `IEmbeddingProvider.embed`. No
   direct `AsyncOpenAI(...)` calls in business logic.
10. All images and binary assets live in MinIO. HTML stores relative paths
    (`/storage/<tenant>/<doc>/<asset>`); base64-inline is rejected by the sanitiser.

## 3. Data model (v2 additions over MVP)

See `services/rag-api/alembic/versions/` and `services/dotnet-api/src/AdvancedRag.Infrastructure/Migrations/`
for the SQL itself. The shape:

```
app.tenant_config             singleton; brand, llm/embedding choices, defaults
app.dimensions                {key, label, hierarchical, required}
app.dimension_values          {dimension_id, parent_id, external_key, label}
app.document_dimension_values M:N between documents and dimension values
app.documents                 + language, summary, external_key, deleted_at
app.document_versions         + content_format ('html'|'markdown'), content_markdown
app.document_views            view tracking
app.document_reactions        per-user ±1 reactions
app.document_favorites        per-user favorites
app.api_keys                  scoped + rate-limited
app.webhooks                  events queue
app.unresolved_questions      clustering + status
app.users.role                role column promoted to first-class
rag.document_chunks           + content_tsv tsvector, language
rag.query_audit_events        + session_id, previous_event_id, api_key_id, filters,
                              rerank_top_k, bm25_top_k, vector_top_k, reranker_model,
                              reranker_score
rag.query_audit_citations     + text_quote, page_number
app.mv_document_metrics       materialised view for analytics
```

## 4. Retrieval pipeline

```
Question
  ↓
[1] (Optional) Query rewrite (LLM cheap, tenant_config.enable_query_rewrite)
  ↓
[2] Embed multilingual via IEmbeddingProvider
  ↓
[3] Parallel:
    ├─ Vector kNN (HNSW, k=20)
    └─ BM25 (tsvector, k=20)
  ↓
[4] RRF fusion (k=60 fixed), top-30 to reranker
  ↓
[5] Cross-encoder rerank via IRerankerProvider, top-8 final
  ↓
[6] Context assembly + de-dup + token-budget trim
  ↓
[7] Conversational history condense (if session_id present)
  ↓
[8] LLM generate via ILlmProvider, structured citations, SSE stream
  ↓
[9] Post-process: validate chunk_id, extract text_quote, flag low-confidence
  ↓
[10] Cache write (if confident) + audit + webhook emit
```

## 5. Auth flow (unified)

```
Browser (manage|chat|docs)
  ↓ POST /api/auth/login
.NET issues __Host-session (HttpOnly, Secure, SameSite=Strict, Path=/)
  ↓
Subsequent requests from manage / chat / docs to .NET → cookie validated
Subsequent requests from chat to FastAPI:
  ↓
Caddy forwards cookie → FastAPI hashes the cookie value
  ↓
FastAPI cache miss → internal GET /internal/session/validate to .NET
                    with Cookie + X-Internal-Service-Token
  ↓
.NET validates session and active user, then returns safe chat claims
  ↓
FastAPI caches claims for 60s and uses them for role, groups, access_scope_hash, corpus

Role hierarchy: viewer < editor < admin
- viewer: chat + docs
- editor: + draft documents (chat + docs + manage)
- admin: + users, dimensions, api-keys, config
```

Implementation detail (OQ-001 resolved on 2026-05-25): FastAPI validates `__Host-session`
through an internal-only .NET validation endpoint and an in-process 60-second claims cache.
The endpoint is reachable only over the Docker network, requires `X-Internal-Service-Token`,
and returns only safe authorization inputs. Caddy/Redis claim injection was rejected because
the current Compose stack has no Redis/memcached service or Caddy auth plugin, and an extra
JWT cookie was rejected because it would recreate the MVP chat-token pattern.

## 6. Design system

`packages/shared-ui` is a pnpm workspace package consumed by the three SPAs. It exports:

- `tokens.css` — CSS variables for light + dark
- Layout: `AppShell`, `Sidebar`, `Header`, `Drawer`
- Inputs: `Button`, `Input`, `Textarea`, `Select`, `Checkbox`, `RadioGroup`, `Switch`
- Feedback: `Toast` (Sonner), `Skeleton`, `EmptyState`
- Overlays: `Dialog`, `HoverCard`, `Tooltip`, `Popover`, `DropdownMenu`
- Data: `DataTable` (TanStack Table), `Pagination`, `Badge`, `Avatar`
- Markdown: `Markdown` (react-markdown + rehype-sanitize)
- Chat: `ChatMessage`, `ChatComposer`, `ConversationList`, `CitationCard`, `CitationDrawer`
- Power: `CommandPalette` (cmdk), `DarkModeToggle`
- Hooks: `useTheme`, `useShortcut`, `useStreamingFetch`, `useApiClient`, `useTenantConfig`

All components are accessibility-checked, support dark mode through `[data-theme]`, and
honour `tenant_config.primary_color` via `--brand-primary` override.

## 7. Object storage

MinIO container in compose. Bucket layout:

```
helpcenter/
├── documents/<doc_id>/images/<uuid>.webp
├── documents/<doc_id>/imports/<original-name>.pdf   (kept for re-extract; behind admin flag)
├── tenant/brand/logo.{png,webp,svg}
└── tenant/brand/favicon.png
```

Access:
- Public read: tenant/brand/* (logo / favicon)
- Signed URL on demand: documents/* (5-minute signed GETs returned by .NET)

## 8. Configuration boundaries

| Where | Type of config |
|---|---|
| `infra/compose/.env.example` | Network ports, secret file paths, MinIO endpoint, OTel toggle |
| `infra/compose/secrets/*.txt` | Sensitive values (OpenAI key, JWT keys, internal token, S3 secret) |
| `app.tenant_config` row | Brand, LLM/embedding/reranker provider+model, defaults, feature toggles |
| Hardcoded in code | Schema version v1, contract groups, route paths |

Setup wizard writes once to `tenant_config`. Subsequent edits go through `PUT /api/v1/config`.

## 9. What stays the same as MVP

- Compose layout (postgres-init, dotnet-api, rag-api, manage-web, chat-web, docs-web,
  caddy, pg-backup)
- Caddy same-origin gateway with `/api/*`
- xUnit + pytest + Vitest + Playwright test stacks
- EF Core / Alembic split per schema
- Daily JSON logs to mounted volumes
- pg_dump backup container
- Health endpoints contract
- Shared error envelope `{ error: { code, message, details, requestId } }`
