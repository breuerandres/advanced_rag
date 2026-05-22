-- v2 migration 008: outgoing webhook subscriptions
-- See docs/v2/03-phases.md Phase 5.7

CREATE TABLE IF NOT EXISTS app.webhooks (
    id                          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    name                        text NOT NULL,
    url                         text NOT NULL,
    events                      text[] NOT NULL,
    secret                      text NOT NULL,                       -- HMAC signing secret
    active                      boolean NOT NULL DEFAULT true,
    last_delivery_at            timestamptz,
    last_delivery_status        int,
    last_delivery_error         text,
    delivery_failure_count      int NOT NULL DEFAULT 0,
    created_by                  uuid NOT NULL REFERENCES app.users(id),
    created_at                  timestamptz NOT NULL DEFAULT now(),
    updated_at                  timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_webhooks_active ON app.webhooks (active) WHERE active = true;
CREATE INDEX IF NOT EXISTS ix_webhooks_events_gin ON app.webhooks USING GIN (events);


CREATE TABLE IF NOT EXISTS app.webhook_deliveries (
    id                          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    webhook_id                  uuid NOT NULL REFERENCES app.webhooks(id) ON DELETE CASCADE,
    event_type                  text NOT NULL,
    payload                     jsonb NOT NULL,
    request_id                  text,
    attempt                     int NOT NULL DEFAULT 1,
    response_status             int,
    response_body_snippet       text,
    delivered_at                timestamptz,
    next_retry_at               timestamptz,
    error_message               text,
    created_at                  timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_webhook_deliveries_webhook
    ON app.webhook_deliveries (webhook_id, created_at DESC);
CREATE INDEX IF NOT EXISTS ix_webhook_deliveries_pending
    ON app.webhook_deliveries (next_retry_at)
    WHERE delivered_at IS NULL AND next_retry_at IS NOT NULL;
