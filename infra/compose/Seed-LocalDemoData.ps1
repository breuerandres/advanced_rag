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

$sql = @"
do `$`$
begin
  if to_regclass('app.organizational_units') is null
     or to_regclass('app.document_permission_groups') is null then
    raise exception 'Hierarchical access schema is not migrated yet.';
  end if;
end `$`$;

delete from rag.query_audit_citations
where query_audit_event_id in (
  select id from rag.query_audit_events where user_id in (
    '02000000-0000-0000-0000-000000000001',
    '02000000-0000-0000-0000-000000000002',
    '02000000-0000-0000-0000-000000000003',
    '02000000-0000-0000-0000-000000000004',
    '02000000-0000-0000-0000-000000000005',
    '02000000-0000-0000-0000-000000000006'
  )
);
delete from rag.query_audit_events where user_id in (
  '02000000-0000-0000-0000-000000000001',
  '02000000-0000-0000-0000-000000000002',
  '02000000-0000-0000-0000-000000000003',
  '02000000-0000-0000-0000-000000000004',
  '02000000-0000-0000-0000-000000000005',
  '02000000-0000-0000-0000-000000000006'
);

delete from app.audit_events where request_id like 'demo-seed-%';
delete from app.document_permission_groups where document_permission_id in (
  select "Id" from app.document_permissions where document_id between '04000000-0000-0000-0000-000000000001' and '04000000-0000-0000-0000-000000000006'
);
delete from app.document_permissions where document_id between '04000000-0000-0000-0000-000000000001' and '04000000-0000-0000-0000-000000000006';
delete from app.document_tags where document_id between '04000000-0000-0000-0000-000000000001' and '04000000-0000-0000-0000-000000000006';
delete from app.document_versions where document_id between '04000000-0000-0000-0000-000000000001' and '04000000-0000-0000-0000-000000000006';
delete from app.documents where "Id" between '04000000-0000-0000-0000-000000000001' and '04000000-0000-0000-0000-000000000006';

delete from app.user_group_publish_grants where user_id between '02000000-0000-0000-0000-000000000001' and '02000000-0000-0000-0000-000000000006'
   or group_id between '03000000-0000-0000-0000-000000000001' and '03000000-0000-0000-0000-000000000004';
delete from app.user_ai_budget_limits where user_id between '02000000-0000-0000-0000-000000000001' and '02000000-0000-0000-0000-000000000006';
delete from app.user_roles where user_id between '02000000-0000-0000-0000-000000000001' and '02000000-0000-0000-0000-000000000006';
delete from app.user_groups where user_id between '02000000-0000-0000-0000-000000000001' and '02000000-0000-0000-0000-000000000006'
   or group_id between '03000000-0000-0000-0000-000000000001' and '03000000-0000-0000-0000-000000000004';
delete from app.users where "Id" between '02000000-0000-0000-0000-000000000001' and '02000000-0000-0000-0000-000000000006';
delete from app.groups where "Id" between '03000000-0000-0000-0000-000000000001' and '03000000-0000-0000-0000-000000000004';
delete from app.organizational_unit_closure where ancestor_id between '01000000-0000-0000-0000-000000000001' and '01000000-0000-0000-0000-000000000008'
   or descendant_id between '01000000-0000-0000-0000-000000000001' and '01000000-0000-0000-0000-000000000008';
delete from app.organizational_units where "Id" between '01000000-0000-0000-0000-000000000002' and '01000000-0000-0000-0000-000000000008';

insert into app.roles ("Id", name) values
  ('00000000-0000-0000-0000-0000000000a1', 'Admin'),
  ('00000000-0000-0000-0000-0000000000a4', 'DocumentEditor'),
  ('00000000-0000-0000-0000-0000000000a5', 'DocumentPublisher'),
  ('00000000-0000-0000-0000-0000000000a3', 'Viewer')
on conflict (name) do nothing;

