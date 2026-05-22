-- v2 migration 010: optional markdown source for document versions
-- See docs/v2/02-target-architecture.md §3 (data model additions)

ALTER TABLE app.document_versions
    ADD COLUMN IF NOT EXISTS content_format text NOT NULL DEFAULT 'html'
        CHECK (content_format IN ('html', 'markdown')),
    ADD COLUMN IF NOT EXISTS content_markdown text;
