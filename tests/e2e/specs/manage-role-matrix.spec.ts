import { execFileSync } from 'node:child_process'
import { pbkdf2Sync, randomUUID } from 'node:crypto'
import { fileURLToPath } from 'node:url'
import path from 'node:path'
import { expect, test, type Page } from '@playwright/test'

const manageBaseUrl = process.env.E2E_MANAGE_URL ?? 'https://manage.localhost'
const rolePassword = 'RoleMatrixPassword!42'
const adminEmail = 'role.admin@example.com'
const managerEmail = 'role.manager@example.com'
const viewerEmail = 'role.viewer@example.com'
const targetViewerId = '30000000-0000-0000-0000-000000000003'
const seedGroupId = '30000000-0000-0000-0000-000000000010'

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../..')
const composeArgs = [
  'compose',
  '--env-file',
  'infra/compose/.env.example',
  '-f',
  'infra/compose/compose.yaml',
  '-f',
  'infra/compose/compose.override.yaml',
]

test.beforeEach(() => {
  seedRoleMatrixUsers()
})

test('viewer has only management self-service and forbidden direct management APIs', async ({ page }) => {
  await page.goto(manageBaseUrl)
  await login(page, viewerEmail)

  await expect(page.getByRole('heading', { name: 'Mi cuenta' })).toBeVisible()
  await expect(page.getByRole('link', { name: 'Mi cuenta' })).toBeVisible()
  await expect(page.getByRole('link', { name: /Configuraci/ })).toBeVisible()
  await expect(page.getByRole('link', { name: 'Documentos' })).toHaveCount(0)
  await expect(page.getByRole('link', { name: 'Usuarios y grupos' })).toHaveCount(0)
  await expect(page.getByRole('link', { name: /Auditor/ })).toHaveCount(0)
  await expect(page.getByRole('link', { name: 'Feedback' })).toHaveCount(0)

  await expectApiStatus(page, 'GET', '/api/configuration', 200)
  await expectApiStatus(page, 'GET', '/api/users', 403)
  await expectApiStatus(page, 'GET', '/api/groups', 403)
  await expectApiStatus(page, 'GET', '/api/documents', 403)
  await expectApiStatus(page, 'GET', '/api/audit/events', 403)
  await expectApiStatus(page, 'GET', '/api/reporting/feedback?negativeOnly=false', 403)
})

test('document manager can manage groups and assignments without admin-only user actions', async ({ page }) => {
  await page.goto(manageBaseUrl)
  await login(page, managerEmail)

  await expect(page.getByRole('heading', { name: 'Usuarios y grupos' })).toBeVisible()
  await expect(page.getByRole('link', { name: 'Documentos' })).toBeVisible()
  await expect(page.getByRole('link', { name: 'Usuarios y grupos' })).toBeVisible()
  await expect(page.getByRole('link', { name: /Auditor/ })).toBeVisible()
  await expect(page.getByRole('link', { name: 'Feedback' })).toBeVisible()
  await expect(page.getByRole('link', { name: /Configuraci/ })).toBeVisible()
  await expect(page.getByRole('button', { name: 'Crear usuario' })).toHaveCount(0)
  await expect(page.getByRole('button', { name: /Editar presupuesto de Role Viewer/ })).toHaveCount(0)
  await expect(page.getByRole('button', { name: /Dar de baja a Role Viewer/ })).toHaveCount(0)
  await expect(page.getByRole('button', { name: /Editar usuario Role Viewer/ })).toBeVisible()

  await page.getByRole('tab', { name: 'Grupos' }).click()
  await expect(page.getByRole('button', { name: 'Crear grupo' })).toBeVisible()
  await expect(page.getByRole('button', { name: /Editar grupo Role Matrix Base/ })).toBeVisible()

  const createdGroup = await apiJson<{ id: string; name: string }>(page, 'POST', '/api/groups', {
    name: `Role Matrix ${Date.now()}`,
  })
  await expectApiStatus(page, 'PUT', `/api/groups/${createdGroup.id}`, 200, {
    name: `${createdGroup.name} Updated`,
  })
  await expectApiStatus(page, 'PUT', `/api/users/${targetViewerId}/groups`, 200, {
    groupIds: [createdGroup.id],
  })
  await expectApiStatus(page, 'POST', '/api/users', 403, {
    email: `blocked-${Date.now()}@example.com`,
    displayName: 'Blocked User',
    password: rolePassword,
    roles: ['Viewer'],
    groupIds: [],
  })
  await expectApiStatus(page, 'PUT', `/api/users/${targetViewerId}/roles`, 403, {
    roles: ['Admin'],
  })
  await expectApiStatus(page, 'PATCH', `/api/users/${targetViewerId}/status`, 403, {
    isActive: false,
  })
  await expectApiStatus(page, 'PUT', `/api/users/${targetViewerId}/ai-budget`, 403, {
    monthlyBudgetUsd: 1,
    isDisabled: false,
  })

  const document = await apiJson<{ id: string }>(page, 'POST', '/api/documents', {
    title: `Role matrix document ${Date.now()}`,
    documentType: 'Policy',
    audience: 'Internal',
    contentHtml: '<p>Role matrix publication guard.</p>',
    allowedGroupIds: [createdGroup.id],
  })
  await expectApiStatus(page, 'POST', `/api/documents/${document.id}/send-to-review`, 200, {
    comment: 'Ready for admin-only publish check.',
  })
  await expectApiStatus(page, 'POST', `/api/documents/${document.id}/request-publish`, 403)
})

async function login(page: Page, email: string) {
  if (await page.getByRole('heading', { name: 'Usuarios y grupos' }).isVisible({ timeout: 1_000 }).catch(() => false)) {
    return
  }

  await expect(page.getByRole('textbox', { name: 'Email' })).toBeVisible()
  await page.getByRole('textbox', { name: 'Email' }).fill(email)
  await page.getByLabel(/Contrase/).fill(rolePassword)
  await page.getByRole('button', { name: 'Ingresar' }).click()
}

