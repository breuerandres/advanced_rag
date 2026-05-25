# Advanced RAG Document Platform

Single-tenant corporate document management and RAG platform.

## Current Status

The project is in implementation planning. The approved base architecture spec lives at:

- `docs/superpowers/specs/2026-05-11-base-architecture-design.md`

## Planned Services

- `apps/manage-web` - management frontend
- `apps/chat-web` - chat frontend
- `apps/docs-web` - document viewer frontend
- `services/dotnet-api` - .NET 8 management API
- `services/rag-api` - FastAPI RAG service
- `infra/compose` - Docker Compose deployment

## Local Development

Local setup commands are added as services are scaffolded.
Real secrets must not be committed.

Recommended first local startup:

```powershell
.\infra\compose\Start-Local.ps1 -TrustCaddyCertificate
```

The script starts the Compose stack and, when explicitly requested, imports the Docker Compose Caddy internal CA into the current user's Windows trusted root store.

Operational hardening notes live in `docs/operations/operational-hardening.md`.

Clean Compose databases are migrated automatically. The initial controlled-deployment administrator is `admin@admin.com` with password `admin`; change it immediately after first login outside throwaway local testing.

## Troubleshooting

- `docs/troubleshooting.md` - local operational fixes, including Caddy Docker HTTPS certificate trust.
