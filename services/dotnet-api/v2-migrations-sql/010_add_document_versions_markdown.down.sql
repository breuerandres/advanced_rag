ALTER TABLE app.document_versions
    DROP COLUMN IF EXISTS content_markdown,
    DROP COLUMN IF EXISTS content_format;