insert into app.organizational_units ("Id", name, parent_id, is_active, created_at, updated_at) values
  ('01000000-0000-0000-0000-000000000001', 'Empresa', null, true, now(), now()),
  ('01000000-0000-0000-0000-000000000002', 'Comunicacion', '01000000-0000-0000-0000-000000000001', true, now(), now()),
  ('01000000-0000-0000-0000-000000000003', 'Marketing', '01000000-0000-0000-0000-000000000002', true, now(), now()),
  ('01000000-0000-0000-0000-000000000004', 'Produccion Audiovisual', '01000000-0000-0000-0000-000000000002', true, now(), now()),
  ('01000000-0000-0000-0000-000000000005', 'Operaciones', '01000000-0000-0000-0000-000000000001', true, now(), now()),
  ('01000000-0000-0000-0000-000000000006', 'Recursos Humanos', '01000000-0000-0000-0000-000000000001', true, now(), now()),
  ('01000000-0000-0000-0000-000000000007', 'Sistemas', '01000000-0000-0000-0000-000000000001', true, now(), now()),
  ('01000000-0000-0000-0000-000000000008', 'Finanzas', '01000000-0000-0000-0000-000000000001', true, now(), now())
on conflict ("Id") do update set
  name = excluded.name,
  parent_id = excluded.parent_id,
  is_active = excluded.is_active,
  updated_at = now();

insert into app.organizational_unit_closure (ancestor_id, descendant_id, depth)
select ancestor_id, descendant_id, depth
from (values
  ('01000000-0000-0000-0000-000000000001'::uuid, '01000000-0000-0000-0000-000000000001'::uuid, 0),
  ('01000000-0000-0000-0000-000000000002'::uuid, '01000000-0000-0000-0000-000000000002'::uuid, 0),
  ('01000000-0000-0000-0000-000000000003'::uuid, '01000000-0000-0000-0000-000000000003'::uuid, 0),
  ('01000000-0000-0000-0000-000000000004'::uuid, '01000000-0000-0000-0000-000000000004'::uuid, 0),
  ('01000000-0000-0000-0000-000000000005'::uuid, '01000000-0000-0000-0000-000000000005'::uuid, 0),
  ('01000000-0000-0000-0000-000000000006'::uuid, '01000000-0000-0000-0000-000000000006'::uuid, 0),
  ('01000000-0000-0000-0000-000000000007'::uuid, '01000000-0000-0000-0000-000000000007'::uuid, 0),
  ('01000000-0000-0000-0000-000000000008'::uuid, '01000000-0000-0000-0000-000000000008'::uuid, 0),
  ('01000000-0000-0000-0000-000000000001'::uuid, '01000000-0000-0000-0000-000000000002'::uuid, 1),
  ('01000000-0000-0000-0000-000000000001'::uuid, '01000000-0000-0000-0000-000000000003'::uuid, 2),
  ('01000000-0000-0000-0000-000000000001'::uuid, '01000000-0000-0000-0000-000000000004'::uuid, 2),
  ('01000000-0000-0000-0000-000000000001'::uuid, '01000000-0000-0000-0000-000000000005'::uuid, 1),
  ('01000000-0000-0000-0000-000000000001'::uuid, '01000000-0000-0000-0000-000000000006'::uuid, 1),
  ('01000000-0000-0000-0000-000000000001'::uuid, '01000000-0000-0000-0000-000000000007'::uuid, 1),
  ('01000000-0000-0000-0000-000000000001'::uuid, '01000000-0000-0000-0000-000000000008'::uuid, 1),
  ('01000000-0000-0000-0000-000000000002'::uuid, '01000000-0000-0000-0000-000000000003'::uuid, 1),
  ('01000000-0000-0000-0000-000000000002'::uuid, '01000000-0000-0000-0000-000000000004'::uuid, 1)
) as closure(ancestor_id, descendant_id, depth)
on conflict (ancestor_id, descendant_id) do update set depth = excluded.depth;

insert into app.groups ("Id", name, owner_organizational_unit_id, publishing_policy) values
  ('03000000-0000-0000-0000-000000000001', 'Gerentes', null, 'ExplicitGrantOnly'),
  ('03000000-0000-0000-0000-000000000002', 'Comite de crisis', '01000000-0000-0000-0000-000000000002', 'OwnerScope'),
  ('03000000-0000-0000-0000-000000000003', 'Liderazgo', null, 'AdminOnly'),
  ('03000000-0000-0000-0000-000000000004', 'Demo viewers', null, 'AdminOnly')
