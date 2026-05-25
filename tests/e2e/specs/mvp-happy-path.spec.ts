import { execFileSync } from 'node:child_process'
import { pbkdf2Sync } from 'node:crypto'
import { randomUUID } from 'node:crypto'
import { fileURLToPath } from 'node:url'
import path from 'node:path'
import { expect, request as playwrightRequest, test, type APIRequestContext, type Page } from '@playwright/test'

const manageBaseUrl = process.env.E2E_MANAGE_URL ?? 'https://manage.localhost'
const chatBaseUrl = process.env.E2E_CHAT_URL ?? 'https://chat.localhost'
const docsBaseUrl = process.env.E2E_DOCS_URL ?? 'https://docs.localhost'
const caddyLoopbackUrl = process.env.E2E_CADDY_LOOPBACK_URL ?? 'https://127.0.0.1'

const adminEmail = 'e2e.admin@example.com'
const documentManagerEmail = 'e2e.manager@example.com'
const viewerEmail = 'e2e.viewer@example.com'
const e2ePassword = 'E2ePassword!42'

const adminUserId = '10000000-0000-0000-0000-000000000001'
const documentManagerUserId = '10000000-0000-0000-0000-000000000002'

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

test('MVP happy path works across management, chat, viewer, feedback, and budget controls', async ({ browser }) => {
  restartDotnetApi()
  seedE2eDatabase()

  const adminApi = await authenticatedApi('manage.localhost', adminEmail)
  const group = await postJson<{ id: string; name: string }>(adminApi, '/api/groups', {
    name: `E2E Viewers ${Date.now()}`,
  })
  const viewer = await postJson<{ id: string }>(adminApi, '/api/users', {
    email: viewerEmail,
    displayName: 'E2E Viewer',
    password: e2ePassword,
    roles: ['Viewer'],
    groupIds: [group.id],
  })

  const documentManagerApi = await authenticatedApi('manage.localhost', documentManagerEmail)
  const document = await postJson<DocumentDetail>(documentManagerApi, '/api/documents', {
    title: `E2E Seguridad ${Date.now()}`,
    documentType: 'Politica',
    audience: 'Equipo interno',
    contentHtml:
      '<h1>Seguridad</h1><p>Para aprobar una solicitud interna, el colaborador debe validar identidad y registrar el motivo en el sistema.</p>',
    allowedGroupIds: [group.id],
  })
  const inReview = await postJson<DocumentDetail>(
    documentManagerApi,
    `/api/documents/${document.id}/send-to-review`,
    { comment: 'Ready for E2E publication.' },
  )
  expect(inReview.state).toBe('In Review')

  const published = await postJson<DocumentDetail>(
    adminApi,
    `/api/documents/${document.id}/request-publish`,
    undefined,
    { timeout: 120_000 },
  )
  expect(published.state).toBe('Published')
  expect(published.currentPublishedVersion?.indexingStatus).toBe('Succeeded')

  await adminApi.dispose()
  await documentManagerApi.dispose()

  const viewerContext = await browser.newContext({
    ignoreHTTPSErrors: true,
  })
  const chatPage = await viewerContext.newPage()
  await chatPage.goto(chatBaseUrl)
  await loginInBrowser(chatPage, viewerEmail)
  await browserPost(chatPage, '/api/auth/chat-token')
  await chatPage.reload()
  await expect(chatPage.getByRole('heading', { name: 'Chat de instrucciones' })).toBeVisible()
  await chatPage.getByRole('textbox', { name: 'Pregunta' }).fill('Que debe hacer el colaborador para aprobar una solicitud interna?')
  await chatPage.getByRole('button', { name: 'Enviar pregunta' }).click()
  await expect(chatPage.getByRole('heading', { name: 'Respuesta' })).toBeVisible({ timeout: 120_000 })
  await expect(chatPage.getByLabel('Citas')).toContainText('Abrir cita')
  await chatPage.getByRole('button', { name: 'No me sirvió' }).click()
  await chatPage.getByRole('textbox', { name: 'Comentario opcional' }).fill('E2E feedback negativo')
  await chatPage.getByRole('button', { name: 'Enviar feedback' }).click()
  await expect(chatPage.getByText('Feedback registrado.')).toBeVisible()

  await chatPage.getByRole('button', { name: /Abrir cita/ }).first().click()
  await chatPage.waitForURL(/docs\.localhost\/open\?code=/)
  await expect(chatPage.getByRole('heading', { name: published.title })).toBeVisible()
  await expect(chatPage.getByText('validar identidad')).toBeVisible()

  const adminContext = await browser.newContext({
    ignoreHTTPSErrors: true,
  })
  const adminPage = await adminContext.newPage()
  await adminPage.goto(manageBaseUrl)
  await loginInBrowser(adminPage, adminEmail)
  await adminPage.reload()
  await adminPage.getByRole('link', { name: 'Feedback' }).click()
  await expect(adminPage.getByRole('heading', { name: 'Feedback auditado' })).toBeVisible()
  await expect(adminPage.getByText('E2E feedback negativo')).toBeVisible({ timeout: 30_000 })

  await adminPage.getByRole('link', { name: 'Usuarios y grupos' }).click()
  await adminPage.getByRole('button', { name: 'Actualizar usuarios' }).click()
  await expect(adminPage.getByRole('row', { name: /e2e\.viewer@example\.com/ })).toBeVisible()
  await adminPage.getByRole('button', { name: 'Editar presupuesto de E2E Viewer' }).click()
  const budgetInput = adminPage.getByRole('spinbutton', { name: 'Presupuesto mensual (USD)' })
  await budgetInput.fill('0')
  await adminPage.getByRole('button', { name: 'Guardar' }).click()
  await expect(adminPage.getByText('Presupuesto actualizado.')).toBeVisible()

  const limitedContext = await browser.newContext({
    ignoreHTTPSErrors: true,
  })
  const limitedPage = await limitedContext.newPage()
  await limitedPage.goto(chatBaseUrl)
  await loginInBrowser(limitedPage, viewerEmail)
  await browserPost(limitedPage, '/api/auth/chat-token')
  await limitedPage.reload()
  await limitedPage.getByRole('textbox', { name: 'Pregunta' }).fill('Puedo consultar otra vez?')
  await limitedPage.getByRole('button', { name: 'Enviar pregunta' }).click()
  await expect(limitedPage.getByText('Alcanzaste el presupuesto mensual de uso de IA.')).toBeVisible()

  await limitedContext.close()
  await adminContext.close()
  await viewerContext.close()

  expect(viewer.id).toBeTruthy()
})

