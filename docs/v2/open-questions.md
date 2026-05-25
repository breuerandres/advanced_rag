# Open Questions

These items need the user's input before they can be resolved. Each entry has:

- a short identifier
- the phase it blocks
- the question
- the options considered (with the current default)
- who needs to answer (usually the user)

Resolve by editing this file and moving the entry to a `## Resolved` section below with
the chosen option and a date stamp.

---

## OQ-001 — FastAPI cookie validation strategy
- **Phase**: 1.5
- **Blocks**: FastAPI session validation
- **Question**: How should FastAPI validate the `__Host-session` cookie set by .NET, given
  that FastAPI cannot per-request introspect .NET on every chat request without adding
  latency?
- **Options**:
  - **A** (default, recommended) — Caddy adds an `X-User-Claims` header by looking up the
    session cookie in a Redis cache populated by .NET on login/refresh. FastAPI trusts the
    header (Caddy is trusted infra). Cache TTL = same as session refresh interval (e.g. 5
    min). Failure mode: cache miss → 401, browser retries → .NET refreshes claims.
  - **B** — FastAPI calls `.NET /api/v1/session/validate` once and caches in-process (e.g.
    structlog-bound cache keyed by cookie hash) for 60 s. Simpler but adds an in-flight
    .NET dependency to every cold chat.
  - **C** — .NET signs a short-lived JWT mirroring the session and sets it as an extra
    cookie that FastAPI validates locally. Closest to MVP but reintroduces the chat-token
    pattern under a different name.
- **Recommended**: A. Decide before Phase 1.5.3.
- **Owner**: User

---

## OQ-002 — Embedding model default
- **Phase**: 1
- **Blocks**: Final shipped default and setup/config UX. It no longer blocks the 1024-d
  RAG schema migration, which already exists.
- **Question**: Default embedding model in the shipped `tenant_config`?
- **Options**:
  - **A** — `text-embedding-3-large` truncated to 1024d (cloud, OpenAI account needed)
  - **B** — `BGE-M3` via self-hosted TEI (no external account, requires GPU for sub-second
    latency)
  - **C** — Let setup wizard ask the operator
- **Recommended**: C — wizard asks, with A pre-selected. Decide before Phase 1.
- **Owner**: User

---

## OQ-003 — MinIO vs Garage
- **Phase**: 3
- **Blocks**: Compose service choice
- **Question**: Which S3-compatible store ships in `compose.yaml` by default?
- **Options**:
  - **A** (default in plan) — MinIO. Most mature, AGPL on the server (no copyleft on the
    consuming product because customers deploy it themselves).
  - **B** — Garage. Apache 2.0 simplifies the legal story for distribution. Smaller
    binary. Less mature ecosystem.
  - **C** — SeaweedFS. Apache 2.0 + high performance. More moving parts.
- **Recommended**: A (MinIO) for v1; Garage documented as drop-in alternative.
- **Owner**: User

---

## OQ-004 — Reindex on embedding change strategy
- **Phase**: 1
- **Blocks**: Phase 1.2 migration UX
- **Question**: When an admin changes `tenant_config.embedding_model` to one with a
  different dimension count, what should happen?
- **Options**:
  - **A** — Auto-trigger background reindex on save; chat fails-closed with
    `EMBEDDING_REINDEX_IN_PROGRESS` until complete
  - **B** — Require admin to click a separate "Apply and reindex" button after change
  - **C** — Block model change unless the new model has the same dimensions
- **Recommended**: B with progress UI.
- **Owner**: User

---

## OQ-005 — Reranker default behavior
- **Phase**: 2
- **Blocks**: Phase 2.5
- **Question**: Should reranker be on by default for new tenants?
- **Options**:
  - **A** — On by default (adds 50–150ms latency)
  - **B** — Off by default (admin enables after seeing baseline retrieval quality)
- **Recommended**: A, but with a one-click "disable reranker" if latency exceeds threshold.
- **Owner**: User

---

## OQ-006 — Conversational memory default
- **Phase**: 5
- **Blocks**: Phase 5.2
- **Question**: New tenants have multi-turn ON or OFF by default?
- **Options**:
  - **A** — ON (modern chat UX)
  - **B** — OFF (predictable single-turn behavior, easier debugging)
- **Recommended**: A.
- **Owner**: User

---

## OQ-007 — i18n locale strategy
- **Phase**: 1
- **Blocks**: Phase 1.4
- **Question**: How does the system decide a user's locale?
- **Options**:
  - **A** — From `users.locale` column (admin sets per user)
  - **B** — From browser `Accept-Language` header at login
  - **C** — From tenant default with per-session toggle
- **Recommended**: A with B fallback for first login, C for tenant-level default.
- **Owner**: User

---

## OQ-008 — Anthropic provider initial model
- **Phase**: 1
- **Blocks**: Enabling Anthropic as a configured runtime provider. The provider file exists,
  but the shipped default model choice is still undecided.
- **Question**: Which Claude model does the Anthropic provider default to?
- **Options**:
  - **A** — `claude-3-5-sonnet` (proven, broadly available)
  - **B** — `claude-4-sonnet` (current-gen, may not be GA for all customers)
- **Recommended**: A as starter, B documented as upgrade.
- **Owner**: User

---

## OQ-009 — VLM-described images opt-in
- **Phase**: 3
- **Blocks**: Phase 3.3
- **Question**: Should image-to-description (VLM) be on by default in the importer?
- **Options**:
  - **A** — On (better recall on docs with diagrams; adds cost)
  - **B** — Off (no extra cost; images preserved as inline references but not chunked)
- **Recommended**: B, with `tenant_config.enable_vlm_image_description` admin toggle.
- **Owner**: User

---

## OQ-010 — Webhook delivery worker
- **Phase**: 5
- **Blocks**: Phase 5.7
- **Question**: How are webhooks delivered?
- **Options**:
  - **A** — In-process .NET background service (BackgroundService)
  - **B** — Separate worker container in compose
  - **C** — Postgres-based queue with a poller
- **Recommended**: A; revisit if throughput grows.
- **Owner**: User

---

## Resolved

(Items move here once decided. Empty at handoff.)
