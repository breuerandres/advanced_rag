-- v2 migration 003: configurable dimensions for document categorisation
-- See docs/adr/0008-configurable-dimensions.md

CREATE TABLE IF NOT EXISTS app.dimensions (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    key             text NOT NULL,
    label           text NOT NULL,
    label_i18n      jsonb NOT NULL DEFAULT '{}'::jsonb,
    hierarchical    boolean NOT NULL DEFAULT false,
    required        boolean NOT NULL DEFAULT false,
    display_order   int NOT NULL DEFAULT 0,
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now(),
    deleted_at      timestamptz,
    CONSTRAINT uq_dimensions_key UNIQUE (key)
);

CREATE INDEX IF NOT EXISTS ix_dimensions_display_order ON app.dimensions (display_order)
    WHERE deleted_at IS NULL;


CREATE TABLE IF NOT EXISTS app.dimension_values (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    dimension_id    uuid NOT NULL REFERENCES app.dimensions(id) ON DELETE CASCADE,
    parent_id       uuid REFERENCES app.dimension_values(id) ON DELETE RESTRICT,
    external_key    text,
    label           text NOT NULL,
    label_i18n      jsonb NOT NULL DEFAULT '{}'::jsonb,
    description     text,
    color           text,
    icon            text,
    display_order   int NOT NULL DEFAULT 0,
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now(),
    deleted_at      timestamptz,
    CONSTRAINT uq_dimension_values_external UNIQUE (dimension_id, external_key)
);

CREATE INDEX IF NOT EXISTS ix_dimension_values_dimension
    ON app.dimension_values (dimension_id) WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS ix_dimension_values_parent
    ON app.dimension_values (parent_id) WHERE parent_id IS NOT NULL AND deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS ix_dimension_values_display_order
    ON app.dimension_values (dimension_id, display_order) WHERE deleted_at IS NULL;


CREATE TABLE IF NOT EXISTS app.document_dimension_values (
    document_id         uuid NOT NULL REFERENCES app.documents(id) ON DELETE CASCADE,
    dimension_value_id  uuid NOT NULL REFERENCES app.dimension_values(id) ON DELETE CASCADE,
    PRIMARY KEY (document_id, dimension_value_id)
);

CREATE INDEX IF NOT EXISTS ix_ddv_dimension_value
    ON app.document_dimension_values (dimension_value_id);
CREATE INDEX IF NOT EXISTS ix_ddv_document
    ON app.document_dimension_values (document_id);


-- Read grant for the RAG service so it can filter retrieval by dimensions.
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'rag_owner') THEN
        GRANT SELECT ON app.dimensions TO rag_owner;
        GRANT SELECT ON app.dimension_values TO rag_owner;
        GRANT SELECT ON app.document_dimension_values TO rag_owner;
    END IF;
END;
$$;
