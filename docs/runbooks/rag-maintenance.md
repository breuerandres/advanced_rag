# RAG Schema Maintenance Runbook

Keeps the `rag` schema healthy under sustained production load. The RAG service does not
run a background maintenance daemon; maintenance is an **internal, token-protected
endpoint** that operators schedule from the host, consistent with the project's
"internal endpoints over background daemons" MVP stance.

## What the purge does

`POST /internal/maintenance/purge` runs two deletions in one transaction:

1. **Inactive chunk purge** — deletes `rag.document_chunks` rows where `is_active = false`
   and `created_at` is older than the retention window. These are superseded / re-indexed
   versions that retrieval already ignores; deleting them removes dead vectors that bloat
   the table and the HNSW graphs. Retention is `RAG_INACTIVE_CHUNK_RETENTION_DAYS`
   (default `7` days).
2. **Expired cache purge** — deletes `rag.semantic_cache_entries` whose `expires_at <= now()`.
   Their `rag.semantic_cache_sources` rows cascade with the entry.

The response reports the counts:

```json
{ "inactiveChunksDeleted": 0, "cacheEntriesDeleted": 0 }
```

## Scheduling the purge

The endpoint is on the internal Docker network only (not routed through Caddy). It requires
the `X-Internal-Service-Token` header (the Compose secret `internal_service_token`).

Run it from inside the Docker network, e.g. a daily host scheduler entry that execs a
short-lived `curl` in the rag-api container:

```bash
# Linux host cron (daily at 03:30)
30 3 * * *  docker compose -f /opt/advanced-rag/compose.yaml exec -T rag-api \
  curl -s -X POST http://localhost:8000/internal/maintenance/purge \
  -H "X-Internal-Service-Token: $(cat /run/secrets/internal_service_token)"
```

```powershell
# Windows Task Scheduler (daily) — action runs:
docker compose -f C:\advanced-rag\compose.yaml exec -T rag-api `
  curl -s -X POST http://localhost:8000/internal/maintenance/purge `
  -H "X-Internal-Service-Token: $env:INTERNAL_SERVICE_TOKEN"
```

A daily cadence is recommended. Increase frequency only if publish/re-index volume is high
enough that dead-row accumulation between runs hurts.

## Autovacuum tuning for `rag.document_chunks`

Deleting inactive chunks marks rows dead; autovacuum must reclaim them (and prune the HNSW
graphs) promptly. On a write-heavy deployment, lower the table's vacuum scale factor so
vacuum triggers sooner:

```sql
ALTER TABLE rag.document_chunks SET (autovacuum_vacuum_scale_factor = 0.05);
```

## Reindex after large purges (maintenance window)

After a large purge, rebuild the two partial HNSW indexes during a maintenance window so the
graphs are compacted. `CONCURRENTLY` avoids blocking retrieval:

```sql
REINDEX INDEX CONCURRENTLY rag.ix_document_chunks_embedding_hnsw_published;
REINDEX INDEX CONCURRENTLY rag.ix_document_chunks_embedding_hnsw_preview;
```

## Budget aggregation index (note)

The per-request budget check (`select sum(estimated_cost_usd) ... where user_id = :u and
created_at >= :start and created_at < :end`) is already served by the composite index
`ix_query_audit_events_user_created_at` on `(user_id, created_at)`, created in the initial
schema migration. No additional index is required for it.