interface DocumentDetail {
  id: string
  title: string
  state: string
  currentPublishedVersion: {
    id: string
    indexingStatus: string
  } | null
}

async function authenticatedApi(host: string, email: string): Promise<APIRequestContext> {
  const api = await playwrightRequest.newContext({
    baseURL: caddyLoopbackUrl,
    ignoreHTTPSErrors: true,
    extraHTTPHeaders: {
      Host: host,
      'X-Request-ID': randomUUID(),
    },
  })
  await getCsrf(api)
  await postJson(api, '/api/auth/login', {
    email,
    password: e2ePassword,
  })
  return api
}

async function loginInBrowser(page: Page, email: string): Promise<void> {
  await browserPost(page, '/api/auth/login', {
    email,
    password: e2ePassword,
  })
}

async function browserPost(page: Page, pathName: string, body?: unknown): Promise<void> {
  await page.evaluate(
    async ({ pathName: evaluatedPath, body: evaluatedBody }) => {
      const csrfResponse = await fetch('/api/csrf', {
        credentials: 'include',
        headers: {
          'X-Request-ID': crypto.randomUUID(),
        },
      })
      if (!csrfResponse.ok) {
        throw new Error(`GET /api/csrf failed with ${csrfResponse.status}: ${await csrfResponse.text()}`)
      }
      const csrf = csrfResponse.headers.get('X-CSRF-Token')
      const response = await fetch(evaluatedPath, {
        method: 'POST',
        credentials: 'include',
        headers: {
          'Content-Type': 'application/json',
          'X-CSRF-Token': csrf ?? '',
          'X-Request-ID': crypto.randomUUID(),
        },
        body: evaluatedBody === undefined ? undefined : JSON.stringify(evaluatedBody),
      })
      if (!response.ok) {
        throw new Error(`${evaluatedPath} failed with ${response.status}: ${await response.text()}`)
      }
    },
    { pathName, body },
  )
}

