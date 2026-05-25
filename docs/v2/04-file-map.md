# 04 — File Map

Inventory of files **created or modified** during the v2 refactor handoff session, with
their phase and intent. Use this as a fast index when something seems out of place.

## Root

| Path | New / Modified | Phase | Purpose |
|---|---|---|---|
| `HANDOFF.md` | New | 0 | Entry point for the next contributor |

## Documentation

| Path | New / Modified | Phase | Purpose |
|---|---|---|---|
| `docs/v2/README.md` | New | 0 | v2 docs index |
| `docs/v2/01-current-state.md` | New | 0 | MVP baseline summary |
| `docs/v2/02-target-architecture.md` | New | 0 | Target arch + invariants |
| `docs/v2/03-phases.md` | New | 0 | Phase-by-phase checklist |
| `docs/v2/04-file-map.md` | New | 0 | This file |
| `docs/v2/open-questions.md` | New | 0 | Escalation queue |
| `docs/adr/0001-multi-provider-llm.md` | New | 1 | LLM abstraction decision |
| `docs/adr/0002-hybrid-retrieval.md` | New | 2 | RRF + reranker decision |
| `docs/adr/0003-multilingual-embeddings.md` | New | 1 | Embedding model + 1024d |
| `docs/adr/0004-postgres-bm25.md` | New | 2 | tsvector instead of Elastic |
| `docs/adr/0005-minio-object-storage.md` | New | 3 | MinIO + alternatives |
| `docs/adr/0006-unified-session-auth.md` | New | 1.5 | Drop chat-token + viewer-exchange |
| `docs/adr/0007-shared-ui-design-system.md` | New | 1.5 | packages/shared-ui rationale |
| `docs/adr/0008-configurable-dimensions.md` | New | 2 | Dimensions vs tags vs hierarchy |
| `docs/adr/0009-conversational-memory.md` | New | 5 | Multi-turn condensation |
| `docs/adr/0010-ragas-evals.md` | New | 5 | Eval framework choice |

## Context

| Path | New / Modified | Phase | Purpose |
|---|---|---|---|
| `context/v2-overview.md` | New | 0 | MVP↔v2 diff with cross-refs |
| `context/v2-progress.md` | New | 0 | v2-only progress tracker |
| `context/design-decisions.md` | Appended | 0 | v2 decisions added in chronological log |
| `context/architecture.md` | (Not modified yet) | 1.5 | Phase 1.5 will append v2 auth section |
| `context/rag-spec.md` | (Not modified yet) | 1+2 | Phase 1 and 2 update retrieval+providers |
| `context/ui-context.md` | (Not modified yet) | 1.5+1.7 | Phase 1.5 will rewrite with shared-ui rules |

## packages/shared-ui (scaffolds)

| Path | New / Modified | Phase | Purpose |
|---|---|---|---|
| `packages/shared-ui/package.json` | New | 1.5 | Workspace pkg manifest |
| `packages/shared-ui/tsconfig.json` | New | 1.5 | TS config |
| `packages/shared-ui/src/index.ts` | New | 1.5 | Barrel exports |
| `packages/shared-ui/src/styles/tokens.css` | New | 1.5 | CSS variables light + dark |
| `packages/shared-ui/src/styles/fonts.css` | New | 1.5 | Inter font import |
| `packages/shared-ui/src/styles/globals.css` | New | 1.5 | Base resets |
| `packages/shared-ui/src/hooks/useTheme.ts` | New | 1.5 | Dark mode hook |
| `packages/shared-ui/src/hooks/useShortcut.ts` | New | 1.5 | Keyboard shortcuts |
| `packages/shared-ui/src/lib/cn.ts` | New | 1.5 | Class merge helper |
| `packages/shared-ui/src/components/Button.tsx` | New | 1.5 | Primary, secondary, ghost, danger variants |
| `packages/shared-ui/src/components/Input.tsx` | New | 1.5 | Text input |
| `packages/shared-ui/src/components/AppShell.tsx` | New | 1.5 | Layout shell |
| `packages/shared-ui/src/components/Sidebar.tsx` | New | 1.5 | Sidebar layout |
| `packages/shared-ui/src/components/Header.tsx` | New | 1.5 | Top header |
| `packages/shared-ui/src/components/DarkModeToggle.tsx` | New | 1.5 | Theme toggle |

