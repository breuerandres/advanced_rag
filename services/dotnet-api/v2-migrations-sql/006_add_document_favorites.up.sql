-- v2 migration 006: per-user document favourites
-- See docs/v2/03-phases.md Phase 4

CREATE TABLE IF NOT EXISTS app.document_favorites (
    document_id     uuid NOT NULL REFERENCES app.documents(id) ON DELETE CASCADE,
    user_id         uuid NOT NULL REFERENCES app.users(id) ON DELETE CASCADE,
    added_at        timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (document_id, user_id)
);

CREATE INDEX IF NOT EXISTS ix_document_favorites_user
    ON app.document_favorites (user_id, added_at DESC);
