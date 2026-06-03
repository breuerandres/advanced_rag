#!/usr/bin/env bash
set -Eeuo pipefail

SERVICE_NAME="${ADVANCED_RAG_SERVICE_NAME:-advanced-rag}"
ENV_FILE_INPUT="${ADVANCED_RAG_ENV_FILE:-}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$SCRIPT_DIR"
COMPOSE_ROOT="$REPO_ROOT/infra/compose"
COMPOSE_FILE="$COMPOSE_ROOT/compose.yaml"

log() {
    printf '[installServiceAutostart] %s\n' "$*"
}

fail() {
    printf '[installServiceAutostart] ERROR: %s\n' "$*" >&2
    exit 1
}

usage() {
    cat <<'USAGE'
Usage: ./installServiceAutostart.sh [options]

Installs a systemd service that starts the Advanced RAG Docker Compose stack
after Docker and network-online.target are available.

Default systemd unit: advanced-rag.service

Options:
  --env-file <path>       Compose environment file. Defaults to infra/compose/.env,
                          then infra/compose/.env.pi when .env is absent.
  --service-name <name>   systemd service name without ".service". Default: advanced-rag.
  -h, --help              Show this help.

Environment variables mirror the options:
  ADVANCED_RAG_ENV_FILE, ADVANCED_RAG_SERVICE_NAME
USAGE
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --env-file)
            [[ $# -ge 2 ]] || fail "--env-file requires a value."
            ENV_FILE_INPUT="$2"
            shift 2
            ;;
        --service-name)
            [[ $# -ge 2 ]] || fail "--service-name requires a value."
            SERVICE_NAME="$2"
            shift 2
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

quote_systemd_arg() {
    local value="$1"
    value="${value//\\/\\\\}"
    value="${value//\"/\\\"}"
    printf '"%s"' "$value"
}

require_command() {
    command -v "$1" >/dev/null 2>&1 || fail "Required command not found: $1"
}

assert_service_name_safe() {
    [[ "$SERVICE_NAME" =~ ^[A-Za-z0-9_.@-]+$ ]] || fail "Invalid systemd service name: $SERVICE_NAME"
}

ENV_FILE="$(select_env_file)"
SERVICE_UNIT="${SERVICE_NAME}.service"
SERVICE_FILE="/etc/systemd/system/${SERVICE_UNIT}"

assert_prerequisites() {
    [[ -d "$REPO_ROOT/.git" ]] || fail "This script must run from the Advanced RAG Git repository root."
    [[ -f "$COMPOSE_FILE" ]] || fail "Missing Compose file: $COMPOSE_FILE"
    [[ -f "$ENV_FILE" ]] || fail "Missing Compose environment file: $ENV_FILE"
    [[ -d "$COMPOSE_ROOT/secrets" ]] || fail "Missing Compose secrets directory: $COMPOSE_ROOT/secrets"

    assert_service_name_safe
    require_command docker
    require_command sudo
    require_command systemctl

    docker compose version >/dev/null
    (cd "$COMPOSE_ROOT" && docker compose --env-file "$ENV_FILE" -f compose.yaml config >/dev/null)
}

install_unit() {
    local docker_bin
    docker_bin="$(command -v docker)"

    local quoted_compose_root
    local quoted_env_file
    local quoted_compose_file
    quoted_compose_root="$(quote_systemd_arg "$COMPOSE_ROOT")"
    quoted_env_file="$(quote_systemd_arg "$ENV_FILE")"
    quoted_compose_file="$(quote_systemd_arg "$COMPOSE_FILE")"

    log "Installing $SERVICE_UNIT at $SERVICE_FILE"

    sudo tee "$SERVICE_FILE" >/dev/null <<EOF
[Unit]
Description=Advanced RAG Docker Compose stack
Requires=docker.service
After=docker.service network-online.target
Wants=network-online.target

[Service]
Type=oneshot
WorkingDirectory=${quoted_compose_root}
ExecStart=${docker_bin} compose --env-file ${quoted_env_file} -f ${quoted_compose_file} up -d
ExecStop=${docker_bin} compose --env-file ${quoted_env_file} -f ${quoted_compose_file} stop
RemainAfterExit=yes
TimeoutStartSec=600
TimeoutStopSec=120

[Install]
WantedBy=multi-user.target
EOF
}

enable_and_start_unit() {
    log "Reloading systemd."
    sudo systemctl daemon-reload

    log "Enabling $SERVICE_UNIT on boot."
    sudo systemctl enable "$SERVICE_UNIT"

    log "Starting $SERVICE_UNIT now."
    sudo systemctl start "$SERVICE_UNIT"

    log "Current service status:"
    sudo systemctl status "$SERVICE_UNIT" --no-pager --lines=30
}

main() {
    assert_prerequisites

    log "Repository: $REPO_ROOT"
    log "Compose root: $COMPOSE_ROOT"
    log "Environment file: $ENV_FILE"
    log "Service: $SERVICE_UNIT"

    install_unit
    enable_and_start_unit

    cat <<EOF

[installServiceAutostart] Installed.

Useful commands:
  sudo systemctl status ${SERVICE_UNIT} --no-pager
  sudo systemctl restart ${SERVICE_UNIT}
  sudo journalctl -u ${SERVICE_UNIT} -n 120 --no-pager
EOF
}

main "$@"
