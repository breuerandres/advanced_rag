# v2 Generic Refactor — Index

This folder is the v2 refactor's documentation home. Anything that **changes** an MVP
behavior, a contract, or a scope decision is documented here. Anything in `context/*.md`
that is **not** mentioned in `context/v2-overview.md` is unchanged.

## Read order

1. `HANDOFF.md` at the repository root — start here if you haven't.
2. `01-current-state.md` — snapshot of the MVP as it was when v2 started, distilled.
3. `02-target-architecture.md` — what the system looks like after Phase 5.
4. `03-phases.md` — phase-by-phase implementation checklist with file pointers.
5. `04-file-map.md` — inventory of files created or modified during this refactor.
6. `open-questions.md` — items that need the user's input before they can be resolved.

For decisions with reasoning and alternatives, see `../adr/000X-*.md`.

## TL;DR of the change

```
MVP (single-tenant, Mymtec-specific)        v2 (generic, multi-empresa)
────────────────────────────────────────    ──────────────────────────────────────
OpenAI hard-coded                       →   ILlmProvider + factory (OpenAI, Anthropic, Azure, Ollama)
text-embedding-3-small (1536d ES-only)  →   text-embedding-3-large @1024d OR BGE-M3 (multilingual)
Vector-only retrieval                   →   Hybrid (vector + BM25 + RRF + reranker)
Tags = none                             →   Configurable dimensions (M:N, hierarchical)
3 cookies (session/chat-token/viewer)   →   1 cookie (__Host-session) + roles
3 SPAs with own UX                      →   3 SPAs sharing packages/shared-ui (Linear/Vercel-style)
HTML editor base64 images                →   MinIO S3-compat with signed URLs
No multilingual                         →   ES-AR / EN-US / PT-BR (i18n)
No API keys                             →   API keys with scopes + rate limits
Per-doc views/likes/favorites: NO       →   Yes (ported from CentroDeAyuda)
No deep-link filters                    →   Chat accepts ?dim_x=val pre-loaded filters
No conversational memory                →   Multi-turn with session_id + condensation
No eval framework                       →   RAGAS continuous in CI
```

## How to make a change in this layout

| Type of change | Where it goes |
|---|---|
| Architectural rule | New entry in `docs/adr/`, reference from `02-target-architecture.md` |
| Schema change | Migration file under `services/*/migrations/`; reference in `03-phases.md` |
| New UI component | `packages/shared-ui/src/components/`; reference in `02-target-architecture.md` |
| Phase task done | Tick in `03-phases.md`; append to `context/v2-progress.md` with the evidence |
| New rule that supersedes MVP | Row in `context/v2-overview.md` + edit the appropriate `docs/v2/` doc |
| Open question for the user | New row in `open-questions.md` |

## Conventions

- Files in this folder are **English**, present-tense, descriptive.
- Cross-link liberally with relative paths. Use full repo paths
  (`services/rag-api/...`) instead of relative `../` paths so the docs are independent of
  where they're being read from.
- Date-stamp anything that is time-bound (e.g., "as of 2026-05-22, …").
- Keep code samples small and only when they communicate something prose can't.
