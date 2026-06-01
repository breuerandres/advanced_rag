# Progress Tracker

## Current Phase

- MVP implementation and stabilization.

## Current Goal

- Stabilize the first document image implementation slice: private MinIO/S3-compatible storage, Postgres metadata, stable app-controlled image URLs, editor upload, sanitizer enforcement, and Compose wiring.

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

## In Progress

- Local Compose/MinIO validation checkpoint is active for the first document image slice. Agent-owned static checks confirmed `Start-Local.ps1` parses, all required local secret files exist by name, and Compose configuration renders with `minio`, `minio-init`, S3 secrets, and `.NET` S3 runtime wiring. Browser validation is pending user-owned Compose startup output.
- MinIO startup failed locally because `minio_root_user.txt`, `minio_root_password.txt`, `s3_access_key.txt`, and `s3_secret_key.txt` existed as directories instead of secret files. `Start-Local.ps1` now validates required secrets with `-PathType Leaf`, and `New-LocalDevSecrets.ps1 -Overwrite` can replace accidental directories with generated local secret files. User-owned repair is still pending.
- The local Compose override now exposes the MinIO admin console on `127.0.0.1:9001` for developer-only inspection. Production Compose remains unexposed for MinIO admin and S3 ports.
- Slice 2 design is pending for RAG image-reference indexing and optional query-time OpenAI multimodal image inputs. The first slice does not fetch image bytes from FastAPI or send images to OpenAI.

## Next Up

- User-owned: confirm `infra/compose/secrets/openai_api_key.txt` contains a real local key, then run `.\infra\compose\Start-Local.ps1 -TrustCaddyCertificate` and report `docker compose ps -a` plus whether `minio-init` exits successfully.
- After Compose startup is confirmed, validate document image upload/rendering through the browser at `https://manage.localhost` and inspect the stable `/api/document-images/{imageId}/content` response path.
- Start slice 2 by deciding the RAG image-reference data model, retrieval caps, OpenAI image detail level, cache behavior, and query audit fields before implementation.

## Open Questions

- Slice 2 must decide whether OpenAI image inputs use Responses API or extend the current Chat Completions provider path.
- Slice 2 must define max images per query, max total image bytes per OpenAI request, image detail level, audit fields, and semantic-cache behavior for multimodal answers.
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
- The working tree has existing uncommitted implementation changes plus this documentation cleanup.
- Document image slice 1 is implemented but still needs local Compose/browser validation with real local secrets.
- User-owned local setup: confirm or create/update `openai_api_key.txt`, then run `.\infra\compose\Start-Local.ps1 -TrustCaddyCertificate`; report `docker compose ps -a` and whether `minio-init` exits successfully before deeper browser validation.
- Slice 2 handoff: keep FastAPI text-first until a design decision defines image-reference indexing, multimodal OpenAI request caps, query audit fields, and cache behavior. Do not send every document image to OpenAI.
