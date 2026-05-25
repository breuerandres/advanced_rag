# v2 Overview — What Changes vs the MVP

This file is the **fast diff** between the MVP (Tasks 0–17.5) and the v2 generic refactor.
Use it as a quick reference when reading `architecture.md`, `rag-spec.md`,
`code-standards.md`, or `ui-context.md`: anything not listed here is unchanged from those
files.

> **Precedence rule**: If this overview contradicts the older context files, **this
> overview wins** until those files are rewritten in Phase 1.5+.

## Master design document

The full design with reasoning, alternatives, and 16 closed product decisions is at:

```
C:\Users\andresbr\.claude\plans\en-este-directorio-hay-wise-whale.md
```

That file is outside the repo. Treat as the authoritative source of intent.

## Product-level diff

| Aspect | MVP | v2 |
|---|---|---|
| Tenancy | Single-tenant per customer | Same (single-tenant Compose), but **generic for any company** |
| Customer | Mymtec / DUX3 | Any |
| End-user UI | Spanish only | Spanish + English + Portuguese (Brazilian) |
| Login flow | Three tokens (session + chat-token + viewer-exchange) | One session cookie + roles |
| Pricing model | N/A in MVP | Self-hosted product, customer installs |
| Embedding | OpenAI text-embedding-3-small @ 1536 dims (ES-only) | Multilingual (text-embedding-3-large @ 1024d **or** BGE-M3) |
| LLM | OpenAI gpt-4.1-nano hard-coded | Pluggable: OpenAI, Anthropic, Azure, Ollama |
| Retrieval | Vector kNN only | Vector + BM25 + RRF + cross-encoder reranker |
| Categorisation | None (tags + groups) | Configurable dimensions (M:N, hierarchical) |
| Asset storage | Base64 inline in HTML | MinIO S3-compatible, signed URLs |
| Chat memory | Single-turn only | Multi-turn with session-scoped condensation |
| UI direction | Functional MVP, shadcn copies | Linear/Vercel minimalist professional, shared design system |
| Dark mode | None | Yes, with persistence |
| Cmd+K | None | Yes (cmdk library) |
| API keys | None | Yes (scoped + rate-limited) |
| Webhooks | None | Yes (`document.published`, etc.) |
| Observability | Structured logs only | OTel collector opt-in overlay (Tempo + Prom + Loki + Grafana) |
| Evals | None | RAGAS continuous in CI |
| Per-doc analytics | None | views, reactions, favorites materialized view |

## Architecture invariants — additions and changes

### Stays the same

- Caddy same-origin gateway
- .NET 8 + FastAPI service split, schema ownership (`app` / `rag`)
- Postgres single database per tenant
- pgvector + HNSW
- Shared error envelope `{ error: { code, message, details, requestId } }`
- xUnit / pytest / Vitest / Playwright stacks

### Changes

| Invariant | MVP | v2 |
|---|---|---|
| Embedding column | `VECTOR(1536)` | `VECTOR(1024)` (frozen) |
| Browser auth | `__Host-session` + `__Host-chat-token` + `__Host-viewer-token` | Only `__Host-session` |
| Role model | `Admin` / `DocumentManager` / `Viewer` (join table `user_roles`) | `admin` / `editor` / `viewer` (column on `app.users`) |
| Chunk text-search | Only embedding | Embedding + `content_tsv` + `content gin_trgm_ops` |
| Provider coupling | OpenAI imported directly | `ILlmProvider` / `IEmbeddingProvider` / `IRerankerProvider` |
| Cache key | `(corpus, access_scope_hash, question_embedding)` | + `filters_hash` |
| Audit | `session_id`, `previous_event_id`, `filters` columns added |
| Citations | `chunk_id`, `document_id`, `heading_path` | + `text_quote`, `page_number` |
| HTML images | Base64 inline | MinIO URLs only (sanitiser rejects base64) |
| Login flow | Endpoints for chat-token and viewer-exchange | Those endpoints **removed** |

## File-level diff (highlights)

| File | MVP | v2 |
|---|---|---|
| `context/architecture.md` | Authoritative on auth | Auth section to be rewritten in Phase 1.5 (see ADR-0006) |
| `context/rag-spec.md` | Retrieval section authoritative | Retrieval section to be rewritten in Phase 2 (see ADR-0002) |
| `context/code-standards.md` | OpenAI direct usage allowed | Forbid direct provider SDK calls outside `providers/` (ADR-0001) |
| `context/ui-context.md` | shadcn copies per app | Migrate to `packages/shared-ui` (ADR-0007) |
| `services/dotnet-api/.../AuthController.cs` | Has `chat-token` action | Action removed in Phase 1.5 (ADR-0006) |
| `services/dotnet-api/.../ViewerController.cs` | Exists | Deleted in Phase 1.5 (ADR-0006) |
| `services/rag-api/.../chat_service.py` | Direct `AsyncOpenAI` calls | Uses providers via factory |
| `services/rag-api/.../indexing_service.py` | Direct `AsyncOpenAI` calls | Uses providers via factory |
| `services/rag-api/.../chunking.py` | `len(text.split())` token count | `tiktoken.encoding_for_model(...)` |
| Frontend SPAs | shadcn local copies | `@helpcenter/shared-ui` imports |
| `pnpm-workspace.yaml` | `apps/*` | `apps/*` + `packages/*` |

## Where each ADR pins down a v2 rule

| Topic | ADR |
|---|---|
| LLM provider abstraction | 0001 |
| Hybrid retrieval (vector + BM25 + RRF + reranker) | 0002 |
| Multilingual embeddings + 1024-d schema | 0003 |
| BM25 in Postgres (no Elasticsearch) | 0004 |
| MinIO S3-compatible storage | 0005 |
| Unified session auth (drop chat-token + viewer-exchange) | 0006 |
| `packages/shared-ui` design system | 0007 |
| Configurable dimensions for categorisation | 0008 |
| Conversational memory (session_id + condensation) | 0009 |
| RAGAS continuous evals + CI gate | 0010 |

## How v2 was decided

User decisions captured in `HANDOFF.md` §1. Six structured prompts (`AskUserQuestion`)
during the design session resolved 16 product/architecture choices. All choices are
locked.

## Workflow rules during v2 implementation

- The previous "Human-In-The-Loop" rule in `context/ai-workflow-rules.md` is **relaxed**
  for the v2 refactor session: the user explicitly authorised "do all implementations"
  with the exception that no tests are run and no dependencies are installed on the
  current PC. Continue scoped commits but do not block on operator-owned checkpoints
  until the next PC.
- All new docs and code comments are **English**. UI strings remain in i18n JSON files
  with `es-AR`, `en-US`, `pt-BR` keys.
- All new tests live in their service's existing test directory; they target the next
  PC's `pnpm test` / `pytest` / `dotnet test`.
- Per-phase commits on `feature/v2-generic` are the unit of progress. Each commit is
  authored to be readable as a tutorial step.
