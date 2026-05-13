# Progress Tracker

## Current Phase

- MVP implementation.

## Current Goal

- Start Task 1 infrastructure and configuration baseline using the human-in-the-loop workflow.

## Completed

- Read the initial prompt and existing context files.
- Confirmed the project is too large for one implementation unit and selected the first sub-project: complete base architecture.
- Selected the architecture approach: Monorepo Compose Product.
- Selected API contract detail level: define MVP contracts by contract group in the architecture spec and defer exhaustive endpoint DTO details to implementation planning.
- Approved MVP API contract groups: auth/session, users/groups, documents, viewer, chat/RAG, feedback/reporting, internal indexing, and health/errors.
- Approved system architecture section.
- Approved identity, roles, access model, and viewer access token approach.
- Captured current architecture decisions in `context/architecture.md`.
- Created `context/design-decisions.md` to avoid losing brainstorming decisions.
- Persisted the rule that meaningful decisions must be written to context files during brainstorming.
- Selected OpenAI MVP defaults: `gpt-4.1-mini` for chat and `text-embedding-3-large` for embeddings with configured dimension reduction to 1536 for pgvector compatibility.
- Selected secure browser session strategy: use `HttpOnly`, `Secure`, `SameSite` cookies with CSRF protection, and prohibit credential-bearing tokens in browser storage.
- Selected browser API topology: route frontend requests through same-origin `/api/*` Caddy paths with host-only `__Host-` cookies; avoid broad parent-domain cookies in the MVP.
- Selected FastAPI auth validation strategy: locally validate short-lived signed access tokens issued by .NET for chat requests instead of introspecting .NET on every chat request.
- Selected chat token refresh strategy: .NET issues/renews short-lived chat tokens from the main secure session through same-origin chat routes; FastAPI has no separate refresh token in the MVP.
- Selected viewer link strategy: use one-time 60-second exchange codes in document URLs and set real viewer access tokens only as host-only `HttpOnly` cookies for `docs.client.com`.
- Approved initial database entity direction: .NET owns `app` schema entities; FastAPI owns `rag` schema entities; RAG chunks store embeddings in the same table; simple feedback belongs on query audit events; citations are query audit child rows.
- Selected MVP document access model: group/department and attribute-based access only; no per-user document ACL exceptions.
- Added AI usage budget requirement: admins can configure per-user monthly monetary budgets, initially USD, and chat blocks new paid AI usage when the configured budget is reached.
- Selected AI budget enforcement scope: budget exhaustion blocks only new paid chat/RAG work, not authorized document viewing or management workflows.
- Selected over-budget cache behavior: over-budget users do not receive semantic cached answers in the MVP because cache lookup requires a paid embedding call.
- Selected default AI usage budget: USD 5 per user per month, configurable by administrators.
- Selected AI budget period: customer calendar month using the deployment's configured timezone.
- Approved MVP operational defaults for request lengths, log retention, token TTLs, semantic cache, rate limits, customer timezone, and AI usage budget.
- Selected MVP UI foundation: React TypeScript with Tailwind CSS, `shadcn/ui`, and `lucide-react` across the three frontends.
- Selected implementation planning decisions: local development uses `manage.localhost`, `chat.localhost`, and `docs.localhost`; management reporting reads FastAPI-owned `rag` schema views through .NET read-only access; same-user chat feedback updates the single feedback value.
- Wrote the formal base architecture design spec at `docs/superpowers/specs/2026-05-11-base-architecture-design.md`.
- Self-reviewed the formal design spec for unresolved markers, stale terms, and obvious contradictions.
- Wrote the detailed MVP implementation plan at `docs/superpowers/plans/2026-05-11-mvp-implementation-plan.md`.
- Self-reviewed the implementation plan for forbidden unresolved markers and ambiguity.
- Added a session handoff section so the project can resume safely after clearing the chat.
- Audited the context distribution and closed technical gaps before implementation:
  - Locked backend library choices (Serilog, FluentValidation, FluentAssertions, Testcontainers, SQLAlchemy 2.0 async, structlog, official OpenAI SDK, etc.) in `code-standards.md`.
  - Locked frontend stack (Vite, TanStack Query, Zustand, React Hook Form + Zod, TipTap, Vitest, Playwright) in `code-standards.md` and `ui-context.md`.
  - Defined the language policy (UI in es-AR, code/comments/logs/commits in English).
  - Created `rag-spec.md`: chunking, retrieval, generation, streaming SSE, cache, pgvector HNSW, `access_scope_hash` algorithm, indexing pipeline, cost/audit.
  - Closed auth fine-grained gaps in `architecture.md`: CSRF via AntiForgery synchronizer tokens, local HTTPS via Caddy internal CA, RS256 JWT with kid + JWKS, internal service token strategy.
  - Added an Operations section to `architecture.md`: postgres-init container, migration ordering via `depends_on: service_completed_successfully`, reporting views grants to `app_reporting_reader`, X-Request-ID propagation, GitHub Actions CI, pg_dump backup baseline.
  - Created `code-patterns.md` with concrete examples for the shared error envelope (3 stacks), thin .NET controller + service, FastAPI endpoint with deps, EF Core and Alembic migrations, xUnit/pytest/Vitest tests, full Caddyfile, docker-compose skeleton, .env.example, postgres init SQL, Serilog/structlog setup, and the RAG system prompt.
