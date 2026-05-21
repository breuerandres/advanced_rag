param(
    [switch] $WithSampleDocument
)

$ErrorActionPreference = "Stop"

$composeRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $composeRoot "..\..")
$demoPassword = "DemoPassword!42"

function New-PasswordHash {
    param([string] $Password)

    $salt = [System.Text.Encoding]::UTF8.GetBytes("advanced-rag-demo")
    $derive = [System.Security.Cryptography.Rfc2898DeriveBytes]::new(
        $Password,
        $salt,
        210000,
        [System.Security.Cryptography.HashAlgorithmName]::SHA256)
    try {
        $hash = $derive.GetBytes(32)
    }
    finally {
        $derive.Dispose()
    }

    return "pbkdf2-sha256`$210000`$" +
        [Convert]::ToBase64String($salt) +
        "`$" +
        [Convert]::ToBase64String($hash)
}

$passwordHash = New-PasswordHash -Password $demoPassword

$sampleDocumentSql = ""
if ($WithSampleDocument) {
    $sampleDocumentSql = @"
insert into app.documents ("Id", title, current_state, created_by_user_id, created_at, updated_at)
values (
  '22000000-0000-0000-0000-000000000001',
  'Demo - Politica de seguridad',
  'Draft',
  '20000000-0000-0000-0000-000000000002',
  now(),
  now()
)
on conflict ("Id") do update set
  title = excluded.title,
  current_state = excluded.current_state,
  updated_at = now();

insert into app.document_versions (
  "Id", document_id, version_number, state, title, document_type, audience, content_html, indexing_status, created_at
)
values (
  '23000000-0000-0000-0000-000000000001',
  '22000000-0000-0000-0000-000000000001',
  1,
  'Draft',
  'Demo - Politica de seguridad',
  'Politica',
  'Equipo interno',
  '<h1>Seguridad</h1><p>Validar identidad y registrar el motivo antes de aprobar solicitudes internas.</p>',
  'None',
  now()
)
on conflict (document_id, version_number) do update set
  title = excluded.title,
  document_type = excluded.document_type,
  audience = excluded.audience,
  content_html = excluded.content_html,
  indexing_status = excluded.indexing_status;

update app.documents
set current_draft_version_id = '23000000-0000-0000-0000-000000000001',
    updated_at = now()
where "Id" = '22000000-0000-0000-0000-000000000001';

insert into app.document_permissions ("Id", document_id, group_id, created_at)
values (
  '24000000-0000-0000-0000-000000000001',
  '22000000-0000-0000-0000-000000000001',
  '21000000-0000-0000-0000-000000000001',
  now()
)
on conflict ("Id") do nothing;

insert into app.audit_events (
  "Id", actor_user_id, event_type, entity_type, entity_id, details_json, request_id, created_at
)
values (
  '25000000-0000-0000-0000-000000000001',
  '20000000-0000-0000-0000-000000000002',
  'document.created',
  'document',
  '22000000-0000-0000-0000-000000000001',
  '{"documentId":"22000000-0000-0000-0000-000000000001"}'::jsonb,
  'demo-seed-document',
  now()
)
on conflict ("Id") do nothing;
"@
}

$sql = @"
delete from app.document_permissions where document_id = '22000000-0000-0000-0000-000000000001';
delete from app.document_tags where document_id = '22000000-0000-0000-0000-000000000001';
delete from app.document_versions where document_id = '22000000-0000-0000-0000-000000000001';
delete from app.documents where "Id" = '22000000-0000-0000-0000-000000000001';
delete from app.user_ai_budget_limits where user_id in (
  '20000000-0000-0000-0000-000000000001',
  '20000000-0000-0000-0000-000000000002',
  '20000000-0000-0000-0000-000000000003'
);
delete from app.user_roles where user_id in (
  '20000000-0000-0000-0000-000000000001',
  '20000000-0000-0000-0000-000000000002',
  '20000000-0000-0000-0000-000000000003'
);
delete from app.user_groups where user_id in (
  '20000000-0000-0000-0000-000000000001',
  '20000000-0000-0000-0000-000000000002',
  '20000000-0000-0000-0000-000000000003'
);
delete from app.audit_events where actor_user_id in (
  '20000000-0000-0000-0000-000000000001',
  '20000000-0000-0000-0000-000000000002',
  '20000000-0000-0000-0000-000000000003'
);
delete from app.users where email like 'demo.%@example.com';
delete from app.groups where "Id" = '21000000-0000-0000-0000-000000000001';

