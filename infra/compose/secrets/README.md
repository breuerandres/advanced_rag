# Local Compose Secrets

Create these files locally before running the Compose stack:

- `postgres_admin_password.txt`
- `postgres_app_password.txt`
- `postgres_rag_password.txt`
- `postgres_reporting_password.txt`
- `openai_api_key.txt`
- `jwt_signing_keys.json`
- `csrf_signing_key.txt`
- `internal_service_token.txt`
- `minio_root_user.txt`
- `minio_root_password.txt`
- `s3_access_key.txt`
- `s3_secret_key.txt`

Do not commit secret values. This directory is ignored except for this README.

## Recommended Local Setup

From the repository root, run:

```powershell
.\infra\compose\New-LocalDevSecrets.ps1
```

Then create `openai_api_key.txt` manually and start the local stack with:

```powershell
.\infra\compose\Start-Local.ps1 -TrustCaddyCertificate
```

`Start-Local.ps1` starts the Compose stack and, only when `-TrustCaddyCertificate` is passed, imports the Docker Compose Caddy internal CA into `Cert:\CurrentUser\Root`. This avoids the browser certificate warning for `https://manage.localhost`, `https://chat.localhost`, and `https://docs.localhost`.

If you want to rotate local stack-owned secrets, run:

```powershell
.\infra\compose\New-LocalDevSecrets.ps1 -Overwrite
```

The script generates:

- The Postgres admin password only when `postgres_admin_password.txt` does not already exist.
- Three random runtime Postgres role passwords.
- A random CSRF HMAC signing key.
- A random internal service token shared by `.NET` and FastAPI.
- Local MinIO root and S3-compatible runtime credentials for document image storage.
- A valid RS256 `jwt_signing_keys.json` document with one `current` key.

The script never creates, overwrites, or rotates `openai_api_key.txt`. That file contains the external OpenAI API key and must be created or updated manually by the developer/operator.

`postgres_admin_password.txt` is intentionally not overwritten once it exists. A running Postgres volume stores the superuser password internally; changing only the Compose secret file breaks `postgres-init` authentication. For a local throwaway reset, remove the Compose volume and generate fresh secrets. For a data-preserving rotation, update the database password with `ALTER ROLE postgres WITH PASSWORD ...` before changing the secret file.

To create the OpenAI key file manually:

```powershell
Set-Content -NoNewline -Path infra/compose/secrets/openai_api_key.txt -Value "sk-proj-replace-with-your-local-key"
```

After changing secrets, recreate or restart the services that read them:

```powershell
docker compose --env-file infra/compose/.env.example -f infra/compose/compose.yaml up -d --build --force-recreate
```

## JWT Signing Key Format

`jwt_signing_keys.json` must contain at least one document with `status: "current"`:

```json
[
  {
    "kid": "local-dev-1",
    "status": "current",
    "private": "-----BEGIN PRIVATE KEY-----\n...\n-----END PRIVATE KEY-----\n",
    "public": "-----BEGIN PUBLIC KEY-----\n...\n-----END PUBLIC KEY-----\n"
  }
]
```

The `.NET` API uses the `current` private key for server-side signing needs such as internal compatibility paths and future rotated credentials. Browser chat and document viewing use the unified `__Host-session` cookie, not a separate chat-token or viewer-token cookie. Missing or placeholder JWT keys can still make `.NET` readiness or signing-dependent tests fail; the `.NET` logs will show `No current JWT signing key is configured.`

## Manual Placeholder Setup

For Compose syntax validation only, placeholder local files are enough. Before running the services, replace placeholders with valid development secrets.

Example PowerShell placeholders for local syntax validation:

```powershell
Set-Content -NoNewline -Path infra/compose/secrets/postgres_admin_password.txt -Value "local-admin-password"
Set-Content -NoNewline -Path infra/compose/secrets/postgres_app_password.txt -Value "local-app-password"
Set-Content -NoNewline -Path infra/compose/secrets/postgres_rag_password.txt -Value "local-rag-password"
Set-Content -NoNewline -Path infra/compose/secrets/postgres_reporting_password.txt -Value "local-reporting-password"
Set-Content -NoNewline -Path infra/compose/secrets/openai_api_key.txt -Value "replace-with-local-openai-key"
Set-Content -NoNewline -Path infra/compose/secrets/jwt_signing_keys.json -Value "[]"
Set-Content -NoNewline -Path infra/compose/secrets/csrf_signing_key.txt -Value "local-csrf-signing-key"
Set-Content -NoNewline -Path infra/compose/secrets/internal_service_token.txt -Value "local-internal-service-token"
Set-Content -NoNewline -Path infra/compose/secrets/minio_root_user.txt -Value "minioadmin"
Set-Content -NoNewline -Path infra/compose/secrets/minio_root_password.txt -Value "minioadminpassword"
Set-Content -NoNewline -Path infra/compose/secrets/s3_access_key.txt -Value "advanced-rag-images"
Set-Content -NoNewline -Path infra/compose/secrets/s3_secret_key.txt -Value "local-s3-runtime-password"
```
