# systemd Autostart

Use `installServiceAutostart.sh` on the Linux deployment host to start the Advanced RAG Docker Compose stack automatically after a server reboot.

This is a host-level bootstrap helper. It does not store secret values, does not modify Compose secret files, and does not replace the future GHCR-based deployment workflow.

## What It Installs

The script creates this systemd unit by default:

```text
advanced-rag.service
```

The unit waits for Docker and `network-online.target`, then runs:

```bash
docker compose --env-file <env-file> -f <compose.yaml> up -d
```

The unit uses `Type=oneshot` and `RemainAfterExit=yes` because Docker Compose starts the containers and exits. The unit is for host reboot recovery, not per-container monitoring.

## Preconditions

- Run on the Linux deployment host, not from the Windows workstation.
- Docker Engine and Docker Compose plugin are installed.
- The repository exists on the host.
- Compose environment file exists, normally `infra/compose/.env.pi` for the Raspberry Pi demo.
- Compose secret files exist under `infra/compose/secrets`.
- `sudo` access is available for writing `/etc/systemd/system`.

## Install

From the repository root:

```bash
chmod +x installServiceAutostart.sh
./installServiceAutostart.sh --env-file infra/compose/.env.pi
```

Expected:

- The script validates Docker Compose config.
- `/etc/systemd/system/advanced-rag.service` is created.
- `systemctl daemon-reload` runs.
- `advanced-rag.service` is enabled for boot.
- `advanced-rag.service` starts immediately.

## Verify

```bash
sudo systemctl status advanced-rag.service --no-pager
docker compose --env-file infra/compose/.env.pi -f infra/compose/compose.yaml ps
```

Expected:

- `advanced-rag.service` is active.
- The Compose services are running or healthy.

Check logs:

```bash
sudo journalctl -u advanced-rag.service -n 120 --no-pager
docker compose --env-file infra/compose/.env.pi -f infra/compose/compose.yaml logs --tail=120 dotnet-api rag-api caddy
```

## Test Reboot Recovery

Only run this when it is safe to reboot the host:

```bash
sudo reboot
```

After the host is back:

```bash
sudo systemctl status advanced-rag.service --no-pager
docker compose --env-file infra/compose/.env.pi -f infra/compose/compose.yaml ps
```

Expected:

- The systemd unit is active.
- The Compose stack is running.
- Public routes through Cloudflare recover after `cloudflared` is also running.

## Remove Autostart

```bash
sudo systemctl disable --now advanced-rag.service
sudo rm /etc/systemd/system/advanced-rag.service
sudo systemctl daemon-reload
```

This removes boot autostart but does not delete containers, volumes, secrets, or project files.
