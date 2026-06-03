# Raspberry Pi Cloudflare Demo Deployment

This guide documents the demo deployment used to expose the Advanced RAG MVP from a Raspberry Pi through Cloudflare Tunnel.

The target deployment is a lightweight demo, not a hardened production installation. It keeps the project's same-origin host model:

- `manage.breuerai.com`
- `chat.breuerai.com`
- `docs.breuerai.com`

Cloudflare terminates public TLS, Cloudflare Tunnel carries encrypted traffic to the Raspberry Pi, and `cloudflared` forwards requests to local Caddy over `http://localhost:80`.

## Deployment Shape

```text
Browser
  -> HTTPS public hostname
Cloudflare edge
  -> encrypted Cloudflare Tunnel
cloudflared on Raspberry Pi
  -> http://localhost:80
Caddy
  -> manage-web, chat-web, docs-web, dotnet-api, rag-api
```

The local `cloudflared -> Caddy` hop uses HTTP because the traffic stays on the Raspberry Pi loopback interface. This avoids TLS/SNI issues between `cloudflared` and Caddy's internal CA certificate. Public browser traffic still uses HTTPS, and the Cloudflare tunnel leg is encrypted.

## Prerequisites

- Raspberry Pi running Ubuntu Server 24.04 LTS 64-bit.
- Docker Engine and Docker Compose plugin installed.
- Repository copied or cloned to the Raspberry Pi.
- Compose secrets created locally under `infra/compose/secrets`.
- OpenAI API key written only to `infra/compose/secrets/openai_api_key.txt`.
- Domain active in Cloudflare. The demo uses `breuerai.com`.
- Cloudflare Zero Trust enabled for the account.

Verify the host:

```bash
uname -m
docker version --format '{{.Server.Arch}} {{.Server.Version}}'
docker compose version
sudo ufw status
```

Expected:

- `uname -m` returns `aarch64`.
- Docker server architecture is `arm64`.
- UFW allows OpenSSH, `80/tcp`, and `443/tcp`.

## Compose Domain Configuration

The Raspberry Pi environment file should use the public domain:

```bash
cd ~/apps/advanced_rag
grep PUBLIC_DOMAIN infra/compose/.env.pi
```

Expected:

```text
PUBLIC_DOMAIN=breuerai.com
```

If needed, update it:

```bash
sed -i 's/^PUBLIC_DOMAIN=.*/PUBLIC_DOMAIN=breuerai.com/' infra/compose/.env.pi
```

Caddy must route by `PUBLIC_DOMAIN`:

```bash
grep -nE "manage|chat|docs" infra/compose/Caddyfile
```

Expected:

```text
manage.{$PUBLIC_DOMAIN}
chat.{$PUBLIC_DOMAIN}
docs.{$PUBLIC_DOMAIN}
```

Recreate Caddy after domain changes:

```bash
docker compose --env-file infra/compose/.env.pi -f infra/compose/compose.yaml up -d --force-recreate caddy
```

Verify local host routing:

```bash
curl -v -H "Host: manage.breuerai.com" http://localhost/
curl -v -H "Host: chat.breuerai.com" http://localhost/
curl -v -H "Host: docs.breuerai.com" http://localhost/
```

Each request should return `200` and frontend HTML.

## Cloudflare Tunnel Setup

In Cloudflare:

1. Open `Zero Trust`.
2. Go to `Networks -> Connectors -> Cloudflare Tunnels`.
3. Create or open the tunnel named `advanced-rag-raspberry`.
4. Choose the `cloudflared` connector type.
5. Copy the Docker command token shown by Cloudflare.

On the Raspberry Pi, run `cloudflared` with host networking:

```bash
docker rm -f cloudflared

docker run -d \
  --name cloudflared \
  --restart unless-stopped \
  --network host \
  cloudflare/cloudflared:latest \
  tunnel --no-autoupdate run --token CLOUDFLARE_TUNNEL_TOKEN
```

Do not store the token in project files or commit it.

Verify connector state:

```bash
docker ps --filter name=cloudflared
docker logs cloudflared --tail=80
```