on conflict ("Id") do update set
  name = excluded.name,
  owner_organizational_unit_id = excluded.owner_organizational_unit_id,
  publishing_policy = excluded.publishing_policy;

insert into app.users ("Id", email, display_name, password_hash, is_active, organizational_unit_id, access_scope_version) values
  ('02000000-0000-0000-0000-000000000001', 'admin@admin.com', 'Demo Admin', '$passwordHash', true, '01000000-0000-0000-0000-000000000001', 1),
  ('02000000-0000-0000-0000-000000000002', 'manager.comunicacion@demo.com', 'Manager Comunicacion', '$passwordHash', true, '01000000-0000-0000-0000-000000000002', 1),
  ('02000000-0000-0000-0000-000000000003', 'viewer.marketing@demo.com', 'Viewer Marketing', '$passwordHash', true, '01000000-0000-0000-0000-000000000003', 1),
  ('02000000-0000-0000-0000-000000000004', 'viewer.audiovisual@demo.com', 'Viewer Produccion Audiovisual', '$passwordHash', true, '01000000-0000-0000-0000-000000000004', 1),
  ('02000000-0000-0000-0000-000000000005', 'viewer.sistemas@demo.com', 'Viewer Sistemas', '$passwordHash', true, '01000000-0000-0000-0000-000000000007', 1),
  ('02000000-0000-0000-0000-000000000006', 'crisis.comunicacion@demo.com', 'Viewer Crisis Comunicacion', '$passwordHash', true, '01000000-0000-0000-0000-000000000003', 1)
on conflict ("Id") do update set
  email = excluded.email,
  display_name = excluded.display_name,
  password_hash = excluded.password_hash,
  is_active = excluded.is_active,
  organizational_unit_id = excluded.organizational_unit_id,
  access_scope_version = app.users.access_scope_version + 1;

insert into app.user_roles (user_id, role_id)
select user_id, roles."Id"
from (values
  ('02000000-0000-0000-0000-000000000001'::uuid, 'Admin'),
  ('02000000-0000-0000-0000-000000000002'::uuid, 'DocumentPublisher'),
  ('02000000-0000-0000-0000-000000000003'::uuid, 'Viewer'),
  ('02000000-0000-0000-0000-000000000004'::uuid, 'Viewer'),
  ('02000000-0000-0000-0000-000000000005'::uuid, 'Viewer'),
  ('02000000-0000-0000-0000-000000000006'::uuid, 'Viewer')
) as role_seed(user_id, role_name)
join app.roles roles on roles.name = role_seed.role_name
on conflict do nothing;

insert into app.user_groups (user_id, group_id) values
  ('02000000-0000-0000-0000-000000000001', '03000000-0000-0000-0000-000000000003'),
  ('02000000-0000-0000-0000-000000000002', '03000000-0000-0000-0000-000000000001'),
  ('02000000-0000-0000-0000-000000000002', '03000000-0000-0000-0000-000000000002'),
  ('02000000-0000-0000-0000-000000000006', '03000000-0000-0000-0000-000000000002')
on conflict do nothing;

insert into app.user_ai_budget_limits (user_id, monthly_budget_usd, is_disabled, updated_by_user_id) values
  ('02000000-0000-0000-0000-000000000001', 5.00, false, '02000000-0000-0000-0000-000000000001'),
  ('02000000-0000-0000-0000-000000000002', 5.00, false, '02000000-0000-0000-0000-000000000001'),
  ('02000000-0000-0000-0000-000000000003', 5.00, false, '02000000-0000-0000-0000-000000000001'),
  ('02000000-0000-0000-0000-000000000004', 5.00, false, '02000000-0000-0000-0000-000000000001'),
  ('02000000-0000-0000-0000-000000000005', 5.00, false, '02000000-0000-0000-0000-000000000001'),
  ('02000000-0000-0000-0000-000000000006', 5.00, false, '02000000-0000-0000-0000-000000000001')
