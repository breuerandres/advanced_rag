-- v2 migration 013: ensure rag_owner can read new app tables it consults at retrieval time
-- See docs/adr/0008-configurable-dimensions.md

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'rag_owner') THEN
        -- Already granted in 003 + 007 inline; this is a safety net.
        GRANT SELECT ON app.dimensions             TO rag_owner;
        GRANT SELECT ON app.dimension_values       TO rag_owner;
        GRANT SELECT ON app.document_dimension_values TO rag_owner;
        GRANT SELECT (id, prefix, scopes, rate_limit_per_minute, monthly_budget_usd, revoked_at, expires_at)
            ON app.api_keys TO rag_owner;
        GRANT SELECT (id, role) ON app.users TO rag_owner;
    END IF;
END;
$$;
