import { execFileSync } from 'node:child_process'
import { fileURLToPath } from 'node:url'
import path from 'node:path'
import { expect, test, type Page } from '@playwright/test'

const manageBaseUrl = process.env.E2E_MANAGE_URL ?? 'https://manage.localhost'
const chatBaseUrl = process.env.E2E_CHAT_URL ?? 'https://chat.localhost'
const docsBaseUrl = process.env.E2E_DOCS_URL ?? 'https://docs.localhost'

const firstAdminEmail = 'first-run.admin@example.com'
const viewerEmail = 'first-run.viewer@example.com'
const e2ePassword = 'FirstRunPassword!42'

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

test('setup fallback creates an admin and reaches chat and viewer product surfaces', async ({ browser }) => {
  resetDatabaseForFirstRun()
  restartDotnetApi()

  const unauthenticatedChat = await browser.newContext({ ignoreHTTPSErrors: true })
  const unauthenticatedChatPage = await unauthenticatedChat.newPage()
  await unauthenticatedChatPage.goto(chatBaseUrl)
  await expect(unauthenticatedChatPage.getByRole('heading', { name: /iniciar sesion/i })).toBeVisible()
  await expect(unauthenticatedChatPage.getByRole('button', { name: 'Entrar al chat' })).toBeVisible()
  await unauthenticatedChat.close()

  const unauthenticatedDocs = await browser.newContext({ ignoreHTTPSErrors: true })
  const unauthenticatedDocsPage = await unauthenticatedDocs.newPage()
  await unauthenticatedDocsPage.goto(docsBaseUrl)
  await expect(unauthenticatedDocsPage.getByRole('heading', { name: /iniciar sesion/i })).toBeVisible()
  await expect(unauthenticatedDocsPage.getByRole('button', { name: 'Entrar' })).toBeVisible()
  await unauthenticatedDocs.close()

  const adminContext = await browser.newContext({ ignoreHTTPSErrors: true })
  const adminPage = await adminContext.newPage()
  await adminPage.goto(manageBaseUrl)
  await expect(adminPage.getByRole('heading', { name: /primer administrador/i })).toBeVisible()

  await adminPage.getByRole('textbox', { name: 'Email' }).fill(firstAdminEmail)
  await adminPage.getByRole('textbox', { name: 'Nombre visible' }).fill('First Run Admin')
  await adminPage.getByLabel(/Contrase/).fill(e2ePassword)
  await adminPage.getByRole('button', { name: 'Crear administrador' }).click()

  await expect(adminPage.getByRole('heading', { name: /Ingres/ })).toBeVisible()
  await adminPage.getByRole('textbox', { name: 'Email' }).fill(firstAdminEmail)
  await adminPage.getByLabel(/Contrase/).fill(e2ePassword)
  await adminPage.getByRole('button', { name: 'Ingresar' }).click()
  await expect(adminPage.getByRole('heading', { name: 'Usuarios y grupos' })).toBeVisible()

  const groupName = `Primer uso ${Date.now()}`
  await adminPage.getByRole('button', { name: 'Crear grupo' }).click()
  await adminPage.getByRole('textbox', { name: 'Nombre del grupo' }).fill(groupName)
  await adminPage.getByRole('button', { name: 'Guardar grupo' }).click()
  await expect(adminPage.getByText('Grupo creado.')).toBeVisible()

  await adminPage.getByRole('button', { name: 'Crear usuario' }).click()
  await adminPage.getByRole('textbox', { name: 'Email' }).fill(viewerEmail)
  await adminPage.getByRole('textbox', { name: 'Nombre visible' }).fill('First Run Viewer')
  await adminPage.getByLabel(/Contrase/).fill(e2ePassword)
  await adminPage.getByRole('combobox', { name: 'Rol' }).selectOption('Viewer')
  await adminPage.getByRole('checkbox', { name: groupName }).click()
  await adminPage.getByRole('button', { name: 'Crear usuario' }).click()
  await expect(adminPage.getByText('Usuario creado.')).toBeVisible()
  await expect(adminPage.getByRole('row', { name: /First Run Viewer/ })).toBeVisible()

  const groups = await browserGetJson<Array<{ id: string; name: string }>>(adminPage, '/api/groups')
  const group = groups.find((item) => item.name === groupName)
  if (!group) {
    throw new Error('Created group was not returned by product API.')
  }

  const document = await browserPostJson<DocumentDetail>(adminPage, '/api/documents', {
    title: `Primer uso seguridad ${Date.now()}`,
    documentType: 'Politica',
    audience: 'Equipo interno',
    contentHtml:
      '<h1>Seguridad</h1><p>Para aprobar una solicitud interna, el colaborador debe validar identidad y registrar el motivo.</p>',
    allowedGroupIds: [group.id],
  })
  await browserPostJson<DocumentDetail>(adminPage, `/api/documents/${document.id}/send-to-review`, {
    comment: 'Ready for first-run verification.',
  })
  const published = await browserPostJson<DocumentDetail>(
    adminPage,
    `/api/documents/${document.id}/request-publish`,
    undefined,
    { timeout: 120_000 },
  )
  expect(published.state).toBe('Published')

  const viewerContext = await browser.newContext({ ignoreHTTPSErrors: true })
  const chatPage = await viewerContext.newPage()
  await chatPage.goto(chatBaseUrl)
  await browserPostJson(chatPage, '/api/auth/login', {
    email: viewerEmail,
    password: e2ePassword,
  })
  await chatPage.reload()
  await expect(chatPage.getByRole('heading', { name: 'Chat de instrucciones' })).toBeVisible()
  await chatPage
    .getByRole('textbox', { name: 'Pregunta' })
    .fill('Que debe hacer el colaborador para aprobar una solicitud interna?')
  await chatPage.getByRole('button', { name: 'Enviar pregunta' }).click()
  await expect(chatPage.getByRole('heading', { name: 'Respuesta' })).toBeVisible({ timeout: 120_000 })
  await expect(chatPage.getByLabel('Citas')).toContainText('Abrir cita')

  await chatPage.getByRole('button', { name: /Abrir cita/ }).first().click()
  await chatPage.waitForURL(/docs\.localhost\/open\?documentId=/)
  await expect(chatPage.getByRole('heading', { name: published.title })).toBeVisible()
  await expect(chatPage.getByText('validar identidad')).toBeVisible()

  await viewerContext.close()
  await adminContext.close()
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

async function browserGetJson<T>(page: Page, pathName: string): Promise<T> {
  return page.evaluate(async (evaluatedPath) => {
    const response = await fetch(evaluatedPath, {
      credentials: 'include',
      headers: {
        'X-Request-ID': crypto.randomUUID(),
      },
    })
    if (!response.ok) {
      throw new Error(`GET ${evaluatedPath} failed with ${response.status}: ${await response.text()}`)
    }

    return (await response.json()) as T
  }, pathName)
}

async function browserPostJson<T = unknown>(
  page: Page,
  pathName: string,
  body?: unknown,
  _options: { timeout?: number } = {},
): Promise<T> {
  return page.evaluate(
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

      const text = await response.text()
      return text.length > 0 ? (JSON.parse(text) as T) : (null as T)
    },
    { pathName, body },
  )
}

function resetDatabaseForFirstRun() {
  const sql = `
do $$
declare
  table_names text;
begin
  select string_agg(format('%I.%I', schemaname, tablename), ', ')
    into table_names
  from pg_tables
  where schemaname in ('app', 'rag')
    and not (schemaname = 'app' and tablename = '__EFMigrationsHistory')
    and not (schemaname = 'rag' and tablename = 'alembic_version');

  if table_names is not null then
    execute 'truncate table ' || table_names || ' restart identity cascade';
  end if;
end $$;

insert into rag.model_pricing (
  id, model_id, model_kind, input_token_price_usd, cached_token_price_usd, output_token_price_usd, effective_from, effective_to
) values
  ('30000000-0000-0000-0000-000000000001', 'gpt-4.1-nano', 'chat', 0.0000000001, 0.0000000000, 0.0000000004, '2026-01-01T00:00:00Z', null),
  ('30000000-0000-0000-0000-000000000002', 'text-embedding-3-small', 'embedding', 0.00000000002, null, null, '2026-01-01T00:00:00Z', null);
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
