DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'rag_owner') THEN
        REVOKE SELECT ON app.api_keys FROM rag_owner;
    END IF;
END;
$$;

ALTER TABLE app.document_views DROP CONSTRAINT IF EXISTS fk_document_views_api_key;
DROP TABLE IF EXISTS app.api_keys;
