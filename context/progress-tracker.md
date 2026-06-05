# Progress Tracker

## Current Phase

- MVP implementation and stabilization.

## Current Goal

- Stabilize `chat-web` polish after the simplified fixed-left-drawer redesign, while preserving persisted conversation behavior, SSE streaming, citation auditability, feedback, budget, and auth states.

## Completed

- On 2026-06-05, follow-up chat UI polish was implemented after user review. Duplicate cache-hit citations are deduplicated by document/version, answer citations now open from a deduplicated `Ver citas (n)` control into a fixed-height right drawer, feedback buttons can be selected or deselected before submission, the composer starts as a single-line autosizing textarea, chat login now uses the same auth-card stack and visible language/theme controls as management, the fixed left drawer shows active session user context, and chat UI mojibake around `caché`/session/login labels was corrected. Verification passed with `pnpm.cmd --dir packages\shared-ui test -- --run src/components/ChatComposer.test.tsx src/components/CitationDrawer.test.tsx`, `pnpm.cmd --dir apps\chat-web test -- --run src/App.test.tsx`, `uv run pytest tests/test_chat_rag.py -q`, `pnpm.cmd --dir apps\chat-web typecheck`, `pnpm.cmd --dir packages\shared-ui typecheck`, `pnpm.cmd --dir apps\chat-web build`, and `git diff --check` with Windows line-ending warnings only. The App test run still emits the existing Vitest warning about `--localstorage-file`.
- On 2026-06-05, the `chat-web` redesign was implemented after user approval without a separate written spec per explicit user request. The app now uses a fixed management-style left drawer for conversation history, new conversation, language selection, dark-mode toggle, and logout. The main chat area is simplified around the active transcript, usage, feedback, error states, answer-level citation access, and the bottom composer. Verification passed with `pnpm.cmd --dir apps\chat-web test -- --run src/App.test.tsx`, `pnpm.cmd --dir apps\chat-web typecheck`, `pnpm.cmd --dir apps\chat-web build`, and `git diff --check` with Windows line-ending warnings only. A local Vite server was started at `http://localhost:5174` for visual inspection; Compose/Caddy remains the right path for authenticated chat acceptance.
- The MVP implementation has progressed through the repository baseline, Compose/Caddy infrastructure, .NET API, FastAPI RAG service, React frontends, database migrations, authentication/session handling, users/groups/budgets, document lifecycle/imports, internal indexing, chat/RAG, feedback/reporting, document viewer, operational hardening, and E2E coverage.
- The product currently uses unified session-authenticated browser flows with document-id viewer locators; legacy browser chat-token and viewer-exchange runtime flows are removed from the active implementation.
- Role behavior is governed by `pruebas.md`: Viewers have limited management self-service access; DocumentManagers can inspect users and balances, create/edit groups, and assign user groups without Admin-only mutations; Admins retain full management authority.
- Runtime UI locales are limited to `es-AR` and `en-US`.
- Management role-matrix implementation and E2E coverage were added on 2026-05-31.
- Management Documents and TipTap editor i18n coverage was completed on 2026-05-31.
- On 2026-05-31, obsolete refactor documentation was removed from `docs/`, `context/`, and the root handoff file. Current implementation files and current context are now the project source of truth.
- On 2026-05-31, the first document image slice was implemented. Document image bytes are stored in private S3-compatible object storage, image metadata is stored in `app.document_images`, canonical HTML references stable `/api/document-images/{imageId}/content` URLs, and external/base64 image sources are rejected with `DOCUMENT_IMAGE_SOURCE_INVALID`.
- Verification for the first document image slice passed on 2026-05-31: focused .NET lifecycle, API, health, EF mapping/migration tests; `dotnet build services\dotnet-api\AdvancedRag.sln --no-restore`; `pnpm --dir apps\manage-web typecheck`; focused manage-web tests; manage-web production build; `docker compose --env-file infra/compose/.env.example -f infra/compose/compose.yaml config`; and `git diff --check` with line-ending warnings only.
- On 2026-06-01, user-owned local validation confirmed document images save correctly to MinIO and are visible in the MinIO browser.
- On 2026-06-01, the Raspberry Pi demo-host setup checkpoint was validated by the user: Ubuntu Server is running on `aarch64`, Docker Engine reports `arm64` server architecture, Docker Compose is installed, and UFW allows OpenSSH plus ports 80 and 443.
- On 2026-06-01, FastAPI text-first RAG image indexing was implemented. The chunker indexes accessible image text from saved HTML (`alt`, then `aria-label`, then `title`) plus nearby captions, without indexing image URLs, fetching object bytes, or using multimodal OpenAI image inputs. Verification passed with `uv run pytest tests/test_chunking.py tests/test_indexing.py -q`, `uv run ruff check .`, `uv run pytest -q`, and `uv run mypy src tests` from `services/rag-api`; repository `git diff --check` returned only line-ending warnings.
- On 2026-06-01, a management editor regression was fixed for published documents. Database inspection confirmed published documents had successful RAG indexing jobs and persisted chunks; the bug was isolated to `manage-web` initializing the editor only from `currentDraftVersion`, which is null after publication by design. The editor now falls back to `currentPublishedVersion` when no draft exists, so opening a published document shows the published title, metadata, and HTML content before creating the next draft. Verification passed with a red/green regression test, `pnpm.cmd --dir apps\manage-web typecheck`, `pnpm.cmd --dir apps\manage-web test -- --run`, `pnpm.cmd --dir apps\manage-web build`, and `git diff --check` with line-ending warnings only. A separate `pnpm.cmd --dir apps\manage-web lint` run remains blocked by pre-existing lint findings in untouched files: `ManagementNav.tsx`, `AuditPage.tsx`, and `ConfigurationPage.tsx`.
- On 2026-06-01, the user approved query-time multimodal RAG as the next image strategy. The approved design keeps textual retrieval first, attaches only a capped set of authorized images associated with retrieved chunks, fetches image bytes through a `.NET` internal endpoint, uses OpenAI Responses API for multimodal generation, and disables semantic cache writes for multimodal answers.
- On 2026-06-01, Caddy demo-host routing was adjusted to use `PUBLIC_DOMAIN` in the frontend site labels instead of hardcoded `.localhost` hosts, so a Raspberry Pi demo host can be reached from another machine through explicit DNS/hosts entries while preserving same-origin routing.
- On 2026-06-01, the Raspberry Pi demo stack was reachable from the Windows client through `manage.ragpi.lan`, `chat.ragpi.lan`, and `docs.ragpi.lan`. The user logged in with the seeded admin account across the services and changed the default password.
- On 2026-06-02, the user confirmed the Raspberry Pi demo stack works end to end after the admin password change.
- On 2026-06-02, the user confirmed ownership of `breuerai.com`; the Raspberry Pi demo should use `manage.breuerai.com`, `chat.breuerai.com`, and `docs.breuerai.com` through Cloudflare Tunnel.
- On 2026-06-03, Raspberry Pi + Cloudflare Tunnel demo deployment notes were documented in `docs/operations/raspberry-pi-cloudflare-demo.md`, including public hostnames, `cloudflared` setup, HTTP local origin routing, verification commands, and troubleshooting for Cloudflare 502/TLS origin errors.
- On 2026-06-03, an interim manual update helper was added at `updateService.sh` with documentation in `docs/operations/manual-service-update.md`. It performs a safe source-based update for the demo host until GHCR/CD is implemented: tracked-worktree guard, optional environment selection, Postgres backup, fast-forward-only pull, Compose validation, rebuild/recreate, health waits, and recent log output.
- On 2026-06-03, a host reboot recovery helper was added at `installServiceAutostart.sh` with documentation in `docs/operations/systemd-autostart.md`. It installs `advanced-rag.service` as a systemd oneshot unit that runs Docker Compose after Docker and network-online are available.
- On 2026-06-03, unauthenticated management surfaces gained visible language and dark-mode controls through the shared `AuthFrame`, including the login page. Login copy is now backed by `es-AR` and `en-US` i18n keys. Verification passed with the focused manage-web login-control test, `pnpm.cmd --dir apps\manage-web typecheck`, and `pnpm.cmd --dir apps\manage-web build`. A full `src/App.test.tsx` run remains blocked by two pre-existing authenticated language-switch tests that still render Spanish after selecting English.
- On 2026-06-03, the Raspberry Pi/demo host branch was realigned with `origin/mvp-implementation` after temporary local Caddy and file-permission commits were discarded from the deployment branch. The remaining local `backups/` directory is untracked deployment-host data and should be ignored locally through `.git/info/exclude` or a later repository-level `.gitignore` update.
- On 2026-06-04, document creation/editor fixes were implemented and agent-verified: management document editor quick group creation, richer TipTap toolbar and list/content styling, DOCX/PDF import responses with safe `contentHtml` plus fallback text, Mammoth `1.11.0` DOCX conversion without embedded image import, DB-backed one-time viewer session handoff codes, docs-web handoff consumption with URL cleanup, and viewer content list styling. Verification passed with focused .NET viewer/import/migration tests, .NET solution build, manage-web App tests/typecheck/build, docs-web App tests/typecheck/build, and `git diff --check` with line-ending warnings only. User-owned Compose/browser acceptance remains pending.
- On 2026-06-04, follow-up management editor usability fixes were implemented and agent-verified: document editor group creation now uses the same modal workflow as the users/groups workspace, access groups render in a compact three-column grid, a select-all groups action was added, the TipTap packages were refreshed from `3.23.5` to the npm latest `3.25.0`, and the toolbar was refit into the compact grouped layout with a native color input backed by the official TipTap color/text-style extensions plus a clear-color action. The document HTML sanitizer now preserves only the `color` CSS property and strips other inline CSS. Manage and chat document-viewer handoff links now open `docs-web` in a new tab with `noopener,noreferrer`. A Docker/Compose frozen-lockfile mismatch was corrected by regenerating the `apps/manage-web` pnpm lockfile importer so every TipTap specifier matches the exact `3.25.0` manifest entries. Verification passed with focused manage-web and chat-web App tests, full manage-web and chat-web App test files, `pnpm.cmd --dir apps\manage-web install --frozen-lockfile`, `pnpm.cmd --dir apps\manage-web typecheck`, `pnpm.cmd --dir apps\chat-web typecheck`, `pnpm.cmd --dir apps\manage-web build`, `pnpm.cmd --dir apps\chat-web build`, focused .NET sanitizer tests, and `dotnet build services\dotnet-api\AdvancedRag.sln --no-restore`.
- On 2026-06-04, a toolbar review follow-up refined the management TipTap toolbar without browser automation: the heading selector was replaced by fixed `P`, `H1`, `H2`, and `H3` buttons, semantic highlight and text-alignment controls were added, docs rendering now styles `<mark>`, and the document sanitizer preserves only approved `color` and `text-align` CSS plus `<mark>` while continuing to strip unsafe inline styles. Verification was intentionally limited per user request and passed with `pnpm.cmd --dir apps\manage-web install --frozen-lockfile`, `pnpm.cmd --dir apps\manage-web typecheck`, focused `DocumentHtmlSanitizerTests`, `pnpm.cmd --dir apps\manage-web build`, and `git diff --check` with line-ending warnings only.
- On 2026-06-04, Chat Conversation Slice A was implemented and agent-verified. FastAPI now exposes user-scoped `GET /api/chat/sessions` and `GET /api/chat/sessions/{session_id}`, persists `session_id`, `previous_event_id`, and `rewritten_question` on RAG audit rows, and rewrites follow-up questions from bounded same-user session history before embedding/retrieval/cache. `chat-web` now loads persisted conversations after session validation, renders selected transcripts, sends a stable `sessionId` on each chat request, and keeps citations/feedback/usage attached to the assistant turn that produced them. Verification passed with `uv run pytest tests/test_chat_rag.py tests/test_locale_support.py -q`, `uv run ruff check .`, `pnpm.cmd --dir apps\chat-web typecheck`, `pnpm.cmd --dir apps\chat-web test -- --run src/App.test.tsx`, `pnpm.cmd --dir apps\chat-web build`, and `git diff --check` with line-ending warnings only.
- On 2026-06-05, Chat Conversation Slice B was implemented and agent-verified. `chat-web` now consumes `/api/chat` SSE response bodies incrementally, renders `answer-token` deltas into the pending transcript turn before `done`, keeps citations, usage, feedback controls, and session-list refresh gated on stream completion, preserves a stable local turn key across the pending-to-completed transition, and removes partial pending turns if the SSE stream fails before `done`. Verification passed with `pnpm.cmd --dir apps\chat-web test -- --run src/App.test.tsx`, `pnpm.cmd --dir apps\chat-web typecheck`, `pnpm.cmd --dir apps\chat-web build`, and `git diff --check` with line-ending warnings only. The App test run still emits the existing Vitest warning about `--localstorage-file`.