insert into app.roles ("Id", name) values
  ('00000000-0000-0000-0000-0000000000a1', 'Admin'),
  ('00000000-0000-0000-0000-0000000000a2', 'DocumentManager'),
  ('00000000-0000-0000-0000-0000000000a3', 'Viewer')
on conflict (name) do nothing;

insert into app.groups ("Id", name)
values ('21000000-0000-0000-0000-000000000001', 'Demo Viewers')
on conflict (name) do update set name = excluded.name;

insert into app.users ("Id", email, display_name, password_hash, is_active) values
  ('20000000-0000-0000-0000-000000000001', 'demo.admin@example.com', 'Demo Admin', '$passwordHash', true),
  ('20000000-0000-0000-0000-000000000002', 'demo.manager@example.com', 'Demo Document Manager', '$passwordHash', true),
  ('20000000-0000-0000-0000-000000000003', 'demo.viewer@example.com', 'Demo Viewer', '$passwordHash', true);

insert into app.user_roles (user_id, role_id)
select '20000000-0000-0000-0000-000000000001'::uuid, "Id" from app.roles where name = 'Admin';
insert into app.user_roles (user_id, role_id)
select '20000000-0000-0000-0000-000000000002'::uuid, "Id" from app.roles where name = 'DocumentManager';
insert into app.user_roles (user_id, role_id)
select '20000000-0000-0000-0000-000000000003'::uuid, "Id" from app.roles where name = 'Viewer';

insert into app.user_groups (user_id, group_id)
values ('20000000-0000-0000-0000-000000000003', '21000000-0000-0000-0000-000000000001');

insert into app.user_ai_budget_limits (user_id, monthly_budget_usd, is_disabled, updated_by_user_id) values
  ('20000000-0000-0000-0000-000000000001', 5.00, false, '20000000-0000-0000-0000-000000000001'),
  ('20000000-0000-0000-0000-000000000002', 5.00, false, '20000000-0000-0000-0000-000000000001'),
  ('20000000-0000-0000-0000-000000000003', 5.00, false, '20000000-0000-0000-0000-000000000001');

insert into rag.model_pricing (
  id, model_id, model_kind, input_token_price_usd, cached_token_price_usd, output_token_price_usd, effective_from, effective_to
) values
  ('30000000-0000-0000-0000-000000000001', 'gpt-4.1-nano', 'chat', 0.0000000001, 0.0000000000, 0.0000000004, '2026-01-01T00:00:00Z', null),
  ('30000000-0000-0000-0000-000000000002', 'text-embedding-3-small', 'embedding', 0.00000000002, null, null, '2026-01-01T00:00:00Z', null)
on conflict (id) do nothing;

$sampleDocumentSql
"@

Push-Location $repoRoot
try {
    $composeArgs = @(
        "compose",
        "--env-file", "infra/compose/.env.example",
        "-f", "infra/compose/compose.yaml",
        "-f", "infra/compose/compose.override.yaml",
        "exec", "-T", "postgres",
        "psql", "-v", "ON_ERROR_STOP=1", "-U", "postgres", "-d", "advanced_rag"
    )

    $sql | docker @composeArgs
}
finally {
    Pop-Location
}

Write-Host ""
Write-Host "Local demo data seeded."
Write-Host "Credentials:"
Write-Host "  Admin:           demo.admin@example.com / $demoPassword"
Write-Host "  DocumentManager: demo.manager@example.com / $demoPassword"
Write-Host "  Viewer:          demo.viewer@example.com / $demoPassword"
if ($WithSampleDocument) {
    Write-Host "Sample draft document: Demo - Politica de seguridad"
}
