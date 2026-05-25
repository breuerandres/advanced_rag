# 04 - File Map

Inventory of v2 files that currently exist in the repository. Reconciled on 2026-05-25.

## Documentation

| Path | Status |
|---|---|
| `HANDOFF.md` | Reconciled v2 handoff/current-state entry point |
| `docs/v2/README.md` | v2 docs index |
| `docs/v2/01-current-state.md` | MVP baseline snapshot at v2 start |
| `docs/v2/02-target-architecture.md` | Target v2 architecture |
| `docs/v2/03-phases.md` | Current phase checklist |
| `docs/v2/04-file-map.md` | This file |
| `docs/v2/open-questions.md` | User decisions still open |
| `docs/adr/0001-multi-provider-llm.md` | Provider abstraction ADR |
| `docs/adr/0002-hybrid-retrieval.md` | Hybrid retrieval ADR |
| `docs/adr/0003-multilingual-embeddings.md` | Multilingual embeddings ADR |
| `docs/adr/0004-postgres-bm25.md` | Postgres BM25 ADR |
| `docs/adr/0005-minio-object-storage.md` | MinIO ADR |
| `docs/adr/0006-unified-session-auth.md` | Unified auth ADR |
| `docs/adr/0007-shared-ui-design-system.md` | Shared UI ADR |
| `docs/adr/0008-configurable-dimensions.md` | Dimensions ADR |
| `docs/adr/0009-conversational-memory.md` | Conversational memory ADR |
| `docs/adr/0010-ragas-evals.md` | RAGAS ADR |

## Context

| Path | Status |
|---|---|
| `context/v2-overview.md` | Fast diff between MVP and target v2 |
| `context/v2-progress.md` | Active v2 progress tracker |
| `context/design-decisions.md` | Contains v2 decision entries |
| `context/progress-tracker.md` | Historical MVP tracker; do not use as active v2 status |

## Shared UI

Existing files:

- `packages/shared-ui/package.json`
- `packages/shared-ui/tsconfig.json`
- `packages/shared-ui/src/index.ts`
- `packages/shared-ui/src/styles/tokens.css`
- `packages/shared-ui/src/styles/fonts.css`
- `packages/shared-ui/src/styles/globals.css`
- `packages/shared-ui/src/hooks/useTheme.ts`
- `packages/shared-ui/src/hooks/useShortcut.ts`
- `packages/shared-ui/src/lib/cn.ts`
- `packages/shared-ui/src/components/AppShell.tsx`
- `packages/shared-ui/src/components/Button.tsx`
- `packages/shared-ui/src/components/CommandPalette.tsx`
- `packages/shared-ui/src/components/DarkModeToggle.tsx`
- `packages/shared-ui/src/components/Header.tsx`
- `packages/shared-ui/src/components/LanguageSelect.tsx`
- `packages/shared-ui/src/components/Sidebar.tsx`

The pending shared components are listed in `packages/shared-ui/README.md` and
`docs/v2/03-phases.md`.

## Frontends

All three SPAs currently have:

- `src/i18n/index.ts`
- `src/i18n/es-AR.json`
- `src/i18n/en-US.json`
- `src/i18n/pt-BR.json`
- Shared UI style imports in `src/main.tsx`
- Shared shell controls in `src/App.tsx`

Additional current v2-related frontend files:

- `apps/manage-web/src/api/account.ts`
- `apps/manage-web/src/features/account/AccountPage.tsx`

Full literal extraction to i18n catalogs is not complete.

## .NET API

### Existing controllers

Current controller files under `services/dotnet-api/src/AdvancedRag.Api/Controllers/`:

- `AccountController.cs`
- `ApiControllerBase.cs`
- `AuditController.cs`
- `AuthController.cs`
- `ConfigurationController.cs`
- `DocumentsController.cs`
- `GroupsController.cs`
- `InternalSessionController.cs`
- `ReportingController.cs`
- `SetupController.cs`
- `UsersController.cs`
- `ViewerController.cs`

No `DimensionsController`, `ApiKeysController`, `AnalyticsController`, `WebhooksController`,
`ReactionsController`, `FavoritesController`, or `ViewsController` exists yet.

### Existing EF migrations

Current migration files under
`services/dotnet-api/src/AdvancedRag.Infrastructure/Migrations/`:

