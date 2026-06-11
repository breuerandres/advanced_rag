#!/usr/bin/env bash
set -Eeuo pipefail

BRANCH="${UPDATE_BRANCH:-mvp-implementation}"
REMOTE="${UPDATE_REMOTE:-origin}"
ENV_FILE_INPUT="${UPDATE_ENV_FILE:-}"
BACKUP_DIR_INPUT="${UPDATE_BACKUP_DIR:-backups}"
HEALTH_TIMEOUT_SECONDS="${UPDATE_HEALTH_TIMEOUT_SECONDS:-240}"
SKIP_BACKUP="false"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$SCRIPT_DIR"
COMPOSE_ROOT="$REPO_ROOT/infra/compose"
COMPOSE_FILE="$COMPOSE_ROOT/compose.yaml"
PREVIOUS_COMMIT=""
BACKUP_FILE=""

log() {
    printf '[updateService] %s\n' "$*"
}

fail() {
    printf '[updateService] ERROR: %s\n' "$*" >&2
    exit 1
}

usage() {
    cat <<'USAGE'
Usage: ./updateService.sh [options]

Safely updates the Docker Compose deployment from the current Git branch.

Options:
  --branch <name>             Git branch to pull. Default: mvp-implementation.
  --remote <name>             Git remote to pull from. Default: origin.
  --env-file <path>           Compose environment file. Defaults to infra/compose/.env,
                              then infra/compose/.env.pi when .env is absent.
  --backup-dir <path>         Directory for pg_dump backups. Default: ./backups.
  --health-timeout <seconds>  Per-service health wait timeout. Default: 240.
  --skip-backup               Skip Postgres backup. Use only for first startup or throwaway demos.
  -h, --help                  Show this help.

Environment variables mirror the options:
  UPDATE_BRANCH, UPDATE_REMOTE, UPDATE_ENV_FILE, UPDATE_BACKUP_DIR,
  UPDATE_HEALTH_TIMEOUT_SECONDS
USAGE
}

on_error() {
    local exit_code=$?
    printf '\n[updateService] Update failed with exit code %s.\n' "$exit_code" >&2

    if [[ -n "$PREVIOUS_COMMIT" ]]; then
        printf '[updateService] Previous commit: %s\n' "$PREVIOUS_COMMIT" >&2
        printf '[updateService] Manual rollback hint:\n' >&2
        printf '  git checkout %s\n' "$PREVIOUS_COMMIT" >&2
        printf '  cd infra/compose\n' >&2
        printf '  docker compose --env-file "%s" -f compose.yaml up -d --build --remove-orphans\n' "$ENV_FILE" >&2
    fi

    if [[ -n "$BACKUP_FILE" && -f "$BACKUP_FILE" ]]; then
        printf '[updateService] Postgres backup created before update: %s\n' "$BACKUP_FILE" >&2
    fi

    exit "$exit_code"
}

trap on_error ERR

