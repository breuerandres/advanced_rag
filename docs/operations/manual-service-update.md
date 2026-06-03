# Manual Service Update

Use `updateService.sh` as the interim deployment helper until the GHCR-based CI/CD flow is implemented.

The script is intended for the Linux deployment host. It keeps real secrets and runtime volumes local to the host, creates a Postgres backup before pulling code, updates the Git branch with a fast-forward-only pull, validates Docker Compose, rebuilds service images from source, recreates the stack, waits for health checks, and prints recent backend/proxy logs.

## Preconditions

- Run from the repository root on the deployment host.
- Docker Engine and Docker Compose plugin are installed.
- The current Git branch is the deployment branch, normally `mvp-implementation`.
- The Compose environment file exists at `infra/compose/.env` or `infra/compose/.env.pi`.
- Compose secret files exist under `infra/compose/secrets`.
- The Git worktree has no tracked local changes.

Check before updating:

```bash
cd /path/to/advanced-rag
git status --short --branch
docker compose --env-file infra/compose/.env.pi -f infra/compose/compose.yaml ps
```

Expected:

- The branch is `mvp-implementation`.
- No tracked local changes are listed.
- Existing service containers are visible if the stack is already running.

## Run Update

Make the script executable once on the Linux host:

```bash
chmod +x updateService.sh
```

Run with the default environment file selection:

```bash
./updateService.sh
```

For the Raspberry Pi demo host that uses `.env.pi`, be explicit:

```bash
./updateService.sh --env-file infra/compose/.env.pi
```

Expected:

- A Postgres backup is written under `./backups/` when the `postgres` container is already running.
- Git updates with `git pull --ff-only`.
- Compose config validates.
- Docker Compose rebuilds and recreates the stack.
- `postgres`, `minio`, `.NET`, FastAPI, the three frontends, and Caddy become healthy.
- The script prints recent `dotnet-api`, `rag-api`, and `caddy` logs.

## Options

```bash
./updateService.sh --help
```

Useful options:

- `--branch <name>` deploys a branch other than `mvp-implementation`.
- `--env-file <path>` selects a specific Compose environment file.
- `--backup-dir <path>` changes where Postgres backups are written.
- `--health-timeout <seconds>` changes the per-service health timeout.
- `--skip-backup` skips the Postgres backup. Use only for first startup or throwaway demos.

## Failure Handling

The script does not automatically roll back. Automatic rollback is unsafe while migrations may have already changed database schema.

If the update fails, the script prints:

- the previous Git commit,
- the Postgres backup path when a backup was created,
- a manual rollback command hint.

After any failure, inspect the service logs before retrying:

```bash
cd infra/compose
docker compose --env-file .env.pi -f compose.yaml ps
docker compose --env-file .env.pi -f compose.yaml logs --tail=120 dotnet-api rag-api caddy
```

Do not use volume-deleting Compose commands on a real deployment unless restoring from backup is the explicit goal.
