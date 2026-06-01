import { execFileSync } from 'node:child_process'
import { pbkdf2Sync, randomUUID } from 'node:crypto'
import { fileURLToPath } from 'node:url'
import path from 'node:path'
import { expect, test, type Page } from '@playwright/test'

const manageBaseUrl = process.env.E2E_MANAGE_URL ?? 'https://manage.localhost'
const adminEmail = 'i18n.admin@example.com'
const adminPassword = 'I18nPassword!42'

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

const sections = [
  {
    id: 'users',
    esNav: 'Usuarios y grupos',
    enNav: 'Users & groups',
    esExpected: ['Administracion', 'Usuarios y grupos', 'Crear usuario', 'Buscar usuarios'],
    enExpected: ['Administration', 'Users & groups', 'Create user', 'Search users'],
    forbiddenInEnglish: ['Nuevo usuario', 'Actualizar usuarios', 'Buscar usuarios', 'Presupuesto base'],
  },
  {
    id: 'groups',
    navId: 'users',
    esNav: 'Usuarios y grupos',
    enNav: 'Users & groups',
    beforeAudit: async (page: Page) => {
      await page.getByRole('tab', { name: 'Grupos' }).click()
    },
    beforeEnglishAudit: async (page: Page) => {
      await page.getByRole('tab', { name: 'Groups' }).click()
    },
    esExpected: ['Grupos', 'Acceso documental', 'Grupo', 'Usuarios'],
    enExpected: ['Groups', 'Group', 'Users'],
    forbiddenInEnglish: ['Acceso documental', 'Crear grupo', 'Actualizar usuarios'],
  },
  {
    id: 'documents',
    esNav: 'Documentos',
    enNav: 'Documents',
    esExpected: ['Instrucciones', 'Documentos', 'Crear documento', 'Buscar documentos'],
    enExpected: ['Instructions', 'Documents', 'Create document', 'Search documents'],
    forbiddenInEnglish: ['Filtros de documentos', 'Estado del documento', 'Todos', 'Cargando documentos'],
  },
  {
    id: 'document-editor',
    navId: 'documents',
    esNav: 'Documentos',
    enNav: 'Documents',
    beforeAudit: async (page: Page) => {
      await page.getByRole('button', { name: 'Crear documento' }).click()
    },
    beforeEnglishAudit: async (page: Page) => {
      await page.getByRole('button', { name: 'Create document' }).click()
    },
    esExpected: ['Editor HTML', 'Crear documento', 'Titulo', 'Guardar borrador'],
    enExpected: ['HTML editor', 'Create document', 'Title', 'Save draft'],
    forbiddenInEnglish: ['Volver al listado', 'Seleccionar archivo', 'Ningun archivo seleccionado', 'Cambios sin guardar'],
  },
  {
    id: 'audit',
    esNav: 'Auditoria',
    enNav: 'Audit',
    esExpected: ['Control', 'Auditoria', 'Buscar eventos', 'Tipo'],
    enExpected: ['Control', 'Audit', 'Search events', 'Type'],
    forbiddenInEnglish: ['Auditoria', 'Buscar eventos', 'Filtros de auditoria', 'Eventos funcionales'],
  },
  {
    id: 'feedback',
    esNav: 'Feedback',
    enNav: 'Feedback',
    esExpected: ['Revision de chat', 'Feedback', 'Polaridad', 'Aplicar filtros'],
    enExpected: ['Chat review', 'Feedback', 'Polarity', 'Apply filters'],
    forbiddenInEnglish: ['Revision de chat', 'Polaridad', 'Documento citado', 'Aplicar filtros'],
  },
  {
    id: 'configuration',
    esNav: 'Configuracion',
    enNav: 'Configuration',
    esExpected: ['Operaciones', 'Configuracion operativa', 'Modelos de IA', 'Cache y limites'],
    enExpected: ['Operations', 'Operational configuration', 'AI models', 'Cache and limits'],
    forbiddenInEnglish: ['Configuracion operativa', 'Presupuesto mensual base', 'Valores protegidos por secretos'],
  },
  {
    id: 'account',
    esNav: 'Mi cuenta',
    enNav: 'My account',
    esExpected: ['Sesion', 'Mi cuenta', 'Guardar email', 'Contraseña actual'],
    enExpected: ['Session', 'My account', 'Save email', 'Current password'],
    forbiddenInEnglish: ['Sesion', 'Guardar email', 'Contraseña actual', 'Nueva contraseña'],
  },
] satisfies Array<{
  id: string
  esNav: string
  enNav: string
  beforeAudit?: (page: Page) => Promise<void>
  beforeEnglishAudit?: (page: Page) => Promise<void>
  navId?: string
  esExpected: string[]
  enExpected: string[]
  forbiddenInEnglish: string[]
}>

test('admin management screens switch static UI copy between Spanish and English', async ({ page }) => {
  seedI18nAdmin()
  const issues: string[] = []

  await page.goto(manageBaseUrl)
  await loginIfNeeded(page)
  await expect(page.getByRole('heading', { name: 'Usuarios y grupos' })).toBeVisible()

  await switchLanguage(page, 'es-AR')
  for (const section of sections) {
    await openSpanishSection(page, section)
    await expectStaticCopy(page, section.esExpected, `Spanish ${section.id}`, issues)
    await expectNoMojibake(page, `Spanish ${section.id}`, issues)
  }

  await switchLanguage(page, 'en-US')
  for (const section of sections) {
    await openEnglishSection(page, section)
    await expectStaticCopy(page, section.enExpected, `English ${section.id}`, issues)
    await expectForbiddenCopyAbsent(page, section.forbiddenInEnglish, `English ${section.id}`, issues)
    await expectNoMojibake(page, `English ${section.id}`, issues)
  }

  expect(issues).toEqual([])
})

