param(
    [switch] $TrustCaddyCertificate,
    [switch] $ForceRecreate,
    [int] $HealthTimeoutSeconds = 180
)

$ErrorActionPreference = "Stop"

$composeRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $composeRoot "..\..")
$envFile = Join-Path $composeRoot ".env.example"
$composeFile = Join-Path $composeRoot "compose.yaml"
$overrideFile = Join-Path $composeRoot "compose.override.yaml"
$secretsDir = Join-Path $composeRoot "secrets"
$caddyRootCertificatePath = Join-Path $secretsDir "caddy-local-root.crt"

function Invoke-Compose {
    param([string[]] $Arguments)

    Push-Location $repoRoot
    try {
        & docker compose `
            --env-file $envFile `
            -f $composeFile `
            -f $overrideFile `
            @Arguments
    }
    finally {
        Pop-Location
    }
}

function Assert-SecretFilesReady {
    $requiredFiles = @(
        "postgres_admin_password.txt",
        "postgres_app_password.txt",
        "postgres_rag_password.txt",
        "postgres_reporting_password.txt",
        "openai_api_key.txt",
        "jwt_signing_keys.json",
        "csrf_signing_key.txt",
        "internal_service_token.txt",
        "minio_root_user.txt",
        "minio_root_password.txt",
        "s3_access_key.txt",
        "s3_secret_key.txt"
    )

    $missing = @()
    foreach ($file in $requiredFiles) {
        $path = Join-Path $secretsDir $file
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            $missing += $file
        }
    }

    if ($missing.Count -gt 0) {
        throw "Missing local Compose secret files, or paths are directories instead of files: $($missing -join ', '). Run .\infra\compose\New-LocalDevSecrets.ps1 -Overwrite and create openai_api_key.txt manually before starting the stack."
    }
}

function Wait-ForServiceHealthy {
    param(
        [string] $ServiceName,
        [int] $TimeoutSeconds
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $containerId = Invoke-Compose -Arguments @("ps", "-q", $ServiceName) 2>$null
        if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($containerId)) {
            $status = & docker inspect `
                --format "{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}" `
                $containerId
            if ($status -eq "healthy" -or $status -eq "running") {
                return
            }
        }

        Start-Sleep -Seconds 2
    }

    throw "Timed out waiting for $ServiceName to become healthy after $TimeoutSeconds seconds."
}

function Import-CaddyRootCertificate {
    New-Item -ItemType Directory -Force -Path $secretsDir | Out-Null

    Wait-ForServiceHealthy -ServiceName "caddy" -TimeoutSeconds $HealthTimeoutSeconds
    Invoke-Compose -Arguments @(
        "cp",
        "caddy:/data/caddy/pki/authorities/local/root.crt",
        $caddyRootCertificatePath
    )

    $certificate = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($caddyRootCertificatePath)
    $trustedCertificate = Get-ChildItem Cert:\CurrentUser\Root |
        Where-Object { $_.Thumbprint -eq $certificate.Thumbprint } |
        Select-Object -First 1

    if ($trustedCertificate) {
        Write-Host "Caddy local root certificate is already trusted for the current user."
        return
    }

    Import-Certificate -FilePath $caddyRootCertificatePath -CertStoreLocation Cert:\CurrentUser\Root | Out-Null
    Write-Host "Imported Caddy local root certificate into Cert:\CurrentUser\Root."
    Write-Host "Close and reopen the browser if it was already open."
}

Assert-SecretFilesReady

$upArgs = @("up", "-d", "--build")
if ($ForceRecreate) {
    $upArgs += "--force-recreate"
}

Invoke-Compose -Arguments $upArgs

if ($TrustCaddyCertificate) {
    Import-CaddyRootCertificate
}

Write-Host ""
Write-Host "Local stack startup requested."
Write-Host "Management URL: https://manage.localhost"
Write-Host "Default admin: admin@admin.com / admin"
Write-Host "Status command:"
Write-Host "docker compose --env-file infra/compose/.env.example -f infra/compose/compose.yaml -f infra/compose/compose.override.yaml ps -a"
