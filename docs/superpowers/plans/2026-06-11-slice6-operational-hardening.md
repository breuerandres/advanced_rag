# Slice 6: Operational Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Keep the rag schema healthy under sustained production load: purge dead chunks and expired cache entries, and index the per-request budget query.

**Architecture:** A maintenance service with two purge operations, exposed through a token-protected internal endpoint (`POST /internal/maintenance/purge`) so operators can schedule it with host cron / a Compose sidecar — consistent with the project's "internal endpoints over background daemons" MVP stance. One Alembic migration adds the budget-query index.

**Tech Stack:** FastAPI, SQLAlchemy async, Alembic, pytest + Testcontainers.

**Depends on:** Slice 1 (deactivation by document+corpus is what produces purgeable rows safely).

---

## Background For A Zero-Context Engineer

- `rag.document_chunks` rows flipped to `is_active = false` (superseded versions, re-indexes) are never deleted. Dead vectors bloat the table and, until vacuum, the HNSW graphs.
- `rag.semantic_cache_entries` rows past `expires_at` are filtered in lookups but never deleted.
- The budget check (`ChatService._enforce_budget`) runs `select coalesce(sum(estimated_cost_usd),0) from rag.query_audit_events where user_id = :u and created_at >= :start and created_at < :end` on **every chat request**; the only index is `ix_query_audit_events_created_at`.
- Internal-endpoint pattern to copy: `services/rag-api/src/advanced_rag/api/routers/indexing.py` (token check via `settings.resolved_internal_service_token`).
- Error-code catalog lives in `context/code-patterns.md`; this slice adds no new public codes (reuses `AUTH_INTERNAL_TOKEN_INVALID`).

## File Map

- Create: `services/rag-api/src/advanced_rag/rag/maintenance_service.py`
- Create: `services/rag-api/src/advanced_rag/api/routers/maintenance.py`
- Modify: `services/rag-api/src/advanced_rag/main.py` (router + service wiring, mirroring `internal_indexing_service`)
- Modify: `services/rag-api/src/advanced_rag/core/config.py`
- Create: `services/rag-api/alembic/versions/<next_id>_budget_query_index.py`
- Create: `services/rag-api/tests/test_maintenance.py`
- Modify: `services/rag-api/tests/test_migrations.py`
- Create: `docs/runbooks/rag-maintenance.md`

---

## Task 1: Budget Query Index

**Files:**
- Create: `services/rag-api/alembic/versions/<next_id>_budget_query_index.py` (confirm head with `uv run alembic heads`)
- Modify: `services/rag-api/tests/test_migrations.py`

- [ ] **Step 1: Failing migration test** — assert index `ix_query_audit_events_user_created` exists after upgrade.

- [ ] **Step 2: Migration**

```python
def upgrade() -> None:
    op.create_index(
        "ix_query_audit_events_user_created",
        "query_audit_events",
        ["user_id", "created_at"],
        schema="rag",
    )


def downgrade() -> None:
    op.drop_index("ix_query_audit_events_user_created", table_name="query_audit_events", schema="rag")
```

- [ ] **Step 3: Run** `uv run pytest tests/test_migrations.py -q` → PASS. **Commit:**

```bash
git add services/rag-api
git commit -m "perf(rag): composite index for per-user budget aggregation"
```

## Task 2: Maintenance Service

**Files:**
- Create: `services/rag-api/src/advanced_rag/rag/maintenance_service.py`
- Create: `services/rag-api/tests/test_maintenance.py`
- Modify: `services/rag-api/src/advanced_rag/core/config.py`

- [ ] **Step 1: Add setting**

```python
rag_inactive_chunk_retention_days: int = 7
```

- [ ] **Step 2: Failing tests**

`tests/test_maintenance.py` (Testcontainers fixture like `test_indexing.py`):

```python
async def test_purge_deletes_old_inactive_chunks_only(...):
    # Arrange: insert chunks: (a) is_active=false, created_at = now()-10d
    #          (b) is_active=false, created_at = now()-1d
    #          (c) is_active=true,  created_at = now()-10d
    # Act: MaintenanceService.purge(retention_days=7)
    # Assert: (a) deleted; (b) and (c) remain; result.inactive_chunks_deleted == 1.

async def test_purge_deletes_expired_cache_entries(...):
    # Arrange: one cache entry expires_at in the past, one in the future
    # (insert rows directly; semantic_cache_sources rows for both).
    # Act: purge.
    # Assert: expired entry and its sources gone; live entry intact;
    #         result.cache_entries_deleted == 1.
```

- [ ] **Step 3: Implement**