async function getCsrf(api: APIRequestContext): Promise<string> {
  let lastError: unknown = null
  for (let attempt = 0; attempt < 20; attempt += 1) {
    try {
      const response = await api.get('/api/csrf')
      if (response.ok()) {
        const token = response.headers()['x-csrf-token']
        expect(token, 'CSRF response header').toBeTruthy()
        return token
      }
      lastError = new Error(`GET /api/csrf failed with ${response.status()}: ${await response.text()}`)
    } catch (error) {
      lastError = error
    }
    await new Promise((resolve) => setTimeout(resolve, 1_000))
  }

  throw lastError instanceof Error ? lastError : new Error('GET /api/csrf failed.')
}

async function postJson<T = unknown>(
  api: APIRequestContext,
  pathName: string,
  body?: unknown,
  options: { timeout?: number } = {},
): Promise<T> {
  const csrf = await getCsrf(api)
  const response = await api.post(pathName, {
    data: body,
    headers: {
      'X-CSRF-Token': csrf,
      'X-Request-ID': randomUUID(),
    },
    timeout: options.timeout,
  })
  await expectOk(response, `POST ${pathName}`)
  return (await response.json()) as T
}

async function expectOk(response: { ok(): boolean; status(): number; text(): Promise<string> }, label: string) {
  if (!response.ok()) {
    throw new Error(`${label} failed with ${response.status()}: ${await response.text()}`)
  }
}