- Created `context/README.md` as the entry point with reading order and source-of-truth precedence.
- Removed duplication from `progress-tracker.md` (Architecture Decisions list now references the canonical files).
- User approved starting implementation from `docs/superpowers/plans/2026-05-11-mvp-implementation-plan.md`.
- User approved documenting a human-in-the-loop implementation protocol in `AGENTS.md`, `context/ai-workflow-rules.md`, `context/progress-tracker.md`, and `context/design-decisions.md`.
- User initialized Git with `git init`.
- Created `AGENTS.md` with project operating rules, source-of-truth pointers, and the human-in-the-loop implementation protocol.
- Added the human-in-the-loop implementation protocol to `context/ai-workflow-rules.md`.
- Asked the user to create and confirm the implementation branch with `git switch -c mvp-implementation` before Task 0 baseline edits.
- Verified that the active branch is still `master`; Task 0 implementation remains blocked until the user creates and confirms `mvp-implementation`.
- Clarified that the implementation branch name is `mvp-implementation` and gave the user the exact confirmation commands.
- Verified the active branch is now `mvp-implementation`.
- Created Task 0 root baseline files: `.gitignore`, `.editorconfig`, `.gitattributes`, `README.md`, `.node-version`, `package.json`, and `pnpm-workspace.yaml`.
- Fixed the implementation plan's documentation marker scan so it excludes the plan file itself and avoids self-matching its own verification command.
- Verified Task 0 baseline status with `git status --short`.
- Verified documentation marker scan with the corrected command; no unresolved marker matches were returned.
- Created the repository baseline commit with message `chore: establish repository baseline`.
- Marked Task 0 steps complete in the MVP implementation plan.

## In Progress

- Task 1 infrastructure and configuration baseline is next.

## Next Up

- Split Task 1 into `Agent-owned` and `User-owned` steps.
- Create Compose, Caddy, environment example, and local secret instructions.
- Ask the user to run Docker Compose syntax validation.

## Open Questions

- None for the current checkpoint.

## Architecture Decisions

See `context/architecture.md`, `context/code-standards.md`, `context/rag-spec.md`, `context/ui-context.md`, and `context/design-decisions.md`. This tracker no longer duplicates the decisions; those files are the source of truth.

## Session Notes

- Conversation can continue in Spanish, but project artifacts must stay in English.
- The formal design has been written to `docs/superpowers/specs/2026-05-11-base-architecture-design.md`.
- The implementation plan has been written to `docs/superpowers/plans/2026-05-11-mvp-implementation-plan.md`.
- The spec and plan are not committed yet because Git was initialized after they were written.
- The visual companion is running at `http://localhost:55187` in `.superpowers/brainstorm/32180-1778467523`.
- The next brainstorming section will close API/data boundaries before the dedicated UI visual pass because backend contracts constrain frontend workflow design.

## Handoff For Next Session

Start by reading `context/README.md`. It defines reading order and source-of-truth precedence. Then read, in order:

- `context/project-overview.md`
- `context/architecture.md`
- `context/code-standards.md`
- `context/rag-spec.md`
- `context/ui-context.md`
- `context/code-patterns.md`
- `context/progress-tracker.md` (this file)
- `context/design-decisions.md` (history; read on demand)
- `context/ai-workflow-rules.md`

Implementation artifacts (formal spec and 18-task plan) live at `docs/superpowers/specs/` and `docs/superpowers/plans/`.

Current state:

- The project is entering implementation kickoff.
- The formal architecture spec and MVP implementation plan have been written.
- The implementation plan has been approved for execution.
- Git has been initialized by the user.
- Human-in-the-loop implementation is now required for MVP execution.
- The implementation branch is `mvp-implementation`.
- The base architecture formal spec is written and approved as the basis for implementation: monorepo, Docker Compose, Caddy same-origin API routing, three React frontends, .NET management API, FastAPI RAG API, PostgreSQL with `app` and `rag` schemas, secure cookies, chat token flow, viewer exchange codes, document lifecycle, assisted imports, publishing blocked on successful indexing, semantic cache, AI usage budgets, audit, logs, secrets, health checks, operational defaults, UI foundation, and OpenAI model defaults.

Next safe implementation work:

- Begin Task 1 infrastructure and configuration baseline.