Expected logs include registered tunnel connections and no repeated origin errors.

## Public Hostnames

Inside the `advanced-rag-raspberry` tunnel, add these public hostnames:

| Public hostname | Service type | Service URL |
| --- | --- | --- |
| `manage.breuerai.com` | `HTTP` | `localhost:80` |
| `chat.breuerai.com` | `HTTP` | `localhost:80` |
| `docs.breuerai.com` | `HTTP` | `localhost:80` |

Do not use `https://localhost:443` unless Cloudflare Tunnel is also configured with the correct origin server name and TLS verification settings for each hostname. The simpler demo configuration is `HTTP localhost:80`.

After saving hostnames, verify the tunnel receives updated config:

```bash
docker logs cloudflared --tail=80
```

Expected example:

```text
Updated to new configuration ... "hostname":"manage.breuerai.com","service":"http://localhost:80"
```

## Public Verification

From a browser:

```text
https://manage.breuerai.com
https://chat.breuerai.com
https://docs.breuerai.com
```

Expected:

- No browser certificate warning.
- `manage` login works.
- `chat` loads under the public hostname.
- `docs` loads under the public hostname.

For a clean deployment, log in with the seeded administrator account and immediately change the default password:

```text
admin@admin.com / admin
```

## Troubleshooting

### Cloudflare 502 Bad Gateway

Check `cloudflared` logs:

```bash
docker logs cloudflared --tail=120
```

If logs show:

```text
originService=https://localhost:443
remote error: tls: internal error
```

the public hostname is still pointing to HTTPS origin. Change the Cloudflare Tunnel public hostname service to:

```text
HTTP localhost:80
```

If logs show:

```text
connect: connection refused
```

verify Caddy is running and listening locally:

```bash
docker compose --env-file infra/compose/.env.pi -f infra/compose/compose.yaml ps caddy
curl -v -H "Host: manage.breuerai.com" http://localhost/
```

If `docker ps -a` shows `cloudflared` but `docker ps` does not, inspect the stopped connector:

```bash
docker ps -a --filter name=cloudflared
docker inspect cloudflared --format 'Status={{.State.Status}} ExitCode={{.State.ExitCode}} Error={{.State.Error}} Network={{.HostConfig.NetworkMode}}'
docker logs cloudflared --tail=120
```

Recreate the connector with `--network host` if needed.

### Public hostname routes to the wrong app

Verify Caddy's domain and site labels:

```bash
grep PUBLIC_DOMAIN infra/compose/.env.pi
grep -nE "manage|chat|docs" infra/compose/Caddyfile
```

Then recreate Caddy:

```bash
docker compose --env-file infra/compose/.env.pi -f infra/compose/compose.yaml up -d --force-recreate caddy
```

### Local Caddy works but Cloudflare does not

This command proves local routing:

```bash
curl -v -H "Host: manage.breuerai.com" http://localhost/
```

If this succeeds but Cloudflare fails, focus on Cloudflare Tunnel public hostname configuration and `cloudflared` logs.

## Security Notes

- Do not expose Postgres, MinIO, `.NET`, or FastAPI ports directly to the Internet.
- Do not publish the MinIO console in the public Cloudflare Tunnel.
- Keep Compose secret files local to the Raspberry Pi.
- Keep `manage.breuerai.com` behind the application's login at minimum.
- Prefer adding Cloudflare Access in front of `manage.breuerai.com` before showing the demo to untrusted users.
- Rotate the default administrator password immediately after first login.

## Useful Commands

Check stack status:

```bash
docker compose --env-file infra/compose/.env.pi -f infra/compose/compose.yaml ps
```

View Caddy logs:

```bash
docker compose --env-file infra/compose/.env.pi -f infra/compose/compose.yaml logs --tail=100 caddy
```

View backend logs:

```bash
docker compose --env-file infra/compose/.env.pi -f infra/compose/compose.yaml logs --tail=120 dotnet-api
docker compose --env-file infra/compose/.env.pi -f infra/compose/compose.yaml logs --tail=120 rag-api
```

Restart the tunnel:

```bash
docker restart cloudflared
docker logs cloudflared --tail=80
```
