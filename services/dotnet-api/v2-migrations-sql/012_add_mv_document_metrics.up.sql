-- v2 migration 012: materialised view aggregating per-document metrics
-- Refreshed by a nightly job (Phase 4).

CREATE MATERIALIZED VIEW IF NOT EXISTS app.mv_document_metrics AS
SELECT
    d.id AS document_id,
    COUNT(DISTINCT v.id) FILTER (WHERE v.viewed_at > now() - interval '30 days') AS views_30d,
    COUNT(DISTINCT v.id) AS views_total,
    COUNT(*) FILTER (WHERE r.reaction = 1)  AS likes,
    COUNT(*) FILTER (WHERE r.reaction = -1) AS dislikes,
    COUNT(DISTINCT f.user_id) AS favorites_count
FROM app.documents d
LEFT JOIN app.document_views      v ON v.document_id = d.id
LEFT JOIN app.document_reactions  r ON r.document_id = d.id
LEFT JOIN app.document_favorites  f ON f.document_id = d.id
GROUP BY d.id;

CREATE UNIQUE INDEX IF NOT EXISTS ux_mv_document_metrics_document
    ON app.mv_document_metrics (document_id);

-- Note: chat citations live in the `rag` schema. Cross-schema joins in a materialised
-- view need careful permission handling. Citations are surfaced through a separate
-- read-only view from .NET, not embedded here, to keep schema ownership clean.
