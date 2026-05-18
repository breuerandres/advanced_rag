import { expect, test, vi, beforeEach, afterEach } from 'vitest'
import { cleanup, render, screen } from '@testing-library/react'
import App from './App'

const originalLocation = window.location

beforeEach(() => {
  vi.restoreAllMocks()
  setLocation('https://docs.localhost/open?code=valid-code')
})

afterEach(() => {
  cleanup()
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

test.each([
  ['VIEWER_CODE_EXPIRED', 'El enlace expiro. Pedi uno nuevo desde el chat.'],
  ['VIEWER_CODE_USED', 'Este enlace ya fue usado. Pedi uno nuevo desde el chat.'],
  ['AUTH_FORBIDDEN', 'No tenes permiso para abrir este documento.'],
  ['AUTH_TOKEN_EXPIRED', 'La sesion del visor expiro. Volve a abrir el enlace.'],
  ['NOT_FOUND', 'No encontramos el documento solicitado.'],
])('shows safe error state for %s', async (code, message) => {
  mockFetch([
    csrfResponse(),
    errorResponse(code),
  ])

  render(<App />)

  expect(await screen.findByRole('alert')).toHaveTextContent(message)
})

test('renders document after successful exchange and load', async () => {
  const replaceState = vi.spyOn(window.history, 'replaceState')
  mockFetch([
    csrfResponse(),
    jsonResponse({ instructionId: 'doc-1', expiresAt: '2026-05-18T12:15:00Z' }),
    jsonResponse({
      instructionId: 'doc-1',
      instructionVersionId: 'version-1',
      title: 'Procedimiento publicado',
      state: 'Published',
      instructionType: 'Politica',
      audience: 'Operaciones',
      contentHtml: '<h2>Contenido publicado</h2><p>Usa el equipo de seguridad.</p>',
      tokenExpiresAt: '2026-05-18T12:15:00Z',
    }),
  ])

  render(<App />)

  expect(await screen.findByRole('heading', { name: 'Procedimiento publicado' })).toBeInTheDocument()
  expect(screen.getByText('Usa el equipo de seguridad.')).toBeInTheDocument()
  expect(screen.getByText('Publicado')).toBeInTheDocument()
  expect(replaceState).toHaveBeenCalledWith({}, '', '/open')
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

function csrfResponse() {
  return new Response('{}', {
    status: 200,
    headers: { 'X-CSRF-Token': 'csrf-token' },
  })
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
