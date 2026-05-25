-- v2 migration 007: scoped API keys with rate limits and budgets
-- See docs/v2/03-phases.md Phase 4

CREATE TABLE IF NOT EXISTS app.api_keys (
    id                          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    name                        text NOT NULL,
    prefix                      text NOT NULL,                    -- visible portion, e.g. 'hc_abcd'
    hashed_secret               text NOT NULL,                    -- argon2id or bcrypt
    scopes                      text[] NOT NULL DEFAULT ARRAY['chat']::text[],
    rate_limit_per_minute       int NOT NULL DEFAULT 60,
    monthly_budget_usd          numeric(8, 2),
    expires_at                  timestamptz,
    last_used_at                timestamptz,
    last_used_ip                text,
    created_by                  uuid NOT NULL REFERENCES app.users(id),
    created_at                  timestamptz NOT NULL DEFAULT now(),
    revoked_at                  timestamptz
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_api_keys_prefix ON app.api_keys (prefix);
CREATE INDEX IF NOT EXISTS ix_api_keys_created_by ON app.api_keys (created_by);
CREATE INDEX IF NOT EXISTS ix_api_keys_revoked ON app.api_keys (revoked_at) WHERE revoked_at IS NULL;

-- Backfill the api_key_id FK on document_views now that the table exists.
ALTER TABLE app.document_views
    ADD CONSTRAINT fk_document_views_api_key
    FOREIGN KEY (api_key_id) REFERENCES app.api_keys(id) ON DELETE SET NULL;

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'rag_owner') THEN
        GRANT SELECT (id, prefix, scopes, rate_limit_per_minute, monthly_budget_usd, revoked_at, expires_at)
        ON app.api_keys TO rag_owner;
    END IF;
END;
$$;