(More components are scaffolded post-handoff; see `03-phases.md` Phase 1.5.5 for the
complete list.)

## Workspace

| Path | New / Modified | Phase | Purpose |
|---|---|---|---|
| `pnpm-workspace.yaml` | Modified | 1.5 | Adds `packages/*` glob |

## Backend

### .NET migrations (EF Core)

Files are written under
`services/dotnet-api/src/AdvancedRag.Infrastructure/Migrations/` with timestamp prefixes.
Each file pairs with a `.Designer.cs` snapshot.

| Migration | Phase | Purpose |
|---|---|---|
| `20260522_AddTenantConfig` | 1 | Tenant singleton with brand, providers, defaults |
| `20260522_AddUserRoleColumn` | 1.5 | Promote role from join table to first-class column |
| `20260522_DropChatTokenAndViewerExchange` | 1.5 | Remove deprecated auth artefacts |
| `20260522_AddDimensions` | 2 | dimensions + dimension_values + document_dimension_values |
| `20260522_AddDocumentMetrics` | 4 | document_views, document_reactions, document_favorites |
| `20260522_AddApiKeys` | 4 | API keys schema |
| `20260522_AddWebhooks` | 5 | Webhooks schema |
| `20260522_AddUnresolvedQuestions` | 5 | Question clustering schema |

### Alembic migrations (FastAPI)

`services/rag-api/alembic/versions/` (timestamped):

| Migration | Phase | Purpose |
|---|---|---|
| `20260522_change_embedding_to_1024.py` | 1 | Embedding dimension migration |
| `20260522_add_bm25_columns.py` | 2 | content_tsv + GIN indexes |
| `20260522_add_audit_session_filters.py` | 2 + 5 | session_id, filters, rerank fields |
| `20260522_add_citation_span.py` | 5 | text_quote, page_number |
| `20260522_add_materialized_metrics.py` | 4 | mv_document_metrics |

### FastAPI provider abstraction

| Path | Phase | Status |
|---|---|---|
| `services/rag-api/src/advanced_rag/providers/__init__.py` | 1 | Scaffolded |
| `services/rag-api/src/advanced_rag/providers/base.py` | 1 | Protocols |
| `services/rag-api/src/advanced_rag/providers/openai_provider.py` | 1 | Refactor of current code |
| `services/rag-api/src/advanced_rag/providers/anthropic_provider.py` | 1 | New |
| `services/rag-api/src/advanced_rag/providers/azure_openai_provider.py` | 1 | New |
| `services/rag-api/src/advanced_rag/providers/ollama_provider.py` | 1 | New |
| `services/rag-api/src/advanced_rag/providers/tei_embedding_provider.py` | 1 | New |
| `services/rag-api/src/advanced_rag/providers/tei_reranker_provider.py` | 2 | New |
| `services/rag-api/src/advanced_rag/providers/cohere_reranker.py` | 2 | New |
| `services/rag-api/src/advanced_rag/providers/factory.py` | 1 | Reads tenant_config |
| `services/rag-api/src/advanced_rag/providers/_retry.py` | 1 | Shared tenacity wrapper |

### RAG pipeline

