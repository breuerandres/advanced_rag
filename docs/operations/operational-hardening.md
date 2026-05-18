# Operational Hardening

Task 16 adds the MVP operational controls for technical rate limits, readiness checks, structured technical logs, and Compose health checks.

## Rate Limits

Technical rate limits protect service stability and are separate from monthly AI budget enforcement.

| Workflow | Limit | Stable error code |
| --- | ---: | --- |
| Login by origin IP | 5 attempts per minute | `LOGIN_IP_RATE_LIMITED` |
| Login by user/email | 10 attempts per 15 minutes | `LOGIN_USER_RATE_LIMITED` |
| Chat by user | 30 questions per minute | `CHAT_RATE_LIMITED` |
| Import extraction by user | 10 imports per hour | `IMPORT_RATE_LIMITED` |
| Viewer exchange | 30 attempts per minute | `VIEWER_EXCHANGE_RATE_LIMITED` |

The MVP uses per-process in-memory fixed-window counters. This matches the initial single-instance Compose deployment. If a service is horizontally scaled later, rate limit state must move to shared storage such as Redis or Postgres advisory-backed counters.

## Readiness

`/health/live` reports only process liveness and must not depend on downstream systems.

`/health/ready` verifies critical dependencies:

- `.NET API`: app database connectivity, CSRF signing secret, internal service token, and current JWT signing key load.
- FastAPI RAG service: database connectivity, pgvector extension presence, OpenAI API key, and internal service token.

Readiness failures return HTTP `503` with a safe list of failed check names.

## Technical Logs

Both backend services write daily JSON log files:

- `.NET API`: `/var/log/dotnet-api/log-YYYYMMDD.json`
- FastAPI RAG service: `/var/log/rag-api/log-YYYYMMDD.json`

Each request log entry includes timestamp, service, request id, origin IP, route, method, response status, safe error code when available, and elapsed milliseconds.

## Compose Health Checks

Compose health checks now gate Caddy startup on healthy backend and frontend services. Backend checks call readiness endpoints; frontend checks verify the served bundle root responds.
