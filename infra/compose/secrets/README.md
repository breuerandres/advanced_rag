# Local Compose Secrets

Create these files locally before validating or running the Compose stack:

- `postgres_admin_password.txt`
- `postgres_app_password.txt`
- `postgres_rag_password.txt`
- `postgres_reporting_password.txt`
- `openai_api_key.txt`
- `jwt_signing_keys.json`
- `csrf_signing_key.txt`
- `internal_service_token.txt`

Do not commit secret values. This directory is ignored except for this README.

For Task 1 syntax validation only, placeholder local files are enough. Before running the services, replace placeholders with valid development secrets.

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
