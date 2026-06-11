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

  expect(screen.getByText('Validando enlace…')).toBeInTheDocument()
})

test('renders the portal with document-type chips and cards', async () => {
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
        {
          id: '66666666-6666-6666-6666-666666666666',
          title: 'Política de viajes',
          state: 'Published',
          documentType: 'Política',
          audience: 'Todos',
          allowedGroups: [],
          updatedAt: '2026-06-01T12:00:00Z',
        },
      ],
    }),
  ])
  const user = userEvent.setup()

  render(<App />)

  expect(await screen.findByRole('heading', { name: 'Biblioteca de documentos' })).toBeInTheDocument()
  // Filter chips come from document types, not access groups.
  expect(screen.getByRole('button', { name: 'Manual' })).toBeInTheDocument()
  expect(screen.queryByRole('button', { name: 'Legales' })).not.toBeInTheDocument()
  expect(screen.getByRole('heading', { name: 'Manual legal' })).toBeInTheDocument()

  await user.click(screen.getByRole('button', { name: 'Política' }))

  expect(screen.queryByRole('heading', { name: 'Manual legal' })).not.toBeInTheDocument()
  expect(screen.getByRole('heading', { name: 'Política de viajes' })).toBeInTheDocument()

  // "Todos" resets the type filter (the clear-filters button only renders on the
  // filtered-empty state, which this flow never reaches).
  await user.click(screen.getByRole('button', { name: 'Todos' }))
  expect(screen.getByRole('heading', { name: 'Manual legal' })).toBeInTheDocument()
})

test('consumes manage session handoff before loading the document portal', async () => {
  setLocation('https://docs.localhost/?handoff=docs-code')
  const replaceState = vi.spyOn(window.history, 'replaceState')
  const fetch = mockFetch([
    jsonResponse({ status: 'ok' }, { 'X-CSRF-Token': 'csrf-token' }),
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
  expect(fetch).toHaveBeenNthCalledWith(
    1,
    '/api/csrf',
    expect.objectContaining({ credentials: 'include' }),
  )
  expect(fetch).toHaveBeenNthCalledWith(
    2,
    '/api/auth/session-handoffs/consume',
    expect.objectContaining({ method: 'POST' }),
  )
  expect(JSON.parse((fetch.mock.calls[1][1] as RequestInit).body as string)).toEqual({
    handoffCode: 'docs-code',
    target: 'docs',
  })
  expect(String(replaceState.mock.calls[0][2])).toBe('/')
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
  expect(screen.getByText('Todavía no hay documentos disponibles para tu usuario.')).toBeInTheDocument()
})

test('shows the filtered empty state with a clear-filters action', async () => {
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
      documents: [
        {
          id: '55555555-5555-5555-5555-555555555555',
          title: 'Manual legal',
          state: 'Published',
          documentType: 'Manual',
          audience: 'Legal',
          allowedGroups: [],
          updatedAt: '2026-05-22T12:00:00Z',
        },
      ],
    }),
  ])
  const user = userEvent.setup()

  render(<App />)

  await user.type(await screen.findByPlaceholderText('Buscar por título, tipo o audiencia…'), 'inexistente')

  expect(await screen.findByText('No encontramos documentos con los filtros actuales.')).toBeInTheDocument()

  await user.click(screen.getByRole('button', { name: 'Limpiar filtros' }))

  expect(screen.getByRole('heading', { name: 'Manual legal' })).toBeInTheDocument()
})

test('shows login when the docs host has no session', async () => {
  setLocation('https://docs.localhost/')
  mockFetch([
    errorResponse('AUTH_REQUIRED'),
  ])

  render(<App />)

  const heading = await screen.findByRole('heading', { name: 'Iniciar sesión' })
  expect(heading.closest('main')).toHaveClass('auth-shell')
  expect(heading.closest('form')).toHaveClass('auth-card')
})

test.each([
  ['AUTH_FORBIDDEN', 'No tenés permiso para abrir este documento.'],
  ['AUTH_REQUIRED', 'Iniciá sesión para continuar.'],
  ['NOT_FOUND', 'No encontramos el documento solicitado.'],
])('shows safe error state for %s', async (code, message) => {
  mockFetch([
    errorResponse(code),
  ])

  render(<App />)

  expect(await screen.findByRole('alert')).toHaveTextContent(message)
})

