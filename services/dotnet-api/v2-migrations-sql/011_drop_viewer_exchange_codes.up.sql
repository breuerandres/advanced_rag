-- v2 migration 011: drop viewer_exchange_codes and viewer_token_audit
-- See docs/adr/0006-unified-session-auth.md

-- The new auth model uses a single __Host-session cookie and roles, so the exchange-code
-- flow used to bootstrap docs.client.com viewer tokens is no longer needed.

DROP TABLE IF EXISTS app.viewer_token_audit;
DROP TABLE IF EXISTS app.viewer_exchange_codes;