| Path | Phase | Status |
|---|---|---|
| `services/rag-api/src/advanced_rag/rag/hybrid_retrieval.py` | 2 | RRF SQL |
| `services/rag-api/src/advanced_rag/rag/rerank.py` | 2 | Cross-encoder caller |
| `services/rag-api/src/advanced_rag/rag/query_rewrite.py` | 5 | Optional rewriter |
| `services/rag-api/src/advanced_rag/rag/conversation_memory.py` | 5 | Multi-turn condensation |
| `services/rag-api/src/advanced_rag/rag/prompts/system_es-AR.md` | 1 | Existing, renamed |
| `services/rag-api/src/advanced_rag/rag/prompts/system_en-US.md` | 1 | New |
| `services/rag-api/src/advanced_rag/rag/prompts/system_pt-BR.md` | 1 | New |
| `services/rag-api/src/advanced_rag/rag/prompts/condenser_es-AR.md` | 5 | New |
| `services/rag-api/src/advanced_rag/rag/prompts/condenser_en-US.md` | 5 | New |
| `services/rag-api/src/advanced_rag/rag/prompts/condenser_pt-BR.md` | 5 | New |

### .NET controllers

| Path | Phase | Status |
|---|---|---|
| `services/dotnet-api/src/AdvancedRag.Api/Controllers/DimensionsController.cs` | 2 | New |
| `services/dotnet-api/src/AdvancedRag.Api/Controllers/ReactionsController.cs` | 4 | New |
| `services/dotnet-api/src/AdvancedRag.Api/Controllers/FavoritesController.cs` | 4 | New |
| `services/dotnet-api/src/AdvancedRag.Api/Controllers/ViewsController.cs` | 4 | New |
| `services/dotnet-api/src/AdvancedRag.Api/Controllers/ApiKeysController.cs` | 4 | New |
| `services/dotnet-api/src/AdvancedRag.Api/Controllers/AnalyticsController.cs` | 4 | New |
| `services/dotnet-api/src/AdvancedRag.Api/Controllers/WebhooksController.cs` | 5 | New |
| `services/dotnet-api/src/AdvancedRag.Api/Controllers/ConfigController.cs` | 1 | New, replaces ConfigurationController |
| `services/dotnet-api/src/AdvancedRag.Api/Controllers/SetupController.cs` | 1 | Extended |
| `services/dotnet-api/src/AdvancedRag.Api/Controllers/AuthController.cs` | 1.5 | Remove chat-token method |
| `services/dotnet-api/src/AdvancedRag.Api/Controllers/ViewerController.cs` | 1.5 | DELETED (exchange-code flow removed) |

### Object storage

| Path | Phase |
|---|---|
| `services/dotnet-api/src/AdvancedRag.Infrastructure/Storage/MinioStorageService.cs` | 3 |
| `services/dotnet-api/src/AdvancedRag.App/Storage/IObjectStorage.cs` | 3 |
| `services/rag-api/src/advanced_rag/storage/__init__.py` | 3 |
| `services/rag-api/src/advanced_rag/storage/base.py` | 3 |
| `services/rag-api/src/advanced_rag/storage/minio_storage.py` | 3 |

## Infra

| Path | Phase | Purpose |
|---|---|---|
| `infra/compose/compose.yaml` | 3 | + minio + tei-embedding (opt) + tei-reranker (opt) |
| `infra/compose/compose.observability.yaml` | 5 | OTel overlay |
| `infra/compose/compose.gpu.yaml` | 1 | GPU overlay for TEI |
| `infra/compose/minio/init-bucket.sh` | 3 | Bucket bootstrap |
| `infra/compose/tei/embedding.yaml` | 1 | TEI config for embedding model |
| `infra/compose/tei/reranker.yaml` | 2 | TEI config for reranker |

## Frontends (per SPA)

Each SPA gets new files under `src/i18n/`, refactored `src/App.tsx`, new feature folders.
See `apps/manage-web/`, `apps/chat-web/`, `apps/docs-web/` directly — too many files to
itemise here.

## Evals

| Path | Phase | Purpose |
|---|---|---|
| `evals/golden.jsonl` | 5 | 20 Q&A starter set |
| `evals/ragas_runner.py` | 5 | CI entry point |
| `evals/README.md` | 5 | How to add cases |
| `.github/workflows/eval.yml` | 5 | CI trigger |
