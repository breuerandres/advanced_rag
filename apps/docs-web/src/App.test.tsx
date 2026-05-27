import { expect, test, vi, beforeEach, afterEach } from 'vitest'
import { cleanup, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import App from './App'
import i18n from './i18n'

const originalLocation = window.location

beforeEach(() => {
  vi.restoreAllMocks()
  setLocation('https://docs.localhost/open?documentId=55555555-5555-5555-5555-555555555555')
})

afterEach(() => {
  cleanup()
  void i18n.changeLanguage('es-AR')
  Object.defineProperty(window, 'location', {
    configurable: true,
    value: originalLocation,
  })
})

test('shows loading exchange state', () => {
  vi.spyOn(globalThis, 'fetch').mockReturnValue(new Promise<Response>(() => undefined))

  render(<App />)

  expect(screen.getByText('Validando enlace...')).toBeInTheDocument()
})

test('renders the independent document portal grouped by category', async () => {
  setLocation('https://docs.localhost/')
  mockFetch([
    jsonResponse({
      user: {
        id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
        email: 'viewer@example.com',
        displayName: 'Viewer User',
        roles: ['Viewer'],
        groups: [{ id: '33333333-3333-3333-3333-333333333333', name: 'Legales' }],
      },
    }),
    jsonResponse({
      groups: [{ id: '33333333-3333-3333-3333-333333333333', name: 'Legales' }],
      documents: [
        {
          id: '55555555-5555-5555-5555-555555555555',
          title: 'Manual legal',
          state: 'Published',
          documentType: 'Manual',
          audience: 'Legal',
          allowedGroups: [{ id: '33333333-3333-3333-3333-333333333333', name: 'Legales' }],
          updatedAt: '2026-05-22T12:00:00Z',
        },
      ],
    }),
  ])

  render(<App />)

  expect(await screen.findByRole('heading', { name: 'Biblioteca de documentos' })).toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Legales' })).toBeInTheDocument()
  expect(screen.getByRole('heading', { name: 'Manual legal' })).toBeInTheDocument()
  expect(screen.queryByRole('navigation', { name: /Navegacion del visor/i })).not.toBeInTheDocument()
})

test('changes the document portal language and hides Portuguese', async () => {
  setLocation('https://docs.localhost/')
  mockFetch([
    jsonResponse({
      user: {
        id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
        email: 'viewer@example.com',
        displayName: 'Viewer User',
        roles: ['Viewer'],
        groups: [],
      },
    }),
    jsonResponse({
      groups: [],
      documents: [],
    }),
  ])
  const user = userEvent.setup()

  render(<App />)

  await user.selectOptions(await screen.findByLabelText('Idioma'), 'en-US')

  expect(screen.queryByRole('option', { name: 'PT' })).not.toBeInTheDocument()
  expect(screen.getByRole('heading', { name: 'Document library' })).toBeInTheDocument()
})

test('shows a semantic empty state when the document portal has no visible documents', async () => {
  setLocation('https://docs.localhost/')
  mockFetch([
    jsonResponse({
      user: {
        id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
        email: 'viewer@example.com',
        displayName: 'Viewer User',
        roles: ['Viewer'],
        groups: [],
      },
    }),
    jsonResponse({
      groups: [],
      documents: [],
    }),
  ])

  render(<App />)

  expect(await screen.findByRole('heading', { name: 'Sin documentos para mostrar' })).toBeInTheDocument()
  expect(screen.getByText('No encontramos documentos disponibles con los filtros actuales.')).toBeInTheDocument()
})

test('shows login when the docs host has no session', async () => {
  setLocation('https://docs.localhost/')
  mockFetch([
    errorResponse('AUTH_REQUIRED'),
  ])

  render(<App />)

  const heading = await screen.findByRole('heading', { name: 'Iniciar sesion' })
  expect(heading.closest('main')).toHaveClass('auth-shell')
  expect(heading.closest('form')).toHaveClass('auth-card')
})

test.each([
  ['AUTH_FORBIDDEN', 'No tenes permiso para abrir este documento.'],
  ['AUTH_REQUIRED', 'Inicia sesion para abrir este documento.'],
  ['NOT_FOUND', 'No encontramos el documento solicitado.'],
])('shows safe error state for %s', async (code, message) => {
  mockFetch([
    errorResponse(code),
  ])

  render(<App />)

  expect(await screen.findByRole('alert')).toHaveTextContent(message)
})

test('renders document through the unified session and document id', async () => {
  mockFetch([
    jsonResponse({
      documentId: '55555555-5555-5555-5555-555555555555',
      documentVersionId: 'version-1',
      title: 'Procedimiento publicado',
      state: 'Published',
      documentType: 'Politica',
      audience: 'Operaciones',
      contentHtml: '<h2>Contenido publicado</h2><p>Usa el equipo de seguridad.</p>',
      tokenExpiresAt: '2026-05-18T12:15:00Z',
    }),
  ])

  render(<App />)

  expect(await screen.findByRole('heading', { name: 'Procedimiento publicado' })).toBeInTheDocument()
  expect(screen.getByText('Usa el equipo de seguridad.')).toBeInTheDocument()
  expect(screen.getByText('Sesion vigente hasta')).toBeInTheDocument()
  expect(screen.getByText('Publicado')).toBeInTheDocument()
})

function setLocation(url: string) {
  Object.defineProperty(window, 'location', {
    configurable: true,
    value: new URL(url),
  })
}

function mockFetch(responses: Response[]) {
  const fetch = vi.spyOn(globalThis, 'fetch')
  for (const response of responses) {
    fetch.mockResolvedValueOnce(response)
  }
}

function jsonResponse(body: unknown) {
  return new Response(JSON.stringify(body), {
    status: 200,
    headers: { 'Content-Type': 'application/json' },
  })
}

function errorResponse(code: string) {
  return new Response(
    JSON.stringify({
      error: {
        code,
        message: 'Safe error',
        details: null,
        requestId: 'request-1',
      },
    }),
    {
      status: code === 'AUTH_FORBIDDEN' ? 403 : code === 'NOT_FOUND' ? 404 : 410,
      headers: { 'Content-Type': 'application/json' },
    },
  )
}