test('renders the document with back navigation and session footer', async () => {
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
  const backLink = screen.getByRole('link', { name: 'Volver a la biblioteca' })
  expect(backLink).toHaveAttribute('href', '/')
  expect(screen.getByText(/Sesión válida hasta/)).toBeInTheDocument()
  // Published documents show no state chip or banner.
  expect(screen.queryByText('Publicado')).not.toBeInTheDocument()
})

test('shows a draft banner when viewing an unpublished version', async () => {
  mockFetch([
    jsonResponse({
      documentId: '55555555-5555-5555-5555-555555555555',
      documentVersionId: 'version-1',
      title: 'Borrador interno',
      state: 'Draft',
      documentType: 'Manual',
      audience: 'Operaciones',
      contentHtml: '<p>Contenido en preparación.</p>',
      tokenExpiresAt: '2026-05-18T12:15:00Z',
    }),
  ])

  render(<App />)

  expect(await screen.findByRole('heading', { name: 'Borrador interno' })).toBeInTheDocument()
  expect(
    screen.getByText('Estás viendo una versión en borrador. No es visible para usuarios finales.'),
  ).toBeInTheDocument()
})

test('consumes handoff code, removes it from the URL, then loads the document', async () => {
  setLocation(
    'https://docs.localhost/open?documentId=55555555-5555-5555-5555-555555555555&handoff=one-time-code',
  )
  const replaceState = vi.spyOn(window.history, 'replaceState')
  const fetch = mockFetch([
    jsonResponse({ status: 'ok' }, { 'X-CSRF-Token': 'csrf-token' }),
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
      documentId: '55555555-5555-5555-5555-555555555555',
      documentVersionId: 'version-1',
      title: 'Procedimiento publicado',
      state: 'Published',
      documentType: 'Politica',
      audience: 'Operaciones',
      contentHtml: '<p>Contenido publicado.</p>',
      tokenExpiresAt: '2026-05-18T12:15:00Z',
    }),
  ])

  render(<App />)

  expect(await screen.findByRole('heading', { name: 'Procedimiento publicado' })).toBeInTheDocument()
  expect(fetch).toHaveBeenNthCalledWith(
    1,
    '/api/csrf',
    expect.objectContaining({ credentials: 'include' }),
  )
  expect(fetch).toHaveBeenNthCalledWith(
    2,
    '/api/viewer/session-handoff',
    expect.objectContaining({ method: 'POST' }),
  )
  expect(JSON.parse((fetch.mock.calls[1][1] as RequestInit).body as string)).toEqual({
    documentId: '55555555-5555-5555-5555-555555555555',
    handoffCode: 'one-time-code',
  })
  expect(String(replaceState.mock.calls[0][2])).toBe(
    '/open?documentId=55555555-5555-5555-5555-555555555555',
  )
})

test('renders list content inside the document content surface', async () => {
  mockFetch([
    jsonResponse({
      documentId: '55555555-5555-5555-5555-555555555555',
      documentVersionId: 'version-1',
      title: 'Procedimiento publicado',
      state: 'Published',
      documentType: 'Politica',
      audience: 'Operaciones',
      contentHtml: '<ul><li>Paso uno</li></ul><ol><li>Paso dos</li></ol>',
      tokenExpiresAt: '2026-05-18T12:15:00Z',
    }),
  ])

  render(<App />)

  expect(await screen.findByText('Paso uno')).toBeInTheDocument()
  expect(screen.getByText('Paso dos')).toBeInTheDocument()
  expect(document.querySelector('.document-content ul')).not.toBeNull()
  expect(document.querySelector('.document-content ol')).not.toBeNull()
})

test('shows the doc chat bubble only for published documents', async () => {
  mockFetch([jsonResponse({ ...PUBLISHED_DOCUMENT, state: 'Draft' })])

  render(<App />)

  expect(await screen.findByRole('heading', { name: 'Procedimiento publicado' })).toBeInTheDocument()
  expect(screen.queryByRole('button', { name: 'Preguntale a este documento' })).not.toBeInTheDocument()
})

