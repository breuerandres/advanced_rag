DROP INDEX IF EXISTS app.ux_documents_external_key;
DROP INDEX IF EXISTS app.ix_documents_language;
ALTER TABLE app.documents
    DROP COLUMN IF EXISTS deleted_at,
    DROP COLUMN IF EXISTS external_key,
    DROP COLUMN IF EXISTS summary,
    DROP COLUMN IF EXISTS language;
