-- Recreate the legacy viewer exchange-code tables. Note: schema is approximate; consult
-- git history of the original migrations for the exact column set if exact downgrade
-- is required.

CREATE TABLE IF NOT EXISTS app.viewer_exchange_codes (
    code            text PRIMARY KEY,
    user_id         uuid NOT NULL,
    document_id     uuid,
    purpose         text NOT NULL,
    allowed_status  text NOT NULL,
    created_at      timestamptz NOT NULL DEFAULT now(),
    expires_at      timestamptz NOT NULL,
    used_at         timestamptz
);

CREATE TABLE IF NOT EXISTS app.viewer_token_audit (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    jti             text NOT NULL,
    user_id         uuid NOT NULL,
    document_id     uuid,
    issued_at       timestamptz NOT NULL DEFAULT now(),
    expires_at      timestamptz NOT NULL
);
