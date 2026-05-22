-- Reverse of 003_add_dimensions

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'rag_owner') THEN
        REVOKE SELECT ON app.document_dimension_values FROM rag_owner;
        REVOKE SELECT ON app.dimension_values FROM rag_owner;
        REVOKE SELECT ON app.dimensions FROM rag_owner;
    END IF;
END;
$$;

DROP TABLE IF EXISTS app.document_dimension_values;
DROP TABLE IF EXISTS app.dimension_values;
DROP TABLE IF EXISTS app.dimensions;