async function expectApiStatus(
  page: Page,
  method: string,
  pathName: string,
  expectedStatus: number,
  body?: unknown,
) {
  const status = await apiStatus(page, method, pathName, body)
  expect(status).toBe(expectedStatus)
}

async function apiStatus(page: Page, method: string, pathName: string, body?: unknown): Promise<number> {
  return page.evaluate(
    async ({ method, pathName, body }) => {
      const headers: Record<string, string> = {
        'X-Request-ID': crypto.randomUUID(),
      }
      if (method !== 'GET') {
        const csrf = await fetch('/api/csrf', {
          credentials: 'include',
          headers: { 'X-Request-ID': crypto.randomUUID() },
        })
        headers['X-CSRF-Token'] = csrf.headers.get('X-CSRF-Token') ?? ''
        headers['Content-Type'] = 'application/json'
      }

      const response = await fetch(pathName, {
        method,
        credentials: 'include',
        headers,
        body: body === undefined ? undefined : JSON.stringify(body),
      })
      return response.status
    },
    { method, pathName, body },
  )
}

async function apiJson<T>(page: Page, method: string, pathName: string, body?: unknown): Promise<T> {
  return page.evaluate(
    async ({ method, pathName, body }) => {
      const csrf = await fetch('/api/csrf', {
        credentials: 'include',
        headers: { 'X-Request-ID': crypto.randomUUID() },
      })
      const response = await fetch(pathName, {
        method,
        credentials: 'include',
        headers: {
          'Content-Type': 'application/json',
          'X-CSRF-Token': csrf.headers.get('X-CSRF-Token') ?? '',
          'X-Request-ID': crypto.randomUUID(),
        },
        body: body === undefined ? undefined : JSON.stringify(body),
      })
      if (!response.ok) {
        throw new Error(`${method} ${pathName} failed with ${response.status}: ${await response.text()}`)
      }

      return (await response.json()) as T
    },
    { method, pathName, body },
  )
}

function seedRoleMatrixUsers() {
  const passwordHash = createPasswordHash(rolePassword)
  const sql = `
insert into app.roles ("Id", name) values
  ('00000000-0000-0000-0000-0000000000a1', 'Admin'),
  ('00000000-0000-0000-0000-0000000000a2', 'DocumentManager'),
  ('00000000-0000-0000-0000-0000000000a3', 'Viewer')
on conflict (name) do nothing;

delete from app.user_ai_budget_limits where user_id in (
  '30000000-0000-0000-0000-000000000001',
  '30000000-0000-0000-0000-000000000002',
  '${targetViewerId}'
);
delete from app.user_roles where user_id in (
  '30000000-0000-0000-0000-000000000001',
  '30000000-0000-0000-0000-000000000002',
  '${targetViewerId}'
);
delete from app.user_groups where user_id in (
  '30000000-0000-0000-0000-000000000001',
  '30000000-0000-0000-0000-000000000002',
  '${targetViewerId}'
);

insert into app.groups ("Id", name) values ('${seedGroupId}', 'Role Matrix Base')
on conflict ("Id") do update set name = excluded.name;

insert into app.users ("Id", email, display_name, password_hash, is_active) values
  ('30000000-0000-0000-0000-000000000001', '${adminEmail}', 'Role Admin', '${passwordHash}', true),
  ('30000000-0000-0000-0000-000000000002', '${managerEmail}', 'Role Manager', '${passwordHash}', true),
  ('${targetViewerId}', '${viewerEmail}', 'Role Viewer', '${passwordHash}', true)
on conflict ("Id") do update set
  email = excluded.email,
  display_name = excluded.display_name,
  password_hash = excluded.password_hash,
  is_active = excluded.is_active;

insert into app.user_roles (user_id, role_id)
select '30000000-0000-0000-0000-000000000001'::uuid, "Id" from app.roles where name = 'Admin'
union all
select '30000000-0000-0000-0000-000000000002'::uuid, "Id" from app.roles where name = 'DocumentManager'
union all
select '${targetViewerId}'::uuid, "Id" from app.roles where name = 'Viewer';

insert into app.user_groups (user_id, group_id) values
  ('30000000-0000-0000-0000-000000000002', '${seedGroupId}'),
  ('${targetViewerId}', '${seedGroupId}');

insert into app.user_ai_budget_limits (user_id, monthly_budget_usd, is_disabled, updated_by_user_id) values
  ('30000000-0000-0000-0000-000000000001', 5.00, false, '30000000-0000-0000-0000-000000000001'),
  ('30000000-0000-0000-0000-000000000002', 5.00, false, '30000000-0000-0000-0000-000000000001'),
  ('${targetViewerId}', 5.00, false, '30000000-0000-0000-0000-000000000001');
`

  execFileSync('docker', [...composeArgs, 'exec', '-T', 'postgres', 'sh', '-lc', postgresSeedCommand()], {
    cwd: repoRoot,
    input: sql,
    stdio: ['pipe', 'pipe', 'pipe'],
    timeout: 60_000,
  })
}

function postgresSeedCommand() {
  return 'PGPASSWORD="$(cat /run/secrets/postgres_admin_password)" psql -v ON_ERROR_STOP=1 -U postgres -d advanced_rag'
}

function createPasswordHash(password: string) {
  const salt = Buffer.from(`advanced-rag-role-matrix-${randomUUID()}`)
  const hash = pbkdf2Sync(password, salt, 210_000, 32, 'sha256')
  return `pbkdf2-sha256$210000$${salt.toString('base64')}$${hash.toString('base64')}`
}