async function loginIfNeeded(page: Page) {
  await page.waitForLoadState('networkidle')

  if (await page.getByRole('heading', { name: 'Usuarios y grupos' }).isVisible({ timeout: 1_000 })) {
    return
  }

  if (await page.getByRole('textbox', { name: 'Email' }).isVisible({ timeout: 1_000 })) {
    await page.getByLabel('Email').fill(adminEmail)
    await page.getByLabel(/Contrase/).fill(adminPassword)
    await page.getByRole('button', { name: 'Ingresar' }).click()
    await expect(page.getByRole('heading', { name: 'Usuarios y grupos' })).toBeVisible()
    return
  }

  throw new Error('Management app did not render the login or authenticated admin shell.')
}

async function switchLanguage(page: Page, locale: 'es-AR' | 'en-US') {
  const label = locale === 'es-AR' ? /Language|Idioma/ : /Idioma|Language/
  await page.getByLabel(label).selectOption(locale)
  await expect(page.getByLabel(locale === 'es-AR' ? 'Idioma' : 'Language')).toHaveValue(locale)
}

async function openSpanishSection(page: Page, section: (typeof sections)[number]) {
  await page.locator(`.sidebar-nav a[href="#${section.navId ?? section.id}"]`).click()
  if (section.beforeAudit) {
    await section.beforeAudit(page)
  }
  await page.waitForLoadState('networkidle')
  await waitForManagementSettled(page)
}

async function openEnglishSection(page: Page, section: (typeof sections)[number]) {
  await page.locator(`.sidebar-nav a[href="#${section.navId ?? section.id}"]`).click()
  if (section.beforeEnglishAudit) {
    await section.beforeEnglishAudit(page)
  }
  await page.waitForLoadState('networkidle')
  await waitForManagementSettled(page)
}

async function waitForManagementSettled(page: Page) {
  await expect(page.locator('.status-message').filter({ hasText: /^(Loading|Cargando)/i })).toHaveCount(0)
}

async function expectStaticCopy(page: Page, expected: string[], context: string, issues: string[]) {
  const visibleText = normalizeForLanguageAudit(await visibleBodyText(page))
  for (const text of expected) {
    if (!visibleText.includes(normalizeForLanguageAudit(text))) {
      issues.push(`${context}: missing expected static copy "${text}".`)
    }
  }
}

async function expectForbiddenCopyAbsent(page: Page, forbidden: string[], context: string, issues: string[]) {
  const visibleText = await visibleBodyText(page)
  for (const text of forbidden) {
    if (visibleText.includes(text)) {
      issues.push(`${context}: stale Spanish copy is still visible: "${text}".`)
    }
  }
}

async function expectNoMojibake(page: Page, context: string, issues: string[]) {
  const visibleText = await visibleBodyText(page)
  if (/[ÃÂâ�]/.test(visibleText)) {
    issues.push(`${context}: visible text contains UTF-8 mojibake.`)
  }
}

async function visibleBodyText(page: Page) {
  return page.locator('body').evaluate((body) => (body as HTMLElement).innerText)
}

function normalizeForLanguageAudit(value: string) {
  return value.normalize('NFD').replace(/\p{Diacritic}/gu, '').toLowerCase()
}

function seedI18nAdmin() {
  const passwordHash = createPasswordHash(adminPassword)
  const adminUserId = '10000000-0000-0000-0000-00000000i18n'.replace('i18n', '1186')
  const sql = `
insert into app.roles ("Id", name) values
  ('00000000-0000-0000-0000-0000000000a1', 'Admin'),
  ('00000000-0000-0000-0000-0000000000a2', 'DocumentManager'),
  ('00000000-0000-0000-0000-0000000000a3', 'Viewer')
on conflict (name) do nothing;

delete from app.user_ai_budget_limits where user_id in (
  select "Id" from app.users where email = '${adminEmail}'
);
delete from app.user_roles where user_id in (
  select "Id" from app.users where email = '${adminEmail}'
);
delete from app.user_groups where user_id in (
  select "Id" from app.users where email = '${adminEmail}'
);
delete from app.audit_events where actor_user_id in (
  select "Id" from app.users where email = '${adminEmail}'
);
delete from app.users where email = '${adminEmail}';

insert into app.users ("Id", email, display_name, password_hash, is_active) values
  ('${adminUserId}', '${adminEmail}', 'I18N Admin', '${passwordHash}', true);

insert into app.user_roles (user_id, role_id)
select '${adminUserId}'::uuid, "Id" from app.roles where name = 'Admin';

insert into app.user_ai_budget_limits (user_id, monthly_budget_usd, is_disabled, updated_by_user_id) values
  ('${adminUserId}', 5.00, false, '${adminUserId}');
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
  const salt = Buffer.from(`advanced-rag-i18n-${randomUUID()}`)
  const hash = pbkdf2Sync(password, salt, 210_000, 32, 'sha256')
  return `pbkdf2-sha256$210000$${salt.toString('base64')}$${hash.toString('base64')}`
}
