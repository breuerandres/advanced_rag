# Progress Tracker

## Current Phase

- MVP implementation and stabilization.

## Current Goal

- Plan and implement query-time multimodal RAG for MinIO-backed document images.

## Completed

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

## In Progress

- Query-time multimodal RAG design is approved and documented in `docs/superpowers/specs/2026-06-01-query-time-multimodal-rag-design.md`.
- Query-time multimodal RAG implementation plan is written in `docs/superpowers/plans/2026-06-01-query-time-multimodal-rag-implementation-plan.md`.

## Next Up

- Agent-owned: implement the query-time multimodal RAG plan slice by slice, starting with RAG schema/indexing for `chunk -> image_id` references.
- User-owned later: after implementation, run a local Compose/browser acceptance with a published document whose image contains visual evidence not repeated in surrounding text.
- User-owned later: copy or clone the repository to the Raspberry Pi demo host, create demo-only Compose secret files locally on that host, and run the Compose stack acceptance check.
- User-owned next: when manually updating the demo host before GHCR/CD, run `chmod +x updateService.sh` once and then `./updateService.sh --env-file infra/compose/.env.pi` from the repository root.
- User-owned next: to recover after host reboot, run `chmod +x installServiceAutostart.sh` once and then `./installServiceAutostart.sh --env-file infra/compose/.env.pi` on the Linux demo host; verify with `sudo systemctl status advanced-rag.service --no-pager`.
- User-owned next: finish verifying public access for `chat.breuerai.com` and `docs.breuerai.com`, then optionally add Cloudflare Access in front of `manage.breuerai.com` before showing the demo to untrusted users.

## Open Questions

- Future cleanup may add orphan image cleanup for uploaded draft images that are removed from HTML before publication.

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
- The latest local work adds visible language and dark-mode controls to unauthenticated management surfaces, including login. Query-time multimodal RAG is approved and its slice-based implementation plan is written.
- Document image slice 1 is implemented and the user confirmed image storage through the local MinIO browser on 2026-06-01.
- Text-first RAG image indexing is implemented and RAG verification passed on 2026-06-01.
- Management editor published-document fallback is implemented and manage-web verification passed on 2026-06-01.
- Query-time multimodal RAG is approved and planned for implementation. Do not send every document image to OpenAI; select only images associated with final retrieved chunks and enforce the initial caps of 3 images and 5 MB total image bytes per chat request.
- Interim manual service updates can use `updateService.sh` from the repository root on the Linux demo host. This is a temporary source-build path until GHCR/CD is implemented.
- Demo-host boot recovery can use `installServiceAutostart.sh` from the repository root on the Linux demo host to install `advanced-rag.service`.