## In Progress

- Query-time multimodal RAG design is approved and documented in `docs/superpowers/specs/2026-06-01-query-time-multimodal-rag-design.md`.
- Query-time multimodal RAG implementation plan is written in `docs/superpowers/plans/2026-06-01-query-time-multimodal-rag-implementation-plan.md`.
- Demo knowledge-library design is being drafted for a generic internal services company. The user selected this domain on 2026-06-03; final taxonomy, document set, permissions, and golden chat questions are pending approval before any seeding implementation.

## Next Up

- User-owned next: run chat browser acceptance by opening `chat.localhost` through the Compose/Caddy stack, confirming the fixed left drawer appears with history, new conversation, ES/EN, dark mode, logout, and active-session user context; ask two follow-up questions in one conversation; confirm answer text appears progressively on a longer response; open `Ver citas (n)` and confirm citations appear in the fixed right drawer without duplicate cards; confirm feedback buttons can be selected and deselected before submission; reload the page and confirm that the left drawer plus selected transcript persist. If the SSE stream drops mid-answer, confirm the partial answer is removed and a retryable error is shown.
- Agent-owned later: resume the query-time multimodal RAG plan slice by slice, starting with RAG schema/indexing for `chunk -> image_id` references.
- User-owned later: after implementation, run a local Compose/browser acceptance with a published document whose image contains visual evidence not repeated in surrounding text.
- User-owned later: copy or clone the repository to the Raspberry Pi demo host, create demo-only Compose secret files locally on that host, and run the Compose stack acceptance check.
- User-owned next: when manually updating the demo host before GHCR/CD, run `chmod +x updateService.sh` once and then `./updateService.sh --env-file infra/compose/.env.pi` from the repository root.
- User-owned next: to recover after host reboot, run `chmod +x installServiceAutostart.sh` once and then `./installServiceAutostart.sh --env-file infra/compose/.env.pi` on the Linux demo host; verify with `sudo systemctl status advanced-rag.service --no-pager`.
- User-owned next: on the demo host, ignore generated backup output locally with `echo "backups/" >> .git/info/exclude` and prevent Linux file-mode noise with `git config core.fileMode false`.
- User-owned next: finish verifying public access for `chat.breuerai.com` and `docs.breuerai.com`, then optionally add Cloudflare Access in front of `manage.breuerai.com` before showing the demo to untrusted users.
- User-owned later: run the local Compose/browser acceptance for document creation/editor fixes from `docs/superpowers/plans/2026-06-04-document-creation-editor-fixes-implementation-plan.md`, including confirmation that manage-to-docs and chat-to-docs viewer links open in a new browser tab.

