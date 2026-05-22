DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'rag_owner') THEN
        REVOKE SELECT ON app.users FROM rag_owner;
        REVOKE SELECT ON app.api_keys FROM rag_owner;
        REVOKE SELECT ON app.document_dimension_values FROM rag_owner;
        REVOKE SELECT ON app.dimension_values FROM rag_owner;
        REVOKE SELECT ON app.dimensions FROM rag_owner;
    END IF;
END;
$$;
