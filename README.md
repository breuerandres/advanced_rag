# Advanced RAG Instruction Platform

Single-tenant corporate instruction management and RAG platform.

## Current Status

The project is in implementation planning. The approved base architecture spec lives at:

- `docs/superpowers/specs/2026-05-11-base-architecture-design.md`

## Planned Services

- `apps/manage-web` - management frontend
- `apps/chat-web` - chat frontend
- `apps/docs-web` - instruction viewer frontend
- `services/dotnet-api` - .NET 8 management API
- `services/rag-api` - FastAPI RAG service
- `infra/compose` - Docker Compose deployment

## Local Development

Local setup commands are added as services are scaffolded.
Real secrets must not be committed.

Operational hardening notes live in `docs/operations/operational-hardening.md`.

## Troubleshooting

- `docs/troubleshooting.md` - local operational fixes, including Caddy Docker HTTPS certificate trust.
