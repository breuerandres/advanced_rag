# Advanced RAG Document Platform

Single-tenant corporate document-management and **Retrieval-Augmented Generation (RAG)**
platform. Customer staff create, review, publish, archive, and audit internal documents;
end users ask questions through a grounded chat that retrieves **only the published content
their role and access scope allow**, with inline citations, token-by-token streaming,
semantic caching, and per-user AI spend budgets.

Every customer runs an **isolated Docker Compose stack** with its own PostgreSQL database,
object storage, secrets, and logs.

> **Full technical documentation** — architecture, technology decisions, authentication
> system, and the complete RAG flow — lives in **[`docs/PROYECTO.md`](docs/PROYECTO.md)**
> (Spanish). Operational guides are under [`docs/operations`](docs/operations) and
> [`docs/runbooks`](docs/runbooks); local fixes in [`docs/troubleshooting.md`](docs/troubleshooting.md).

## Highlights

- **Two backends, two schemas, never cross-written.** A .NET 8 API owns the `app` schema
  (identity, document lifecycle, imports, audit); a FastAPI service owns the `rag` schema
  (retrieval, embeddings, chat, semantic cache, query audit).
- **Three single-purpose React frontends** — management, chat, and document viewer — each
  talking to its backend through **same-origin `/api/*` routes** behind Caddy.
- **Access-scoped retrieval.** Public chat retrieves only `Published` content matching the
  user's effective access scope (hierarchical organizational units + transverse groups).
- **Hybrid retrieval.** pgvector kNN (HNSW) + BM25/trigram fused with Reciprocal Rank
  Fusion, then a cross-encoder reranker, with the access + lifecycle filter pushed into SQL.
- **Streaming answers with citations**, query-time **multimodal** image grounding, and a
  scope-partitioned **semantic cache**.
- **Cost control.** Per-user monthly AI budgets, versioned model pricing, and per-query
  cost snapshots in the RAG query audit.

## Architecture

```
                                  Browser (HTTPS)
                                        │
                          ┌─────────────▼─────────────┐
                          │   Caddy — reverse proxy    │   manage. / chat. / docs.
                          │   TLS, same-origin /api/*  │   security headers
                          └─────────────┬─────────────┘
            ┌───────────────────────────┼───────────────────────────┐
            │                           │                            │
       manage-web                  chat-web / docs-web         (static React SPAs)
            │                           │
   /api/*  →  .NET        /api/chat*, /api/feedback*  →  rag-api (FastAPI, SSE)
                          /api/auth|session|csrf|viewer/*  →  .NET
            │                           │
   ┌────────▼─────────┐   internal token  ┌────────▼─────────┐
   │   dotnet-api      │  + per-request    │     rag-api       │
   │   (.NET 8)        │  session validate │    (FastAPI)      │
   │   schema: app     │◀─────────────────▶│    schema: rag    │
   └────────┬──────────┘                   └────────┬──────────┘
            │                                        │
            └───────────────┬────────────────────────┘
                            │
         ┌──────────────────▼───────────────────┐     ┌───────────────┐
         │  PostgreSQL 16 + pgvector             │     │  MinIO (S3)   │
         │  schemas:  app  |  rag                │     │  doc images   │
         └───────────────────────────────────────┘     └───────────────┘
                            │
                            ▼   OpenAI API — chat completion + embeddings
```

- Frontends never call across origins. `manage`/`docs` → .NET; `chat` → FastAPI for
  chat/feedback and .NET for auth/session/viewer links.
- `.NET → FastAPI` internal calls (indexing, cache invalidation, image bytes) travel over
  the Docker network with an `X-Internal-Service-Token`, never through Caddy.
- FastAPI authorizes every request by validating the browser `__Host-session` cookie
  against .NET's `/internal/session/validate` endpoint (no caching of access claims).

## Technology stack

| Area | Choices |
| --- | --- |
| Management API | .NET 8 (SDK 8.0.x), ASP.NET Core, EF Core, `Ganss.Xss`, SkiaSharp, Mammoth |
| RAG service | Python 3.12, FastAPI, SQLAlchemy 2 (async + asyncpg), Alembic, `openai`, PyJWT, structlog |
| Database | PostgreSQL 16 + pgvector (HNSW), schemas `app` and `rag` |
| Object storage | MinIO (S3-compatible) for document images |
| Frontends | React 18 + TypeScript, Vite, Tailwind CSS, `shadcn/ui`, lucide-react, TanStack Query, react-hook-form + zod, i18next, TipTap |
| AI models (default, configurable) | Chat `gpt-4.1-nano`; embeddings `text-embedding-3-small` @ 1024 dims |
| Reverse proxy | Caddy 2 (internal CA for local TLS) |
| Packaging | pnpm workspace (frontends), `dotnet` (.NET), `uv` (Python), Docker Compose |

## Repository layout

```
apps/
  manage-web/   # management frontend (.NET API)
  chat-web/     # chat frontend (FastAPI + .NET auth)
  docs-web/     # document viewer frontend
packages/
  shared-ui/    # shared React component library (@helpcenter/shared-ui)
services/
  dotnet-api/   # .NET 8 management API — owns the `app` schema
  rag-api/      # FastAPI RAG service — owns the `rag` schema
infra/
  compose/      # Caddyfile, compose.yaml, secrets, MinIO/Postgres init, scripts
docs/           # operations, runbooks, troubleshooting, and PROYECTO.md
```

## Local development

Requires Docker Desktop, Node.js + `pnpm`, the .NET 8 SDK, and `uv` (for the RAG service).
Backend integration tests use Testcontainers, so **Docker must be running** for them.

```powershell
# 1. Start the full stack behind Caddy (and trust the local CA on first run)
.\infra\compose\Start-Local.ps1 -TrustCaddyCertificate

# 2. Seed a demo hierarchy, users, and documents
.\infra\compose\Seed-LocalDemoData.ps1
```

The apps are then reachable at `https://manage.localhost`, `https://chat.localhost`, and
`https://docs.localhost`. The first seeded administrator is `admin@admin.com` / `admin` —
change it immediately outside throwaway local testing. Database migrations
(EF Core for `app`, Alembic for `rag`) run automatically on service startup.

Monorepo-wide commands:

```powershell
pnpm install            # install workspace deps
pnpm lint               # eslint across apps/packages
pnpm test               # vitest across apps/packages
pnpm typecheck          # tsc -b across apps/packages
pnpm build              # build all frontends
pnpm test:e2e           # Playwright E2E against the Compose stack
```

Per-service commands:

```powershell
# .NET management API (from repo root)
dotnet build services\dotnet-api\AdvancedRag.sln --no-restore
dotnet test  services\dotnet-api\AdvancedRag.sln

# FastAPI RAG service (from services\rag-api)
uv run pytest -q
uv run ruff check .
uv run mypy src tests
```

## Security model (summary)

- Sessions use an RS256-signed JWT in a `__Host-` cookie; signing keys rotate via a
  key store, and public keys are published at `/.well-known/jwks.json`.
- Mutating requests are protected by a signed double-submit CSRF token.
- Public chat retrieves only `Published` content; the document viewer revalidates access
  server-side (links are locators, not authorization); the session JWT never travels in a
  URL; cached answers are reused only for a matching `access_scope_hash`.
- Audit trails persist to PostgreSQL, not only to logs.

See [`docs/PROYECTO.md`](docs/PROYECTO.md) for the complete model.

## License

Proprietary — all rights reserved, unless a `LICENSE` file states otherwise.
