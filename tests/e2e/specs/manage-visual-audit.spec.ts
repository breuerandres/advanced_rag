import { expect, test, type Page } from '@playwright/test'
import { mkdir, rm, writeFile } from 'node:fs/promises'
import path from 'node:path'

const manageBaseUrl = process.env.E2E_MANAGE_URL ?? 'https://manage.localhost'
const artifactDir = path.resolve('artifacts/manage-visual-audit')
const demoAdminEmail = process.env.E2E_VISUAL_ADMIN_EMAIL ?? 'demo.admin@example.com'
const demoAdminPassword = process.env.E2E_VISUAL_ADMIN_PASSWORD ?? 'DemoPassword!42'

interface VisualIssue {
  page: string
  viewport: string
  severity: 'info' | 'warning' | 'error'
  message: string
  selector?: string
}

test.describe('management desktop visual audit', () => {
  test('reviews management desktop screens and tables for visual imperfections', async ({ page }) => {
    await rm(artifactDir, { force: true, recursive: true })
    await mkdir(artifactDir, { recursive: true })
    const issues: VisualIssue[] = []

    await page.setViewportSize({ width: 1440, height: 900 })
    await page.goto(manageBaseUrl)
    await page.waitForLoadState('networkidle')
    await screenshot(page, '00-entry-desktop')

    const authenticated = await ensureAuthenticated(page, issues)
    if (!authenticated) {
      await writeReport(issues)
      return
    }

    await waitForManagementData(page)
    await auditCurrentPage(page, issues, 'manage-users-desktop', 'desktop')
    await auditDialog(page, issues, 'manage-user-dialog-desktop', 'Crear usuario', 'Crear usuario')
    await auditDialog(page, issues, 'manage-group-dialog-desktop', 'Crear grupo', 'Crear grupo')

    await openSection(page, 'Documentos')
    await waitForManagementData(page)
    await auditCurrentPage(page, issues, 'manage-documents-desktop', 'desktop')
    await page.getByRole('button', { name: 'Crear documento' }).click()
    await expect(page.getByRole('heading', { name: 'Crear documento' })).toBeVisible()
    await auditCurrentPage(page, issues, 'manage-document-editor-desktop', 'desktop')

    await openSection(page, 'Auditoria')
    await waitForManagementData(page)
    await auditCurrentPage(page, issues, 'manage-audit-desktop', 'desktop')

    await openSection(page, 'Feedback')
    await waitForManagementData(page)
    await auditCurrentPage(page, issues, 'manage-feedback-desktop', 'desktop')

    await openSection(page, 'Configuracion')
    await waitForManagementData(page)
    await auditCurrentPage(page, issues, 'manage-config-desktop', 'desktop')

    await writeReport(issues)
  })
})

async function ensureAuthenticated(page: Page, issues: VisualIssue[]): Promise<boolean> {
  if (await page.getByRole('heading', { name: 'Usuarios y grupos' }).isVisible()) {
    return true
  }

  if (await page.getByRole('heading', { name: 'Configurá el primer administrador' }).isVisible()) {
    issues.push({
      page: 'entry',
      viewport: 'desktop',
      severity: 'info',
      message: 'Management is in first-run setup state; authenticated tables were not reachable without creating data.',
    })
    await auditCurrentPage(page, issues, 'manage-first-run-desktop', 'desktop')
    return false
  }

  if (await page.getByRole('heading', { name: 'Ingresá a la consola' }).isVisible()) {
    await page.getByLabel('Email').fill(demoAdminEmail)
    await page.getByLabel('Contraseña').fill(demoAdminPassword)
    await page.getByRole('button', { name: 'Ingresar' }).click()
    try {
      await expect(page.getByRole('heading', { name: 'Usuarios y grupos' })).toBeVisible({ timeout: 15_000 })
      return true
    } catch {
      issues.push({
        page: 'login',
        viewport: 'desktop',
        severity: 'error',
        message: `Could not authenticate with ${demoAdminEmail}; authenticated visual audit was blocked.`,
      })
      await screenshot(page, '01-login-blocked-desktop')
      return false
    }
  }

  issues.push({
    page: 'entry',
    viewport: 'desktop',
    severity: 'error',
    message: 'Unknown management entry state; neither setup, login, nor authenticated shell was visible.',
  })
  return false
}

async function openSection(page: Page, name: string) {
  await page.getByRole('link', { name }).click()
  await expect(page.getByRole('heading', { name })).toBeVisible()
}

async function waitForManagementData(page: Page) {
  await page.waitForLoadState('networkidle')
  await expect(page.locator('.status-message').filter({ hasText: /^Cargando/ })).toHaveCount(0, {
    timeout: 15_000,
  })
}

async function auditDialog(
  page: Page,
  issues: VisualIssue[],
  screenshotName: string,
  buttonName: string,
  headingName: string,
) {
  await page.getByRole('button', { name: buttonName }).click()
  await expect(page.getByRole('heading', { name: headingName })).toBeVisible()
  await auditCurrentPage(page, issues, screenshotName, 'desktop')
  await page.getByRole('button', { exact: true, name: 'Cerrar' }).click()
}

