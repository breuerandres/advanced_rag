-- v2 migration 009: add language, summary, external_key to documents
-- See docs/adr/0003-multilingual-embeddings.md

ALTER TABLE app.documents
    ADD COLUMN IF NOT EXISTS language text NOT NULL DEFAULT 'es-AR',
    ADD COLUMN IF NOT EXISTS summary text,
    ADD COLUMN IF NOT EXISTS external_key text,
    ADD COLUMN IF NOT EXISTS deleted_at timestamptz;

CREATE INDEX IF NOT EXISTS ix_documents_language ON app.documents (language);
CREATE UNIQUE INDEX IF NOT EXISTS ux_documents_external_key
    ON app.documents (external_key) WHERE external_key IS NOT NULL;
