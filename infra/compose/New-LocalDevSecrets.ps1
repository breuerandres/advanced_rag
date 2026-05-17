param(
    [switch] $Overwrite
)

$ErrorActionPreference = "Stop"

$composeRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $composeRoot "..\..")
$secretsDir = Join-Path $composeRoot "secrets"
New-Item -ItemType Directory -Force -Path $secretsDir | Out-Null

function New-Base64UrlSecret {
    param([int] $Bytes = 32)

    $buffer = [byte[]]::new($Bytes)
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $rng.GetBytes($buffer)
    }
    finally {
        $rng.Dispose()
    }
    return [Convert]::ToBase64String($buffer).TrimEnd("=").Replace("+", "-").Replace("/", "_")
}

function Write-SecretFile {
    param(
        [string] $Name,
        [string] $Value
    )

    $path = Join-Path $secretsDir $Name
    if ((Test-Path $path) -and -not $Overwrite) {
        Write-Host "Keeping existing $Name"
        return
    }

    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($path, $Value, $utf8NoBom)
    Write-Host "Wrote $Name"
}

$postgresAdminPasswordPath = Join-Path $secretsDir "postgres_admin_password.txt"
if (Test-Path $postgresAdminPasswordPath) {
    Write-Host "Keeping existing postgres_admin_password.txt"
}
else {
    Write-SecretFile -Name "postgres_admin_password.txt" -Value (New-Base64UrlSecret -Bytes 32)
}
Write-SecretFile -Name "postgres_app_password.txt" -Value (New-Base64UrlSecret -Bytes 32)
Write-SecretFile -Name "postgres_rag_password.txt" -Value (New-Base64UrlSecret -Bytes 32)
Write-SecretFile -Name "postgres_reporting_password.txt" -Value (New-Base64UrlSecret -Bytes 32)
Write-SecretFile -Name "csrf_signing_key.txt" -Value (New-Base64UrlSecret -Bytes 32)
Write-SecretFile -Name "internal_service_token.txt" -Value (New-Base64UrlSecret -Bytes 32)

$openAiKeyPath = Join-Path $secretsDir "openai_api_key.txt"
if (Test-Path $openAiKeyPath) {
    Write-Host "Keeping existing openai_api_key.txt"
}
else {
    Write-Warning "openai_api_key.txt is missing. Create it manually with your local OpenAI API key before testing real chat/embedding calls."
}

$jwtPath = Join-Path $secretsDir "jwt_signing_keys.json"
if ((Test-Path $jwtPath) -and -not $Overwrite) {
    Write-Host "Keeping existing jwt_signing_keys.json"
}
else {
    $python = @'
import json
import sys
from pathlib import Path
from cryptography.hazmat.primitives.asymmetric import rsa
from cryptography.hazmat.primitives import serialization

target = Path(sys.argv[1])
key = rsa.generate_private_key(public_exponent=65537, key_size=2048)

private_pem = key.private_bytes(
    serialization.Encoding.PEM,
    serialization.PrivateFormat.PKCS8,
    serialization.NoEncryption(),
).decode("utf-8")

public_pem = key.public_key().public_bytes(
    serialization.Encoding.PEM,
    serialization.PublicFormat.SubjectPublicKeyInfo,
).decode("utf-8")

document = [{
    "kid": "local-dev-1",
    "status": "current",
    "private": private_pem,
    "public": public_pem,
}]

target.write_text(json.dumps(document, indent=2), encoding="utf-8")
'@

    Push-Location (Join-Path $repoRoot "services\rag-api")
    try {
        $temp = New-TemporaryFile
        Set-Content -Path $temp -Value $python -Encoding UTF8
        uv run python $temp $jwtPath
    }
    finally {
        Pop-Location
        if ($temp -and (Test-Path $temp)) {
            Remove-Item -Force $temp
        }
    }
    Write-Host "Wrote jwt_signing_keys.json"
}

Write-Host ""
Write-Host "Local Compose secrets are ready under $secretsDir"
Write-Host "Restart services that read secrets after changes:"
Write-Host "docker compose --env-file infra/compose/.env.example -f infra/compose/compose.yaml up -d --build --force-recreate"