on conflict (user_id) do update set
  monthly_budget_usd = excluded.monthly_budget_usd,
  is_disabled = excluded.is_disabled,
  updated_by_user_id = excluded.updated_by_user_id,
  updated_at = now();

insert into app.documents ("Id", title, current_state, created_by_user_id, created_at, updated_at) values
  ('04000000-0000-0000-0000-000000000001', 'Manual general de comunicacion interna', 'Published', '02000000-0000-0000-0000-000000000001', now(), now()),
  ('04000000-0000-0000-0000-000000000002', 'Guia del area Comunicacion', 'Published', '02000000-0000-0000-0000-000000000002', now(), now()),
  ('04000000-0000-0000-0000-000000000003', 'Calendario de campanas de Marketing', 'Published', '02000000-0000-0000-0000-000000000002', now(), now()),
  ('04000000-0000-0000-0000-000000000004', 'Checklist de produccion audiovisual', 'Published', '02000000-0000-0000-0000-000000000002', now(), now()),
  ('04000000-0000-0000-0000-000000000005', 'Procedimiento de guardias de Sistemas', 'Published', '02000000-0000-0000-0000-000000000001', now(), now()),
  ('04000000-0000-0000-0000-000000000006', 'Protocolo de comunicacion en crisis', 'Published', '02000000-0000-0000-0000-000000000002', now(), now())
on conflict ("Id") do update set
  title = excluded.title,
  current_state = excluded.current_state,
  updated_at = now();

insert into app.document_versions (
  "Id", document_id, version_number, state, title, document_type_id, content_html, published_at, published_by_user_id, indexing_status, created_at
)
select version_id, document_id, 1, 'Published', title, '20000000-0000-0000-0000-000000000001'::uuid, content_html, now(), published_by, 'Succeeded', now()
from (values
  ('05000000-0000-0000-0000-000000000001'::uuid, '04000000-0000-0000-0000-000000000001'::uuid, 'Manual general de comunicacion interna', '<h1>Manual general de comunicacion interna</h1><p>Lineamientos generales para comunicaciones internas de toda la empresa.</p>', '02000000-0000-0000-0000-000000000001'::uuid),
  ('05000000-0000-0000-0000-000000000002'::uuid, '04000000-0000-0000-0000-000000000002'::uuid, 'Guia del area Comunicacion', '<h1>Guia del area Comunicacion</h1><p>Procesos internos para el area Comunicacion y sus equipos dependientes.</p>', '02000000-0000-0000-0000-000000000002'::uuid),
  ('05000000-0000-0000-0000-000000000003'::uuid, '04000000-0000-0000-0000-000000000003'::uuid, 'Calendario de campanas de Marketing', '<h1>Calendario de campanas de Marketing</h1><p>Fechas y criterios de coordinacion para campanas de Marketing.</p>', '02000000-0000-0000-0000-000000000002'::uuid),
  ('05000000-0000-0000-0000-000000000004'::uuid, '04000000-0000-0000-0000-000000000004'::uuid, 'Checklist de produccion audiovisual', '<h1>Checklist de produccion audiovisual</h1><p>Pasos previos para piezas audiovisuales internas.</p>', '02000000-0000-0000-0000-000000000002'::uuid),
  ('05000000-0000-0000-0000-000000000005'::uuid, '04000000-0000-0000-0000-000000000005'::uuid, 'Procedimiento de guardias de Sistemas', '<h1>Procedimiento de guardias de Sistemas</h1><p>Rotacion y escalamiento para guardias del area Sistemas.</p>', '02000000-0000-0000-0000-000000000001'::uuid),
  ('05000000-0000-0000-0000-000000000006'::uuid, '04000000-0000-0000-0000-000000000006'::uuid, 'Protocolo de comunicacion en crisis', '<h1>Protocolo de comunicacion en crisis</h1><p>Canales, responsables y aprobaciones del comite de crisis.</p>', '02000000-0000-0000-0000-000000000002'::uuid)
) as version_seed(version_id, document_id, title, content_html, published_by)
on conflict (document_id, version_number) do update set
  title = excluded.title,
  document_type_id = excluded.document_type_id,
  content_html = excluded.content_html,
  state = excluded.state,
  published_at = excluded.published_at,
  published_by_user_id = excluded.published_by_user_id,
  indexing_status = excluded.indexing_status;