- `20260513184201_InitialAppSchema.cs`
- `20260517090000_AddDocumentVersionIndexingStatus.cs`
- `20260520170000_RenameLegacyContentStorageToDocuments.cs`
- `20260520173000_GrantRagOwnerAppReadAccess.cs`
- `20260520190000_RenameLegacyAuditEventsToDocuments.cs`
- `20260522134000_SeedDefaultAdminUser.cs`
- `20260522150000_AddConfigurableDimensions.cs`
- `AppDbContextModelSnapshot.cs`

Only `AddConfigurableDimensions` is a materialized v2 schema migration.

### Raw v2 SQL scripts

Raw scripts under `services/dotnet-api/v2-migrations-sql/`:

- `001_add_tenant_config`
- `002_add_user_role_column`
- `003_add_dimensions`
- `004_add_document_views`
- `005_add_document_reactions`
- `006_add_document_favorites`
- `007_add_api_keys`
- `008_add_webhooks`
- `009_add_document_language_summary`
- `010_add_document_versions_markdown`
- `011_drop_viewer_exchange_codes`
- `012_add_mv_document_metrics`
- `013_grant_v2_app_reads_to_rag_owner`

These scripts are not all materialized as EF Core migrations. `003_add_dimensions` has a
matching EF migration in `20260522150000_AddConfigurableDimensions.cs`.

## FastAPI RAG

### Provider abstraction

Existing files under `services/rag-api/src/advanced_rag/providers/`:

- `__init__.py`
- `_retry.py`
- `base.py`
- `factory.py`
- `openai_provider.py`
- `anthropic_provider.py`
- `azure_openai_provider.py`
- `ollama_provider.py`
- `tei_embedding_provider.py`
- `tei_reranker_provider.py`
- `cohere_reranker.py`

### RAG pipeline

Existing files:

- `services/rag-api/src/advanced_rag/auth/session_validation.py`
- `services/rag-api/src/advanced_rag/rag/answer_generator.py`
- `services/rag-api/src/advanced_rag/rag/chat_service.py`
- `services/rag-api/src/advanced_rag/rag/indexing_service.py`
- `services/rag-api/src/advanced_rag/rag/hybrid_retrieval.py`
- `services/rag-api/src/advanced_rag/rag/rerank.py`
- `services/rag-api/src/advanced_rag/rag/query_rewrite.py`
- `services/rag-api/src/advanced_rag/rag/conversation_memory.py`
- `services/rag-api/src/advanced_rag/rag/prompts/system_es-AR.md`
- `services/rag-api/src/advanced_rag/rag/prompts/system_en-US.md`
- `services/rag-api/src/advanced_rag/rag/prompts/system_pt-BR.md`
- `services/rag-api/src/advanced_rag/rag/prompts/condenser_es-AR.md`
- `services/rag-api/src/advanced_rag/rag/prompts/condenser_en-US.md`
- `services/rag-api/src/advanced_rag/rag/prompts/condenser_pt-BR.md`
- `services/rag-api/src/advanced_rag/rag/prompts/rewriter_es-AR.md`
- `services/rag-api/src/advanced_rag/rag/prompts/rewriter_en-US.md`
- `services/rag-api/src/advanced_rag/rag/prompts/rewriter_pt-BR.md`

`query_rewrite.py` and `conversation_memory.py` exist but are not called from
`ChatService.answer`.

### Alembic migrations

Current v2 migration files:

- `20260522_120000_v2_change_embedding_dimensions.py`
- `20260522_120100_v2_add_bm25_columns.py`
- `20260522_120200_v2_add_audit_session_fields.py`
- `20260522_120300_v2_add_citation_span.py`
- `20260522_120400_v2_add_unresolved_questions.py`
- `20260522_134100_seed_default_model_pricing.py`

## Infrastructure

Existing v2-related files:

- `infra/compose/compose.v2-extras.yaml`
- `infra/compose/compose.observability.yaml`
- `infra/compose/minio/init-bucket.sh`
- `infra/compose/observability/README.md`
- `infra/compose/observability/otel-collector.yaml`
- `infra/compose/observability/tempo.yaml`
- `infra/compose/observability/prometheus.yml`
- `infra/compose/observability/loki.yaml`
- `infra/compose/observability/grafana-datasources.yaml`

No application object-storage service implementation exists yet.

## Evals

Existing files:

- `evals/README.md`
- `evals/requirements.txt`
- `evals/golden.jsonl`
- `evals/baseline_metrics.json`
- `evals/ragas_runner.py`
- `.github/workflows/eval.yml`
