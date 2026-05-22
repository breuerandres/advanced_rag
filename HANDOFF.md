# Handoff — v2 Generic Refactor

This document is the single entry point for picking up the **v2 generic refactor** of the
Advanced RAG MVP. It is written for a new contributor (human or LLM) coming in **without
prior session context**.

> **Read order**
>
> 1. This file (skim once, top to bottom)
> 2. `docs/v2/README.md` (refactor index with phase-by-phase guide)
> 3. The relevant ADR(s) under `docs/adr/` for the area you are about to touch
> 4. The original context files in `context/` — these are still authoritative for whatever
>    has not been v2-superseded; see §3 below for the precedence rule
> 5. `context/v2-overview.md` — diff summary between MVP and v2

---

## 1. Why this refactor exists

The original `Advanced RAG MVP` was scoped as a single-tenant corporate document platform
for one specific customer (Mymtec / Dux3). The user decided to evolve it into a **generic,
self-hosted, multilingual help-center product that any company can install**.

The full design with reasoning, comparative analysis with three legacy systems
(`CentroDeAyuda`, `DuxFacturasApi`, DUX3 integration), and 16 closed design decisions lives
at:

```
C:\Users\andresbr\.claude\plans\en-este-directorio-hay-wise-whale.md
```

That document is **the source of truth** for product direction. Everything in `docs/v2/`
is a derivative or implementation projection of it.

The 16 closed decisions, summarised:

| # | Decision |
|---|---|
| 1 | Independent product (no migration from legacy CentroDeAyuda) |
| 2 | Single-tenant per company via Docker Compose |
| 3 | Content scope: HTML/Markdown articles + imported PDF/DOCX (no tickets/code) |
| 4 | Multi-provider LLM with an abstraction layer (`ILlmProvider` / `IEmbeddingProvider` / `IRerankerProvider`) |
| 5 | Categorisation: **configurable dimensions** (e.g. `product`, `area`, `module`) |
| 6 | Main UX is a **chat conversational with citations** |
| 7 | Port from CentroDeAyuda: favourites, view tracking + analytics, per-article reactions (NOT videos/courses) |
| 8 | Retrieval is **hybrid vector + BM25 + reranker** |
| 9 | **Multilingual** with a single embedding model (`text-embedding-3-large` truncated to 1024d, or `BGE-M3`) |
| 10 | Auth: local username/password **+ API keys** (no SSO/SAML in v1) |
| 11 | Object storage: **S3-compatible self-hosted (MinIO default)** |
| 12 | Chat accepts pre-loaded filters via URL/API (deep linking) |
| 13 | **Unified auth**: one session serves manage / chat / docs |
| 14 | UX direction: **Linear/Vercel-style minimalist professional** |
| 15 | Keep **3 separate SPAs**, only unify the session |
| 16 | UX features: dark mode, chat 3-pane layout, rich citation preview, command palette (Cmd+K) |

These supersede any conflicting rule in `context/architecture.md`, `context/rag-spec.md`,
`context/ui-context.md`, etc. The detailed mappings live in `context/v2-overview.md`.

---

## 2. Current state of this working copy

- Git repo was **re-initialized on this machine** because the original `.git` was not
  present in the working copy received here. Branch layout:
  - `main` — single baseline commit `chore: import existing MVP working tree as baseline`,
    representing the MVP at Task 17.5 completion.
  - `feature/v2-generic` — the working branch for the refactor. **All v2 commits go here.**
- The previous original repository's git history is **lost in this copy**. The MVP's
  task-by-task history is summarised in `context/progress-tracker.md` (461 lines, kept as
  reference). Treat that file as **read-only history**; v2 progress is tracked separately
  in `context/v2-progress.md`.
- No dependencies were installed on this machine. `node_modules/`, `.venv/`, `bin/`, `obj/`
  do not exist. The next PC must run `pnpm install`, `uv sync`, `dotnet restore`.
- No tests were executed here. Tests written during this refactor target the next PC.

### Transferring work to the next PC

Two options. The user's choice is recorded by the operator at handover time.

**Option A — Bundle**

```bash
# On this PC
git -C D:/advanced_rag-mvp-implementation bundle create v2-generic.bundle --all

# On the next PC (assuming an existing checkout with the original history)
git fetch ./v2-generic.bundle feature/v2-generic:feature/v2-generic
git merge --allow-unrelated-histories feature/v2-generic
# Resolve any conflict between the new baseline and the old history; v2 files take precedence.
```

**Option B — Patch series**