update app.documents documents
set current_published_version_id = version_seed.version_id,
    current_draft_version_id = null,
    updated_at = now()
from (values
  ('04000000-0000-0000-0000-000000000001'::uuid, '05000000-0000-0000-0000-000000000001'::uuid),
  ('04000000-0000-0000-0000-000000000002'::uuid, '05000000-0000-0000-0000-000000000002'::uuid),
  ('04000000-0000-0000-0000-000000000003'::uuid, '05000000-0000-0000-0000-000000000003'::uuid),
  ('04000000-0000-0000-0000-000000000004'::uuid, '05000000-0000-0000-0000-000000000004'::uuid),
  ('04000000-0000-0000-0000-000000000005'::uuid, '05000000-0000-0000-0000-000000000005'::uuid),
  ('04000000-0000-0000-0000-000000000006'::uuid, '05000000-0000-0000-0000-000000000006'::uuid)
) as version_seed(document_id, version_id)
where documents."Id" = version_seed.document_id;

insert into app.document_permissions ("Id", document_id, organizational_unit_id, group_id, created_at) values
  ('06000000-0000-0000-0000-000000000001', '04000000-0000-0000-0000-000000000001', '01000000-0000-0000-0000-000000000001', null, now()),
  ('06000000-0000-0000-0000-000000000002', '04000000-0000-0000-0000-000000000002', '01000000-0000-0000-0000-000000000002', null, now()),
  ('06000000-0000-0000-0000-000000000003', '04000000-0000-0000-0000-000000000003', '01000000-0000-0000-0000-000000000003', null, now()),
  ('06000000-0000-0000-0000-000000000004', '04000000-0000-0000-0000-000000000004', '01000000-0000-0000-0000-000000000004', null, now()),
  ('06000000-0000-0000-0000-000000000005', '04000000-0000-0000-0000-000000000005', '01000000-0000-0000-0000-000000000007', null, now()),
  ('06000000-0000-0000-0000-000000000006', '04000000-0000-0000-0000-000000000006', '01000000-0000-0000-0000-000000000002', null, now())
on conflict ("Id") do update set
  document_id = excluded.document_id,
  organizational_unit_id = excluded.organizational_unit_id,
  group_id = excluded.group_id;

insert into app.document_permission_groups (document_permission_id, group_id) values
  ('06000000-0000-0000-0000-000000000006', '03000000-0000-0000-0000-000000000002')
on conflict do nothing;

insert into rag.model_pricing (
  id, model_id, model_kind, input_token_price_usd, cached_token_price_usd, output_token_price_usd, effective_from, effective_to
) values
  ('30000000-0000-0000-0000-000000000001', 'gpt-4.1-nano', 'chat', 0.0000000001, 0.0000000000, 0.0000000004, '2026-01-01T00:00:00Z', null),
  ('30000000-0000-0000-0000-000000000002', 'text-embedding-3-small', 'embedding', 0.00000000002, null, null, '2026-01-01T00:00:00Z', null)
on conflict (id) do nothing;
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
Write-Host "Local hierarchical demo data seeded."
Write-Host "Credentials:"
Write-Host "  Admin:                  admin@admin.com / $demoPassword"
Write-Host "  Publisher Comunicacion: manager.comunicacion@demo.com / $demoPassword"
Write-Host "  Viewer Marketing:       viewer.marketing@demo.com / $demoPassword"
Write-Host "  Viewer Audiovisual:     viewer.audiovisual@demo.com / $demoPassword"
Write-Host "  Viewer Sistemas:        viewer.sistemas@demo.com / $demoPassword"
Write-Host "  Viewer Crisis:          crisis.comunicacion@demo.com / $demoPassword"
if ($WithSampleDocument) {
    Write-Host "Note: -WithSampleDocument is retained for compatibility; the hierarchy seed always creates the approved published demo documents."
}
