-- v2 migration 005: per-user ±1 reactions per document
-- See docs/v2/03-phases.md Phase 4

CREATE TABLE IF NOT EXISTS app.document_reactions (
    document_id     uuid NOT NULL REFERENCES app.documents(id) ON DELETE CASCADE,
    user_id         uuid NOT NULL REFERENCES app.users(id) ON DELETE CASCADE,
    reaction        smallint NOT NULL CHECK (reaction IN (-1, 1)),
    reacted_at      timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (document_id, user_id)
);

CREATE INDEX IF NOT EXISTS ix_document_reactions_doc_value
    ON app.document_reactions (document_id, reaction);