```bash
# On this PC
git -C D:/advanced_rag-mvp-implementation format-patch main..feature/v2-generic -o v2-patches/

# On the next PC
git checkout -b feature/v2-generic
git am v2-patches/*.patch
# If files differ from the baseline, `git am --3way` will help.
```

**Option C — Copy files**

If the next PC does not need granular history, simply copy the working tree on top of the
original repository, then `git checkout -b feature/v2-generic` and `git add -A && git
commit -m "feat: import v2 refactor working tree"`. This loses commit granularity.

The bundle is the safer default.

---

## 3. Precedence rule for documentation

When the new docs and the old MVP docs disagree, follow this order (high → low):

1. **`docs/v2/`** — anything in here is the **current** rule for the v2 refactor.
2. **`docs/adr/`** — decisions records, also v2-authoritative.
3. **`context/v2-overview.md`** — fast diff between MVP and v2 with cross-references.
4. **`context/architecture.md`, `rag-spec.md`, `code-standards.md`, `ui-context.md`** —
   still authoritative for anything not v2-superseded.
5. **`context/design-decisions.md`** — history with new v2 entries appended.
6. **`context/progress-tracker.md`** — frozen history of the MVP. **Do not edit.** v2
   progress goes to `context/v2-progress.md`.

If you find a contradiction that is not covered by `v2-overview.md`, add a row to it before
making the code change.

---

## 4. What was done in this handoff session

Everything in this session is design + scaffolding + documentation. **No backend service
was started, no tests were run, no dependencies were installed.** The next contributor
must install, run, and verify.

What you will find committed on `feature/v2-generic`:

| Area | Status | Where |
|---|---|---|
| Refactor design (master doc) | Complete | `C:\Users\andresbr\.claude\plans\en-este-directorio-hay-wise-whale.md` (outside repo) |
| Handoff docs | Complete | `HANDOFF.md`, `docs/v2/*.md`, `docs/adr/*.md` |
| Context updates | Complete | `context/v2-overview.md`, `context/v2-progress.md`, `context/design-decisions.md` (appended) |
| SQL migrations | Files written, **not applied** | `services/dotnet-api/...Migrations/...` (EF Core) and `services/rag-api/alembic/versions/...` |
| Provider abstraction | Files written, **not type-checked** | `services/rag-api/src/advanced_rag/providers/` |
| Hybrid retrieval | Files written, **not tested** | `services/rag-api/src/advanced_rag/rag/hybrid_retrieval.py`, `rerank.py` |
| Unified auth (server) | Files written, **not tested** | .NET controllers / FastAPI middleware |
| `packages/shared-ui` design system | Scaffold + tokens + first components | `packages/shared-ui/` |
| Chat-web / docs-web / manage-web refactor | Partial scaffolds at most | `apps/*/src/` |
| i18n | Files for ES + EN scaffolded | `apps/*/src/i18n/` |
| MinIO compose service | Stub | `infra/compose/compose.yaml` |
| Dimensions CRUD | Schema + endpoints | `services/dotnet-api/...` |
| CdA features (favourites/views/reactions) | Schema + endpoints | `services/dotnet-api/...` |
| API keys + rate limit | Schema + endpoints | `services/dotnet-api/...` |
| Setup wizard | Endpoint scaffolded | `services/dotnet-api/...` |
| RAGAS evals | Folder + golden seed | `evals/` |
| OTel | Compose overlay file | `infra/compose/compose.observability.yaml` |

Each commit on `feature/v2-generic` is scoped to one of the rows above. Read commits in
order — they were authored to be readable as a tutorial.

> **Important caveat.** This session prioritised written design over running code. Some of
> the implementation pieces are **scaffolds that will not compile yet** because related
> wiring (DI, dependencies in `pyproject.toml`, package.json entries) requires installing
> dependencies and running tooling. The next PC must do that. See §6 for the recommended
> first 90 minutes on the next PC.

---

## 5. Phase map (where we are, where we go)

The full phase breakdown is in `docs/v2/03-phases.md`. Short version:

```
Phase 0   Preparation                       (this session — DONE for design; init on next PC)
Phase 1   Foundations generic               (NOT STARTED end-to-end; partial scaffolds)
Phase 1.5 Unified auth + shared-ui          (NOT STARTED; scaffolds only)
Phase 1.7 UI refactor per SPA               (NOT STARTED; design done)
Phase 2   Hybrid retrieval + dimensions     (NOT STARTED; schema written)
Phase 3   Object storage + bulk import      (NOT STARTED; MinIO compose only)
Phase 4   CdA features                      (NOT STARTED; schema written)
Phase 5   Quality (evals + OTel + memory)   (NOT STARTED; folders scaffolded)
```