async function auditCurrentPage(
  page: Page,
  issues: VisualIssue[],
  name: string,
  viewport: string,
) {
  await screenshot(page, name)
  const measurements = await page.evaluate(() => {
    const pageOverflow = document.documentElement.scrollWidth - document.documentElement.clientWidth
    const appShell = document.querySelector('.app-shell')
    const appShellOverflow = appShell ? appShell.scrollWidth - appShell.clientWidth : 0

    const clippedTables = Array.from(document.querySelectorAll('.table-frame')).flatMap((frame, index) => {
      const table = frame.querySelector('table')
      if (!table) {
        return []
      }

      const frameStyle = window.getComputedStyle(frame)
      const frameBox = frame.getBoundingClientRect()
      const tableBox = table.getBoundingClientRect()
      const clippedRight = tableBox.right - frameBox.right
      const clippedLeft = frameBox.left - tableBox.left
      const hasHorizontalScroll = frameStyle.overflowX === 'auto' || frameStyle.overflowX === 'scroll'
      return !hasHorizontalScroll && (clippedRight > 1 || clippedLeft > 1)
        ? [
            {
              selector: `.table-frame[${index}]`,
              amount: Math.round(Math.max(clippedRight, clippedLeft)),
            },
          ]
        : []
    })

    const horizontallyScrollableTables = Array.from(document.querySelectorAll('.table-frame')).flatMap((frame, index) => {
      const table = frame.querySelector('table')
      if (!table) {
        return []
      }

      const frameBox = frame.getBoundingClientRect()
      const tableBox = table.getBoundingClientRect()
      const visualOverflow = tableBox.width - frameBox.width
      return visualOverflow > 2
        ? [
            {
              selector: `.table-frame[${index}]`,
              amount: Math.round(visualOverflow),
            },
          ]
        : []
    })

    const overflowingElements = Array.from(
      document.querySelectorAll('button, .badge, th, td, label, input, select, .workspace-tab'),
    ).flatMap((element) => {
      const htmlElement = element as HTMLElement
      if (
        htmlElement.matches('.icon-button, .file-input-native') ||
        htmlElement.closest('.row-actions') ||
        (htmlElement.matches('td') && htmlElement.innerText.trim().length === 0)
      ) {
        return []
      }

      const style = window.getComputedStyle(htmlElement)
      const box = htmlElement.getBoundingClientRect()
      if (box.width === 0 || box.height === 0 || style.visibility === 'hidden' || style.display === 'none') {
        return []
      }

      const horizontalOverflow = htmlElement.scrollWidth - htmlElement.clientWidth
      const verticalOverflow = htmlElement.scrollHeight - htmlElement.clientHeight
      if (horizontalOverflow <= 2 && verticalOverflow <= 2) {
        return []
      }

      return [
        {
          selector: readableSelector(htmlElement),
          text: (htmlElement.innerText || htmlElement.getAttribute('aria-label') || '').slice(0, 120),
          horizontalOverflow: Math.round(horizontalOverflow),
          verticalOverflow: Math.round(verticalOverflow),
        },
      ]
    })

    function readableSelector(element: HTMLElement) {
      const label = element.getAttribute('aria-label')
      if (label) {
        return `${element.tagName.toLowerCase()}[aria-label="${label.slice(0, 40)}"]`
      }

      const className = typeof element.className === 'string' ? element.className.split(' ').filter(Boolean)[0] : ''
      return className ? `${element.tagName.toLowerCase()}.${className}` : element.tagName.toLowerCase()
    }

    return {
      appShellOverflow: Math.round(appShellOverflow),
      clippedTables,
      horizontallyScrollableTables,
      overflowingElements,
      pageOverflow: Math.round(pageOverflow),
    }
  })

  if (measurements.pageOverflow > 1 || measurements.appShellOverflow > 1) {
    issues.push({
      page: name,
      viewport,
      severity: 'error',
      message: `Horizontal overflow detected: page ${measurements.pageOverflow}px, shell ${measurements.appShellOverflow}px.`,
    })
  }

  for (const table of measurements.clippedTables) {
    issues.push({
      page: name,
      viewport,
      severity: 'error',
      selector: table.selector,
      message: `Table content is clipped by ${table.amount}px inside its frame.`,
    })
  }

  if (viewport === 'desktop') {
    for (const table of measurements.horizontallyScrollableTables) {
      issues.push({
        page: name,
        viewport,
        severity: 'warning',
        selector: table.selector,
        message: `Table requires ${table.amount}px of horizontal scroll on desktop.`,
      })
    }
  }

  for (const element of measurements.overflowingElements.slice(0, 20)) {
    issues.push({
      page: name,
      viewport,
      severity: 'warning',
      selector: element.selector,
      message: `Element content overflows by ${element.horizontalOverflow}px horizontal and ${element.verticalOverflow}px vertical: ${element.text}`,
    })
  }
}

async function screenshot(page: Page, name: string) {
  await page.screenshot({ fullPage: true, path: path.join(artifactDir, `${name}.png`) })
}

async function writeReport(issues: VisualIssue[]) {
  await writeFile(path.join(artifactDir, 'report.json'), `${JSON.stringify(issues, null, 2)}\n`)
  await writeFile(
    path.join(artifactDir, 'summary.md'),
    [
      '# Management Visual Audit',
      '',
      `Generated at: ${new Date().toISOString()}`,
      '',
      `Issues found: ${issues.length}`,
      '',
      ...issues.map((issue) => `- **${issue.severity}** ${issue.page} (${issue.viewport}): ${issue.message}`),
      '',
    ].join('\n'),
  )
}
