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

Do not commit secret values. This directory is ignored except for this README.

## Recommended Local Setup

From the repository root, run:

```powershell
.\infra\compose\New-LocalDevSecrets.ps1 -OpenAiApiKey "sk-proj-replace-with-your-local-key"
```

If you want to rotate every local secret, run:

```powershell
.\infra\compose\New-LocalDevSecrets.ps1 -OpenAiApiKey "sk-proj-replace-with-your-local-key" -Overwrite
```

The script generates:

- Four random Postgres role passwords.
- A random CSRF HMAC signing key.
- A random internal service token shared by `.NET` and FastAPI.
- A valid RS256 `jwt_signing_keys.json` document with one `current` key.
- `openai_api_key.txt` from the value you provide.

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

The `.NET` API uses the `current` private key to issue chat tokens. Missing or placeholder JWT keys cause `/api/auth/chat-token` to fail with `INTERNAL_ERROR`; the `.NET` logs will show `No current JWT signing key is configured.`

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
```