function seedE2eDatabase() {
  const passwordHash = createPasswordHash(e2ePassword)
  const sql = `
delete from rag.query_audit_citations where query_audit_event_id in (
  select id from rag.query_audit_events where user_id in ('${adminUserId}', '${documentManagerUserId}')
     or user_id in (select "Id" from app.users where email like 'e2e.%@example.com')
);
delete from rag.query_audit_events where user_id in ('${adminUserId}', '${documentManagerUserId}')
   or user_id in (select "Id" from app.users where email like 'e2e.%@example.com');
delete from rag.semantic_cache_sources where document_id in (
  select "Id" from app.documents where title like 'E2E Seguridad%'
);
delete from rag.document_chunks where document_id in (
  select "Id" from app.documents where title like 'E2E Seguridad%'
);
delete from rag.indexing_jobs where document_id in (
  select "Id" from app.documents where title like 'E2E Seguridad%'
);
delete from app.viewer_token_audit where user_id in (
  select "Id" from app.users where email like 'e2e.%@example.com'
) or document_id in (
  select "Id" from app.documents where title like 'E2E Seguridad%'
);
delete from app.viewer_exchange_codes where user_id in (
  select "Id" from app.users where email like 'e2e.%@example.com'
) or document_id in (
  select "Id" from app.documents where title like 'E2E Seguridad%'
);
delete from app.review_comments where document_version_id in (
  select "Id" from app.document_versions where document_id in (
    select "Id" from app.documents where title like 'E2E Seguridad%'
  )
);
delete from app.import_metadata where document_version_id in (
  select "Id" from app.document_versions where document_id in (
    select "Id" from app.documents where title like 'E2E Seguridad%'
  )
);
delete from app.document_permissions where document_id in (
  select "Id" from app.documents where title like 'E2E Seguridad%'
);
delete from app.document_tags where document_id in (
  select "Id" from app.documents where title like 'E2E Seguridad%'
);
delete from app.document_versions where document_id in (
  select "Id" from app.documents where title like 'E2E Seguridad%'
);
delete from app.documents where title like 'E2E Seguridad%';
delete from app.user_ai_budget_limits where user_id in (
  select "Id" from app.users where email like 'e2e.%@example.com'
);
delete from app.user_roles where user_id in (
  select "Id" from app.users where email like 'e2e.%@example.com'
);
delete from app.user_groups where user_id in (
  select "Id" from app.users where email like 'e2e.%@example.com'
);
delete from app.audit_events where actor_user_id in (
  select "Id" from app.users where email like 'e2e.%@example.com'
);
delete from app.users where email like 'e2e.%@example.com';
delete from app.groups where name like 'E2E Viewers%';

insert into app.roles ("Id", name) values
  ('00000000-0000-0000-0000-0000000000a1', 'Admin'),
  ('00000000-0000-0000-0000-0000000000a2', 'DocumentManager'),
  ('00000000-0000-0000-0000-0000000000a3', 'Viewer')
on conflict (name) do nothing;

insert into app.users ("Id", email, display_name, password_hash, is_active) values
  ('${adminUserId}', '${adminEmail}', 'E2E Admin', '${passwordHash}', true),
  ('${documentManagerUserId}', '${documentManagerEmail}', 'E2E Document Manager', '${passwordHash}', true);

insert into app.user_roles (user_id, role_id)
select '${adminUserId}'::uuid, "Id" from app.roles where name = 'Admin';
insert into app.user_roles (user_id, role_id)
select '${documentManagerUserId}'::uuid, "Id" from app.roles where name = 'DocumentManager';

insert into app.user_ai_budget_limits (user_id, monthly_budget_usd, is_disabled, updated_by_user_id) values
  ('${adminUserId}', 5.00, false, '${adminUserId}'),
  ('${documentManagerUserId}', 5.00, false, '${adminUserId}');

insert into rag.model_pricing (
  id, model_id, model_kind, input_token_price_usd, cached_token_price_usd, output_token_price_usd, effective_from, effective_to
) values
  ('30000000-0000-0000-0000-000000000001', 'gpt-4.1-nano', 'chat', 0.0000000001, 0.0000000000, 0.0000000004, '2026-01-01T00:00:00Z', null),
  ('30000000-0000-0000-0000-000000000002', 'text-embedding-3-small', 'embedding', 0.00000000002, null, null, '2026-01-01T00:00:00Z', null)
on conflict (id) do nothing;
`

  execFileSync('docker', [...composeArgs, 'exec', '-T', 'postgres', 'sh', '-lc', postgresSeedCommand()], {
    cwd: repoRoot,
    input: sql,
    stdio: ['pipe', 'pipe', 'pipe'],
    timeout: 60_000,
  })
}

function restartDotnetApi() {
  execFileSync('docker', [...composeArgs, 'restart', 'dotnet-api'], {
    cwd: repoRoot,
    stdio: ['ignore', 'pipe', 'pipe'],
    timeout: 60_000,
  })
  waitForDotnetApi()
}

function waitForDotnetApi() {
  let lastError: unknown = null
  for (let attempt = 0; attempt < 30; attempt += 1) {
    try {
      execFileSync(
        'docker',
        [
          ...composeArgs,
          'exec',
          '-T',
          'dotnet-api',
          'sh',
          '-lc',
          'curl -fsS http://localhost:8080/health/ready >/dev/null',
        ],
        {
          cwd: repoRoot,
          stdio: ['ignore', 'pipe', 'pipe'],
          timeout: 10_000,
        },
      )
      return
    } catch (error) {
      lastError = error
      sleepSync(1_000)
    }
  }

  throw lastError instanceof Error ? lastError : new Error('dotnet-api did not become ready.')
}

function sleepSync(milliseconds: number) {
  Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, milliseconds)
}

function postgresSeedCommand() {
  return 'PGPASSWORD="$(cat /run/secrets/postgres_admin_password)" psql -v ON_ERROR_STOP=1 -U postgres -d advanced_rag'
}

function createPasswordHash(password: string) {
  const salt = Buffer.from('advanced-rag-e2e')
  const hash = pbkdf2Sync(password, salt, 210_000, 32, 'sha256')
  return `pbkdf2-sha256$210000$${salt.toString('base64')}$${hash.toString('base64')}`
}