while [[ $# -gt 0 ]]; do
    case "$1" in
        --branch)
            [[ $# -ge 2 ]] || fail "--branch requires a value."
            BRANCH="$2"
            shift 2
            ;;
        --remote)
            [[ $# -ge 2 ]] || fail "--remote requires a value."
            REMOTE="$2"
            shift 2
            ;;
        --env-file)
            [[ $# -ge 2 ]] || fail "--env-file requires a value."
            ENV_FILE_INPUT="$2"
            shift 2
            ;;
        --backup-dir)
            [[ $# -ge 2 ]] || fail "--backup-dir requires a value."
            BACKUP_DIR_INPUT="$2"
            shift 2
            ;;
        --health-timeout)
            [[ $# -ge 2 ]] || fail "--health-timeout requires a value."
            HEALTH_TIMEOUT_SECONDS="$2"
            shift 2
            ;;
        --skip-backup)
            SKIP_BACKUP="true"
            shift
            ;;
        -h|--help)
            usage
            exit 0
            ;;
        *)
            fail "Unknown argument: $1"
            ;;
    esac
done

resolve_path() {
    local input="$1"
    local base="${2:-$REPO_ROOT}"

    if [[ "$input" = /* ]]; then
        printf '%s\n' "$input"
    else
        printf '%s/%s\n' "$base" "$input"
    fi
}

select_env_file() {
    if [[ -n "$ENV_FILE_INPUT" ]]; then
        resolve_path "$ENV_FILE_INPUT" "$REPO_ROOT"
        return
    fi

    if [[ -f "$COMPOSE_ROOT/.env" ]]; then
        printf '%s\n' "$COMPOSE_ROOT/.env"
        return
    fi

    if [[ -f "$COMPOSE_ROOT/.env.pi" ]]; then
        printf '%s\n' "$COMPOSE_ROOT/.env.pi"
        return
    fi

    fail "No Compose environment file found. Create infra/compose/.env or pass --env-file infra/compose/.env.pi."
}

ENV_FILE="$(select_env_file)"
BACKUP_DIR="$(resolve_path "$BACKUP_DIR_INPUT" "$REPO_ROOT")"

compose() {
    (cd "$COMPOSE_ROOT" && docker compose --env-file "$ENV_FILE" -f compose.yaml "$@")
}

require_command() {
    command -v "$1" >/dev/null 2>&1 || fail "Required command not found: $1"
}

assert_prerequisites() {
    [[ -d "$REPO_ROOT/.git" ]] || fail "This script must run from the Advanced RAG Git repository root."
    [[ -f "$COMPOSE_FILE" ]] || fail "Missing Compose file: $COMPOSE_FILE"
    [[ -f "$ENV_FILE" ]] || fail "Missing Compose environment file: $ENV_FILE"
    [[ -d "$COMPOSE_ROOT/secrets" ]] || fail "Missing Compose secrets directory: $COMPOSE_ROOT/secrets"

    require_command git
    require_command docker

    docker compose version >/dev/null

    local required_secrets=(
        "postgres_admin_password.txt"
        "postgres_app_password.txt"
        "postgres_rag_password.txt"
        "postgres_reporting_password.txt"
        "openai_api_key.txt"
        "jwt_signing_keys.json"
        "csrf_signing_key.txt"
        "internal_service_token.txt"
        "minio_root_user.txt"
        "minio_root_password.txt"
        "s3_access_key.txt"
        "s3_secret_key.txt"
    )

    local missing=()
    local secret_file
    for secret_file in "${required_secrets[@]}"; do
        if [[ ! -s "$COMPOSE_ROOT/secrets/$secret_file" ]]; then
            missing+=("$secret_file")
        fi
    done

    if [[ ${#missing[@]} -gt 0 ]]; then
        fail "Missing or empty Compose secret files: ${missing[*]}"
    fi
}

assert_clean_worktree() {
    local current_branch
    current_branch="$(git -C "$REPO_ROOT" branch --show-current)"

    [[ "$current_branch" == "$BRANCH" ]] || fail "Current branch is '$current_branch', expected '$BRANCH'. Pass --branch to deploy another branch."

    local tracked_changes
    tracked_changes="$(git -C "$REPO_ROOT" status --porcelain --untracked-files=no)"

    if [[ -n "$tracked_changes" ]]; then
        printf '%s\n' "$tracked_changes" >&2
        fail "Tracked local changes are present. Commit, stash, or resolve them before updating the service."
    fi
}

create_postgres_backup() {
    if [[ "$SKIP_BACKUP" == "true" ]]; then
        log "Skipping Postgres backup by explicit request."
        return
    fi

    local postgres_container_id
    postgres_container_id="$(compose ps -q postgres || true)"

    if [[ -z "$postgres_container_id" ]]; then
        log "Postgres container is not present. Skipping backup; this looks like a first startup."
        return
    fi

    local postgres_state
    postgres_state="$(docker inspect --format '{{.State.Status}}' "$postgres_container_id" 2>/dev/null || true)"
    [[ "$postgres_state" == "running" ]] || fail "Postgres container exists but is not running; cannot create backup."

    mkdir -p "$BACKUP_DIR"

    local timestamp
    timestamp="$(date -u +%Y%m%d-%H%M%S)"
    BACKUP_FILE="$BACKUP_DIR/postgres-${timestamp}-${PREVIOUS_COMMIT}.dump"

    log "Creating Postgres backup: $BACKUP_FILE"

    compose exec -T postgres bash -lc \
        'export PGPASSWORD="$(cat /run/secrets/postgres_admin_password)"; pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" --format=custom' \
        > "$BACKUP_FILE"

    [[ -s "$BACKUP_FILE" ]] || fail "Postgres backup file is empty: $BACKUP_FILE"
}

update_repository() {
    log "Fetching $REMOTE/$BRANCH"
    git -C "$REPO_ROOT" fetch "$REMOTE" "$BRANCH"

    log "Fast-forwarding local branch with git pull --ff-only"
    git -C "$REPO_ROOT" pull --ff-only "$REMOTE" "$BRANCH"
}

validate_compose_config() {
    log "Validating Docker Compose configuration."
    compose config >/dev/null
}

rebuild_and_start_stack() {
    log "Rebuilding and recreating the Compose stack."
    compose up -d --build --remove-orphans
}

refresh_caddy_container() {
    # The Caddyfile is bind-mounted as a single file. `git pull` replaces that file
    # with a new inode, but an already-running container keeps the mount pointed at
    # the old inode, so it never sees Caddyfile changes -- and `caddy reload` only
    # reloads that stale content. `compose up` does not recreate caddy on its own
    # because the container spec is unchanged. Force-recreating the caddy container
    # re-resolves the bind mount to the current file and loads the updated routing.
    log "Recreating the caddy container to pick up Caddyfile changes."
    compose up -d --force-recreate caddy
}

wait_for_service() {
    local service_name="$1"
    local timeout_seconds="$2"
    local start_time
    start_time="$(date +%s)"

    log "Waiting for $service_name to become healthy."

    while true; do
        local container_id
        container_id="$(compose ps -q "$service_name" 2>/dev/null || true)"

        if [[ -n "$container_id" ]]; then
            local status
            status="$(docker inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' "$container_id" 2>/dev/null || true)"

            if [[ "$status" == "healthy" || "$status" == "running" ]]; then
                log "$service_name is $status."
                return
            fi

            if [[ "$status" == "exited" || "$status" == "dead" ]]; then
                compose logs --tail=80 "$service_name" || true
                fail "$service_name stopped with status '$status'."
            fi
        fi

        local now
        now="$(date +%s)"
        if (( now - start_time >= timeout_seconds )); then
            compose logs --tail=80 "$service_name" || true
            fail "Timed out waiting for $service_name after ${timeout_seconds}s."
        fi

        sleep 3
    done
}

wait_for_stack() {
    local services=(
        "postgres"
        "minio"
        "dotnet-api"
        "rag-api"
        "manage-web"
        "chat-web"
        "docs-web"
        "caddy"
    )

    local service
    for service in "${services[@]}"; do
        wait_for_service "$service" "$HEALTH_TIMEOUT_SECONDS"
    done
}

print_final_status() {
    local public_domain
    public_domain="$(grep -E '^PUBLIC_DOMAIN=' "$ENV_FILE" | tail -n 1 | cut -d '=' -f 2- || true)"
    public_domain="${public_domain//$'\r'/}"
    public_domain="${public_domain%\"}"
    public_domain="${public_domain#\"}"

    log "Compose status:"
    compose ps

    log "Recent backend and proxy logs:"
    compose logs --tail=80 dotnet-api rag-api caddy

    if [[ -n "$public_domain" ]]; then
        cat <<EOF

[updateService] Public URLs:
  https://manage.${public_domain}
  https://chat.${public_domain}
  https://docs.${public_domain}
EOF
    fi

    if [[ -n "$BACKUP_FILE" ]]; then
        log "Backup created: $BACKUP_FILE"
    fi

    log "Updated from $PREVIOUS_COMMIT to $(git -C "$REPO_ROOT" rev-parse --short HEAD)."
}

main() {
    cd "$REPO_ROOT"

    assert_prerequisites
    assert_clean_worktree

    PREVIOUS_COMMIT="$(git -C "$REPO_ROOT" rev-parse --short HEAD)"

    log "Repository: $REPO_ROOT"
    log "Branch: $BRANCH"
    log "Environment file: $ENV_FILE"
    log "Previous commit: $PREVIOUS_COMMIT"

    create_postgres_backup
    update_repository
    validate_compose_config
    rebuild_and_start_stack
    refresh_caddy_container
    wait_for_stack
    print_final_status
}

main "$@"