## Open Questions

- Future cleanup may add orphan image cleanup for uploaded draft images that are removed from HTML before publication.
- Demo content design must still choose the final group/access taxonomy, document type taxonomy, initial published/draft/review mix, and the golden question set for chat validation.
- No open product decisions remain for the current document creation/editor fixes slice or its management editor usability follow-up. Browser/Compose acceptance, including the new-tab document handoff behavior, is still pending.

## Architecture Decisions

See `context/architecture.md`, `context/code-standards.md`, `context/rag-spec.md`, `context/ui-context.md`, and `context/design-decisions.md`. This tracker records status only.

## Session Notes

- Conversation can continue in Spanish, but project artifacts must stay in English.
- Obsolete refactor docs and branches should not be used to infer future product behavior.

## Handoff For Next Session

Start by reading `context/README.md`. It defines reading order and source-of-truth precedence. Then read, in order:

- `context/project-overview.md`
- `context/architecture.md`
- `context/code-standards.md`
- `context/rag-spec.md`
- `context/ui-context.md`
- `context/code-patterns.md`
- `context/progress-tracker.md`
- `context/design-decisions.md`
- `context/ai-workflow-rules.md`

Current state:

- The active branch is `mvp-implementation`.
- The latest local work implements Chat Conversation Slices A and B: persisted audit-backed chat sessions, user-scoped session list/history endpoints, bounded same-session question condensation, transcript-oriented `chat-web` with stable `sessionId` propagation, and progressive frontend SSE rendering with partial-answer rollback on interrupted streams. User-owned browser/Compose acceptance is pending.
- The previous local work implements document creation/editor fixes plus the follow-up management editor group-selection, TipTap `3.25.0` toolbar/color controls, and cross-app docs links opening in new tabs. Query-time multimodal RAG remains approved and its slice-based implementation plan is written.
- Document image slice 1 is implemented and the user confirmed image storage through the local MinIO browser on 2026-06-01.
- Text-first RAG image indexing is implemented and RAG verification passed on 2026-06-01.
- Management editor published-document fallback is implemented and manage-web verification passed on 2026-06-01.
- Query-time multimodal RAG is approved and planned for implementation. Do not send every document image to OpenAI; select only images associated with final retrieved chunks and enforce the initial caps of 3 images and 5 MB total image bytes per chat request.
- Interim manual service updates can use `updateService.sh` from the repository root on the Linux demo host. This is a temporary source-build path until GHCR/CD is implemented.
- Demo-host boot recovery can use `installServiceAutostart.sh` from the repository root on the Linux demo host to install `advanced-rag.service`.
- The demo host deployment branch should remain aligned with `origin/mvp-implementation`; host-only generated data such as `backups/` belongs outside tracked Git state.
