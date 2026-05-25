-- v2 migration 004: document view tracking
-- See docs/v2/03-phases.md Phase 4

CREATE TABLE IF NOT EXISTS app.document_views (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    document_id     uuid NOT NULL REFERENCES app.documents(id) ON DELETE CASCADE,
    user_id         uuid REFERENCES app.users(id) ON DELETE SET NULL,
    api_key_id      uuid,                                       -- FK added in 007 if api_keys present
    viewed_at       timestamptz NOT NULL DEFAULT now(),
    source          text,                                       -- 'chat-citation' | 'browse' | 'search-result'
    request_id      text
);

CREATE INDEX IF NOT EXISTS ix_document_views_document
    ON app.document_views (document_id, viewed_at DESC);
CREATE INDEX IF NOT EXISTS ix_document_views_user
    ON app.document_views (user_id, viewed_at DESC) WHERE user_id IS NOT NULL;