Each phase's checklist is in `docs/v2/03-phases.md`. **The next contributor should resume
from Phase 0.4** (install dependencies on the new machine) and then walk through Phase 1
linearly. See §6.

---

## 6. First 90 minutes on the next PC

1. Clone or import this work (see §2).
2. `cd advanced_rag-mvp-implementation`.
3. Install everything once:
   ```bash
   pnpm install
   cd services/rag-api && uv sync && cd ../..
   cd services/dotnet-api && dotnet restore && cd ../..
   pnpm --filter "./packages/shared-ui" install
   ```
4. Bring up the stack to confirm the **MVP baseline** still works after the v2 file
   additions (most files are additive; some endpoints were modified — see commit list):
   ```bash
   cd infra/compose
   docker compose --env-file .env.example -f compose.yaml up -d --build
   ```
5. Run unit tests:
   ```bash
   cd services/dotnet-api && dotnet test
   cd services/rag-api && uv run pytest -q
   pnpm -r test -- --run
   ```
   Expect some failures because of v2 partial scaffolds. Mark each failing area with a TODO
   referencing the relevant phase task in `docs/v2/03-phases.md`.
6. Read `docs/v2/README.md` end-to-end (15 min). Then jump to the first incomplete phase.

If you only have an hour and want maximum signal, do steps 1–4 and skim §1 and `docs/v2/README.md`.

---

## 7. What this handoff session did NOT do

- Did not run any test, build, or `docker compose up`.
- Did not install any dependency (`pnpm`, `uv`, `dotnet`, `pip`).
- Did not modify `pnpm-lock.yaml`, `uv.lock`, NuGet lock files — they were left untouched
  intentionally. The next PC's install will update them.
- Did not push to any remote. No `git remote` is configured.
- Did not create real secrets. `infra/compose/secrets/` is unchanged.
- Did not delete or rename any legacy MVP code. Removals deferred to Phase 1.5 onwards so
  the baseline remains runnable.

---

## 8. Communication channels & escalations

When the next contributor (LLM or human) hits something that needs the operator's
attention, the questions to ask are recorded in `docs/v2/open-questions.md`. Each entry is
tagged with the phase, owner, and what blocks until it is answered.

---

## 9. Anti-checklist (things easy to break)

These came up in the design phase and are worth re-reading before touching the relevant
code:

- **Embedding dimensions are part of the contract.** Schema is fixed at `VECTOR(1024)`. If
  you change `tenant_config.embedding_dimensions`, reindexing is mandatory; do not just
  update the column.
- **Auth changes break tests.** The MVP test suite relies on `chat-token` and viewer
  exchange codes. Phase 1.5 deletes both. Migrate the tests in the same commit, not later.
- **Cache key includes filters.** When you wire dimension filters into chat, the
  semantic-cache key must include a hash of the filters (`filter_hash`). Otherwise users
  with filter `modulo=A` will see cached answers from filter `modulo=B`.
- **HTML sanitisation runs twice.** `Ganss.Xss` in .NET on save + DOMPurify on render.
  Don't remove either thinking it's redundant — they defend different vectors.
- **Reranker latency is opt-out, not always-on by default.** It is on by default but per
  query a `rerank=false` flag is allowed. Don't make it unconditional.
- **i18n keys live in JSON, not in TS literals.** Never inline a Spanish string in a
  component once Phase 1 ships.
- **MinIO bucket URLs are signed.** Don't return permanent public URLs in API responses
  for protected docs.

---

## 10. Index of new files (cheatsheet)

For full breakdown see `docs/v2/04-file-map.md`. Highest-value entries:

```
HANDOFF.md                               this file
docs/v2/README.md                        v2 refactor index
docs/v2/01-current-state.md              MVP baseline snapshot
docs/v2/02-target-architecture.md        v2 target architecture
docs/v2/03-phases.md                     phase-by-phase task list
docs/v2/04-file-map.md                   inventory of created/modified files
docs/v2/open-questions.md                escalation queue
docs/adr/0001-multi-provider-llm.md
docs/adr/0002-hybrid-retrieval.md
docs/adr/0003-multilingual-embeddings.md
docs/adr/0004-postgres-bm25.md
docs/adr/0005-minio-object-storage.md
docs/adr/0006-unified-session-auth.md
docs/adr/0007-shared-ui-design-system.md
docs/adr/0008-configurable-dimensions.md
docs/adr/0009-conversational-memory.md
docs/adr/0010-ragas-evals.md
context/v2-overview.md                   MVP vs v2 diff
context/v2-progress.md                   v2-only progress tracker
```

Welcome aboard. The product spec is solid; the implementation is mostly mechanical from
here. — A.
