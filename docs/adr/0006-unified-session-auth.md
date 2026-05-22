# ADR-0006 — Unified session auth across the 3 SPAs

**Status**: Accepted (v2 design phase, 2026-05-22)
**Phase**: 1.5
**Supersedes**: `context/architecture.md` §Auth (chat-token + viewer-exchange code flows)

## Context

The MVP authenticates each SPA differently:

- `manage-web` uses `__Host-session` (HttpOnly, Secure, SameSite=Strict).
- `chat-web` exchanges the session for a short-lived `__Host-chat-token` via
  `POST /api/auth/chat-token`, validated locally by FastAPI.
- `docs-web` opens with a one-time exchange code in the URL (60s), then sets
  `__Host-viewer-token` (15min).

This was overkill for the MVP's threat model and is poor UX:

- Three login states for end users with one account.
- Three cookie lifetimes to debug when something is wrong.
- Two custom token flows that each require code, tests, and ops attention.
- "Login on manage, then open chat in a new tab" → user must wait for chat-token exchange.

The user explicitly asked: "I'd like one credential to enter all three pages."

## Decision

Make `__Host-session` the **only** browser auth cookie. Remove chat-token and viewer
exchange-code flows.

A new column `app.users.role` carries one of `admin`, `editor`, `viewer`. Endpoint
authorisation uses standard `[Authorize(Roles="…")]` in .NET and a `require_role()`
dependency in FastAPI.

Role hierarchy:

| Role | manage-web access | chat-web access | docs-web access | Capabilities |
|---|---|---|---|---|
| `viewer` | denied | full | full | chat queries, read docs, mark favourites |
| `editor` | partial (own docs) | full | full | + draft/edit own docs, send to review |
| `admin` | full | full | full | + publish, users, dimensions, config, api-keys |

FastAPI authenticates by reading the session cookie via Caddy. The chosen FastAPI
validation strategy (Open Question OQ-001):

- **Default plan**: Caddy injects `X-User-Claims` header into FastAPI requests after
  resolving the session cookie via a Redis cache that .NET keeps populated on login /
  refresh.
- **Fallback**: short-lived JWT signed by .NET on each request, set as a separate cookie
  validated locally by FastAPI (closer to MVP pattern, simpler to ship).

Endpoints deleted:
- `POST /api/auth/chat-token`
- `POST /api/viewer/exchange-links`
- `POST /api/viewer/exchange-code`
- `GET /api/viewer/documents/{id}` (functionality moved to `GET /api/v1/documents/{id}`
  with role + permission check)

## Alternatives considered

### Keep MVP auth pattern, just add API keys
Pros: No regression risk.
Cons: Doesn't address user's UX request; still three token flows.

### Move everything to bearer tokens in `Authorization` header
Pros: API-first feel.
Cons: `localStorage` is unsafe for credentials (per OWASP). Cookies + double-submit CSRF
remains the SOP for browser-issued requests.

### Single multi-domain cookie with `Domain=.client.com`
Pros: One cookie, automatically shared between subdomains.
Cons: `__Host-` prefix and broad-domain cookies are mutually exclusive. Broad cookies
expand the attack surface (every subdomain shares them, including any compromised one).

## Defense-in-depth lost vs gained

**Lost from MVP**:
- Short-lived chat-token limited XSS exposure window to 15 min.
- Single-use viewer exchange codes prevented link replay.

**Gained / compensated**:
- `__Host-` cookie is HttpOnly + Secure + SameSite=Strict + Path=/ — XSS cannot read it.
- CSRF double-submit cookie/header pair stays in place.
- CSP becomes stricter (Phase 1.7) to reduce XSS risk surface.
- For anonymous link sharing (future), a dedicated `POST /api/v1/documents/{id}/share-link`
  endpoint will issue HMAC-signed public links with configurable TTL — opt-in per
  customer, not the default browser auth path.

## Consequences

**Positive**
- One login, three apps.
- Less code (3 controllers deleted, 1 simplified).
- Simpler debugging.
- Per-role endpoint authorisation is idiomatic ASP.NET.

**Negative**
- Forces all chat users to be database users. The MVP allowed "anonymous" chat via
  exchange-code only; v2 doesn't. (User accepted this.)
- More privileged context in the browser (cookie has full session, no scope-limited
  token). Strict cookie attributes + CSP + CSRF mitigate.
- Migration: existing test suite mocks chat-token flow; tests must be rewritten in same
  commit as the controller deletion.

**Risks / mitigations**
- A vulnerable subdomain affects the session → cookies are `__Host-` per-host, so the
  session cookie set on `manage.client.com` is **not** automatically sent to
  `chat.client.com`. Solution: same session is re-established on each SPA's domain on
  first request via a "session bootstrap" call, or every SPA is served behind a single
  hostname (recommended). See Open Question OQ-001.

## References

- OWASP Session Management Cheat Sheet
- `services/dotnet-api/src/AdvancedRag.Api/Controllers/AuthController.cs` (to be edited)
- Open Question OQ-001 (FastAPI cookie validation strategy)
