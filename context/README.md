# Context Index

This directory is the durable memory for the Advanced RAG Instruction Platform project. Files here are the **source of truth** for product behavior, technical decisions, and current state. Anything in chat that contradicts these files is wrong until the files are updated.

## Reading Order For A New Session

Read these in order. Each builds on the previous.

1. **`project-overview.md`** — what the product is, who uses it, success criteria.
2. **`architecture.md`** — how the system is built. The single source of truth for technical rules and invariants.
3. **`code-standards.md`** — library choices, versions, language policy, conventions per stack.
4. **`rag-spec.md`** — RAG-specific decisions (chunking, retrieval, generation, cache, `access_scope_hash`).
5. **`ui-context.md`** — UI direction, components, editor, streaming.
6. **`code-patterns.md`** — concrete reference patterns (error envelope, controllers, migrations, Caddyfile, compose, tests). Copy from here when implementing.
7. **`progress-tracker.md`** — current status, open questions, handoff for the next session.
8. **`design-decisions.md`** — chronological log of decisions with rationale. Read when you need to know *why* a rule exists.
9. **`ai-workflow-rules.md`** — process rules for working on this project with an AI assistant.

For deeper formal artifacts:

- **`../docs/superpowers/specs/2026-05-11-base-architecture-design.md`** — formal architecture spec. Mirrors `architecture.md` with additional narrative; refer to `architecture.md` first.
- **`../docs/superpowers/plans/2026-05-11-mvp-implementation-plan.md`** — the 18-task MVP implementation roadmap with scaffold commands, directory tree, and per-task verification. Read when starting any implementation work.

## Source-Of-Truth Rules

If two files seem to contradict, the precedence is:

1. `architecture.md` for technical/system rules and invariants.
2. `code-standards.md` for library/version/language choices.
3. `rag-spec.md` for everything RAG-specific.
4. `ui-context.md` for UI conventions.
5. `project-overview.md` for product behavior and scope.
6. `code-patterns.md` for shape of code (DTOs, signatures, file paths).
7. `docs/superpowers/specs/...` only fills gaps not covered above.

`design-decisions.md` and `progress-tracker.md` are history, not authority. If they conflict with the files above, update them, not the others.

## When Editing

- Update the **most authoritative** file for the kind of change you are making. Avoid restating in multiple files.
- New significant decisions also get an entry in `design-decisions.md` with rationale and tradeoffs.
- Update `progress-tracker.md` only with status, in-progress, next-up, open questions, and handoff. Do not duplicate architecture rules there.
- If you add a new top-level concern, add a row to this README's reading-order list.

## Quick Facts Snapshot

For fast reference. The authoritative location is the linked file.

| Fact | Value | Authority |
| --- | --- | --- |
| Deployment | Single-tenant Docker Compose per customer | `architecture.md` |
| Reverse proxy | Caddy with same-origin `/api/*` routing | `architecture.md` |
| Backend services | `.NET 8` (management API) + FastAPI (RAG) | `architecture.md` |
| Database | One Postgres per customer, schemas `app` + `rag`, pgvector | `architecture.md` |
| Frontends | 3 React apps: `manage`, `chat`, `docs` | `architecture.md` |
| Chat model | `gpt-4.1-nano` (configurable) | `rag-spec.md` |
| Embedding model | `text-embedding-3-small` @ 1536 native dims (configurable) | `rag-spec.md` |
| Default monthly AI budget | USD 5 per user | `architecture.md` |
| Semantic cache threshold | 0.90 cosine similarity | `rag-spec.md` |
| Semantic cache TTL | 24 hours | `rag-spec.md` |
| Chat token TTL | 15 minutes | `architecture.md` |
| Viewer access token TTL | 15 minutes | `architecture.md` |
| Viewer exchange code TTL | 60 seconds | `architecture.md` |
| Import upload limit | 10 MB per PDF/DOCX | `architecture.md` |
| End-user UI language | Spanish (es-AR) | `code-standards.md` |
| Code/comments/logs language | English | `code-standards.md` |