test('asks the document mini chat and renders the streamed answer with feedback', async () => {
  const fetch = mockFetch([
    jsonResponse(PUBLISHED_DOCUMENT),
    jsonResponse({ status: 'ok' }, { 'X-CSRF-Token': 'csrf-token' }),
    docChatSse('La respuesta sale de este documento.'),
    jsonResponse({ status: 'ok' }, { 'X-CSRF-Token': 'csrf-token' }),
    jsonResponse({
      queryAuditEventId: '11111111-1111-1111-1111-111111111111',
      value: 'up',
      comment: 'Muy claro',
    }),
  ])
  const user = userEvent.setup()

  render(<App />)

  await user.click(await screen.findByRole('button', { name: 'Preguntale a este documento' }))
  await user.click(screen.getByRole('button', { name: '¿De qué trata este documento?' }))

  expect(await screen.findByText('La respuesta sale de este documento.')).toBeInTheDocument()
  const chatCall = fetch.mock.calls.find(([url]) => url === '/api/chat')
  expect(chatCall).toBeDefined()
  expect(JSON.parse((chatCall![1] as RequestInit).body as string)).toMatchObject({
    question: '¿De qué trata este documento?',
    documentId: '55555555-5555-5555-5555-555555555555',
  })

  await user.click(screen.getByRole('button', { name: 'Respuesta útil' }))
  await user.type(
    screen.getByPlaceholderText('Contanos qué mejorarías (opcional)'),
    'Muy claro',
  )
  await user.click(screen.getByRole('button', { name: 'Enviar feedback' }))

  expect(await screen.findByText('Gracias por tu feedback.')).toBeInTheDocument()
  const feedbackCall = fetch.mock.calls.find(([url]) =>
    String(url).startsWith('/api/feedback/11111111-1111-1111-1111-111111111111'),
  )
  expect(feedbackCall).toBeDefined()
  expect(JSON.parse((feedbackCall![1] as RequestInit).body as string)).toEqual({
    value: 'up',
    comment: 'Muy claro',
  })
})

test('shows the budget-limited message and retry when the doc chat is over budget', async () => {
  mockFetch([
    jsonResponse(PUBLISHED_DOCUMENT),
    jsonResponse({ status: 'ok' }, { 'X-CSRF-Token': 'csrf-token' }),
    errorResponse('AI_BUDGET_EXCEEDED', 429),
  ])
  const user = userEvent.setup()

  render(<App />)

  await user.click(await screen.findByRole('button', { name: 'Preguntale a este documento' }))
  await user.click(screen.getByRole('button', { name: 'Resumime los puntos principales.' }))

  expect(
    await screen.findByText(
      'Alcanzaste tu límite mensual de uso de IA. Podés seguir leyendo el documento sin problema.',
    ),
  ).toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Reintentar' })).toBeInTheDocument()
  // The failed question was rolled back from the transcript.
  expect(screen.queryByText('Resumime los puntos principales.', { selector: '.bubble' })).not.toBeInTheDocument()
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
  return fetch
}

function jsonResponse(body: unknown, headers: Record<string, string> = {}) {
  return new Response(JSON.stringify(body), {
    status: 200,
    headers: { 'Content-Type': 'application/json', ...headers },
  })
}

function errorResponse(code: string, status?: number) {
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
      status: status ?? (code === 'AUTH_FORBIDDEN' ? 403 : code === 'NOT_FOUND' ? 404 : 410),
      headers: { 'Content-Type': 'application/json' },
    },
  )
}

function sseResponse(events: string) {
  return new Response(events, {
    status: 200,
    headers: { 'Content-Type': 'text/event-stream' },
  })
}

function docChatSse(answer: string, auditId = '11111111-1111-1111-1111-111111111111') {
  return sseResponse(
    `event: request-id\ndata: {"request_id":"req-1"}\n\n` +
      `event: answer-token\ndata: ${JSON.stringify({ delta: answer })}\n\n` +
      `event: citations\ndata: ${JSON.stringify({ query_audit_event_id: auditId, citations: [] })}\n\n` +
      `event: usage\ndata: {"input_tokens":1,"cached_tokens":0,"output_tokens":1,"cost_usd":0.0001}\n\n` +
      `event: done\ndata: {}\n\n`,
  )
}

const PUBLISHED_DOCUMENT = {
  documentId: '55555555-5555-5555-5555-555555555555',
  documentVersionId: 'version-1',
  title: 'Procedimiento publicado',
  state: 'Published',
  documentType: 'Politica',
  audience: 'Operaciones',
  contentHtml: '<p>Usa el equipo de seguridad.</p>',
  tokenExpiresAt: '2026-05-18T12:15:00Z',
}
