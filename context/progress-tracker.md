# Progress Tracker

## Current Phase

- MVP implementation and stabilization.

## Current Goal

- Stabilize the MinIO-backed document image flow and the first text-first RAG image indexing update.

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
- On 2026-06-01, FastAPI text-first RAG image indexing was implemented. The chunker indexes accessible image text from saved HTML (`alt`, then `aria-label`, then `title`) plus nearby captions, without indexing image URLs, fetching object bytes, or using multimodal OpenAI image inputs. Verification passed with `uv run pytest tests/test_chunking.py tests/test_indexing.py -q`, `uv run ruff check .`, `uv run pytest -q`, and `uv run mypy src tests` from `services/rag-api`; repository `git diff --check` returned only line-ending warnings.

## In Progress

- Text-first RAG image indexing needs a user-owned end-to-end acceptance pass against the local Compose stack: publish/index a document with a meaningful image description, then confirm chat can use that textual description when answering.
- Query-time multimodal image inputs remain intentionally unimplemented.

## Next Up

- User-owned: create or edit a document image with meaningful `alt` text/caption, publish it, wait for indexing to succeed, and ask chat a question that can only be answered from that image description.
- If direct visual reasoning is required later, define a separate multimodal slice before implementation: provider API path, retrieval caps, max total image bytes, image detail level, audit fields, cost behavior, and semantic-cache behavior.

## Open Questions

- Future multimodal slice must decide whether OpenAI image inputs use Responses API or extend the current Chat Completions provider path.
- Future multimodal slice must define max images per query, max total image bytes per OpenAI request, image detail level, audit fields, and semantic-cache behavior for multimodal answers.
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
- The latest local work is the text-first RAG image indexing slice and its context updates.
- Document image slice 1 is implemented and the user confirmed image storage through the local MinIO browser on 2026-06-01.
- Text-first RAG image indexing is implemented and RAG verification passed on 2026-06-01.
- Next user-owned acceptance: publish/index a document with meaningful image `alt` text/caption and confirm chat can answer from that textual description.
- Keep FastAPI text-first until a separate design decision defines multimodal image retrieval, OpenAI request caps, query audit fields, and cache behavior. Do not send every document image to OpenAI.
