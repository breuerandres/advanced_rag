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
- On 2026-06-01, FastAPI text-first RAG image indexing was implemented. The chunker indexes accessible image text from saved HTML (`alt`, then `aria-label`, then `title`) plus nearby captions, without indexing image URLs, fetching object bytes, or using multimodal OpenAI image inputs. Verification passed with `uv run pytest tests/test_chunking.py tests/test_indexing.py -q`, `uv run ruff check .`, `uv run pytest -q`, and `uv run mypy src tests` from `services/rag-api`; repository `git diff --check` returned only line-ending warnings.
- On 2026-06-01, the user approved query-time multimodal RAG as the next image strategy. The approved design keeps textual retrieval first, attaches only a capped set of authorized images associated with retrieved chunks, fetches image bytes through a `.NET` internal endpoint, uses OpenAI Responses API for multimodal generation, and disables semantic cache writes for multimodal answers.

## In Progress

- Query-time multimodal RAG design is approved and documented in `docs/superpowers/specs/2026-06-01-query-time-multimodal-rag-design.md`.
- Implementation plan is pending.

## Next Up

- Agent-owned: write an implementation plan for query-time multimodal RAG, split into RAG schema/indexing, `.NET` internal image endpoint, FastAPI image fetch/selection, OpenAI Responses provider path, chat/audit/no-cache integration, and local acceptance.
- User-owned later: after implementation, run a local Compose/browser acceptance with a published document whose image contains visual evidence not repeated in surrounding text.

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
- The latest local work is the approved query-time multimodal RAG design and context update.
- Document image slice 1 is implemented and the user confirmed image storage through the local MinIO browser on 2026-06-01.
- Text-first RAG image indexing is implemented and RAG verification passed on 2026-06-01.
- Query-time multimodal RAG is approved for implementation. Do not send every document image to OpenAI; select only images associated with final retrieved chunks and enforce the initial caps of 3 images and 5 MB total image bytes per chat request.