```python
from __future__ import annotations

from pydantic import BaseModel, ConfigDict
from sqlalchemy import text
from sqlalchemy.ext.asyncio import AsyncSession, async_sessionmaker


class PurgeResult(BaseModel):
    model_config = ConfigDict(frozen=True)

    inactive_chunks_deleted: int
    cache_entries_deleted: int


class MaintenanceService:
    """Periodic hygiene for the rag schema, invoked via the internal API."""

    def __init__(self, session_factory: async_sessionmaker[AsyncSession]) -> None:
        self._session_factory = session_factory

    async def purge(self, *, retention_days: int) -> PurgeResult:
        async with self._session_factory() as session:
            chunks = await session.execute(
                text(
                    """
                    delete from rag.document_chunks
                    where is_active = false
                      and created_at < now() - make_interval(days => :retention_days)
                    """
                ),
                {"retention_days": retention_days},
            )
            cache = await session.execute(
                text("delete from rag.semantic_cache_entries where expires_at <= now()")
            )
            await session.commit()
        return PurgeResult(
            inactive_chunks_deleted=int(getattr(chunks, "rowcount", 0) or 0),
            cache_entries_deleted=int(getattr(cache, "rowcount", 0) or 0),
        )
```

(`rag.semantic_cache_sources` rows cascade with their entry — verify the FK is `on delete cascade` in `20260513_184500_initial_rag_schema.py`; if it is not, delete sources first with an explicit statement.)

- [ ] **Step 4: Run** `uv run pytest tests/test_maintenance.py -q` → PASS. **Commit:**

```bash
git add services/rag-api
git commit -m "feat(rag): maintenance service purging inactive chunks and expired cache entries"
```

## Task 3: Internal Endpoint And Wiring

**Files:**
- Create: `services/rag-api/src/advanced_rag/api/routers/maintenance.py`
- Modify: `services/rag-api/src/advanced_rag/main.py`
- Modify: `services/rag-api/tests/test_maintenance.py`

- [ ] **Step 1: Failing endpoint test** (httpx/TestClient pattern used by existing router tests): POST without token → 401 `AUTH_INTERNAL_TOKEN_INVALID`; with token → 200 `{"inactiveChunksDeleted": N, "cacheEntriesDeleted": M}`.

- [ ] **Step 2: Implement the router** (copy the token check from `routers/indexing.py`):

```python
from fastapi import APIRouter, Header, Request
from pydantic import BaseModel, ConfigDict, Field

from advanced_rag.core.errors import ApiException
from advanced_rag.rag.maintenance_service import MaintenanceService

router = APIRouter(prefix="/internal/maintenance", tags=["internal-maintenance"])


class PurgeResponse(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    inactive_chunks_deleted: int = Field(serialization_alias="inactiveChunksDeleted")
    cache_entries_deleted: int = Field(serialization_alias="cacheEntriesDeleted")


@router.post("/purge", response_model=PurgeResponse, response_model_by_alias=True)
async def purge(
    request: Request,
    internal_service_token: str | None = Header(default=None, alias="X-Internal-Service-Token"),
) -> PurgeResponse:
    settings = request.app.state.settings
    expected_token = settings.resolved_internal_service_token
    if not expected_token or internal_service_token != expected_token:
        raise ApiException("AUTH_INTERNAL_TOKEN_INVALID", 401, "Internal service token is invalid.")
    service: MaintenanceService = request.app.state.maintenance_service
    result = await service.purge(retention_days=settings.rag_inactive_chunk_retention_days)
    return PurgeResponse(
        inactive_chunks_deleted=result.inactive_chunks_deleted,
        cache_entries_deleted=result.cache_entries_deleted,
    )
```

Wire in `main.py` exactly like the indexing router/service pair (`app.state.maintenance_service = MaintenanceService(session_factory)`, `app.include_router(maintenance.router)`).

- [ ] **Step 3: Run** the endpoint tests → PASS. **Step 4: Lint/typecheck.** **Step 5: Commit:**

```bash
git add services/rag-api
git commit -m "feat(rag): internal maintenance purge endpoint"
```

## Task 4: Runbook And Docs

**Files:**
- Create: `docs/runbooks/rag-maintenance.md`

- [ ] **Step 1: Write the runbook** covering, concretely:

- Scheduling the purge: example Windows Task Scheduler / cron line calling `curl -X POST http://<internal-host>:8000/internal/maintenance/purge -H "X-Internal-Service-Token: $TOKEN"` from inside the Docker network (e.g., a `docker compose exec rag-api curl ...` wrapper), recommended daily.
- Autovacuum tuning for `rag.document_chunks` (lower `autovacuum_vacuum_scale_factor`, e.g. `ALTER TABLE rag.document_chunks SET (autovacuum_vacuum_scale_factor = 0.05);`) so dead vectors leave the HNSW graphs promptly.
- Maintenance-window `REINDEX INDEX CONCURRENTLY` for the two partial HNSW indexes after large purges.
- A note that purge retention is `RAG_INACTIVE_CHUNK_RETENTION_DAYS` (default 7).

- [ ] **Step 2: Full verification**: `uv run pytest -q && uv run ruff check . && uv run mypy src tests` → green. Run `graphify update .`.

- [ ] **Step 3: Commit**

```bash
git add docs/runbooks/rag-maintenance.md graphify-out
git commit -m "docs: rag maintenance runbook (purge schedule, autovacuum, reindex)"
```

## Postman Checklist (new endpoints)

- POST `http://rag-api:8000/internal/maintenance/purge` — internal network only (not routed through Caddy); header `X-Internal-Service-Token: <token>`; empty body; expect `200 {"inactiveChunksDeleted": N, "cacheEntriesDeleted": M}`; without token expect `401 AUTH_INTERNAL_TOKEN_INVALID`.
