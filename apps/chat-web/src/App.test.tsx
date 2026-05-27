import { afterEach, expect, test, vi } from 'vitest'
import { cleanup, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import App from './App'
import i18n from './i18n'

afterEach(() => {
  cleanup()
  void i18n.changeLanguage('es-AR')
  vi.unstubAllGlobals()
})

test('shows the three-pane chat workspace with local conversations and citation rail', async () => {
  renderAuthenticatedChat([])

  expect(await screen.findByRole('heading', { name: 'Chat de instrucciones' })).toBeInTheDocument()
  expect(screen.getByRole('navigation', { name: 'Conversaciones' })).toBeInTheDocument()
  expect(screen.getByRole('complementary', { name: 'Contexto de respuesta' })).toBeInTheDocument()
  expect(screen.getByText('Nueva conversación')).toBeInTheDocument()
  expect(screen.getByText('Sin citas todavía')).toBeInTheDocument()
  expect(screen.getByText('Sesion unificada')).toBeInTheDocument()
  expect(screen.getByText('Hacé una pregunta sobre las instrucciones publicadas.')).toBeInTheDocument()
  expect(screen.getByRole('textbox', { name: 'Pregunta' })).toBeEnabled()
  expect(screen.getByText('0 / 4000')).toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Enviar pregunta' })).toBeDisabled()
})

test('disables the question input while submitting', async () => {
  const pendingChat = deferred<Response>()
  renderAuthenticatedChat([csrfResponse(), pendingChat.promise])
  const user = userEvent.setup()

  await user.type(await screen.findByRole('textbox', { name: 'Pregunta' }), 'Como ingreso?')
  await user.click(screen.getByRole('button', { name: 'Enviar pregunta' }))

  expect(screen.getByRole('textbox', { name: 'Pregunta' })).toBeDisabled()
  expect(screen.getByRole('button', { name: /Enviando/ })).toBeDisabled()
  expect(screen.getByLabelText('Respuesta en curso')).toBeInTheDocument()
  expect(screen.getByTestId('streaming-cursor')).toHaveAttribute('aria-hidden', 'true')

  pendingChat.resolve(
    sseResponse([
      ['answer-token', { delta: 'Usa tu usuario corporativo.' }],
      ['citations', { query_audit_event_id: '33333333-3333-3333-3333-333333333333', citations: [] }],
      ['done', {}],
    ]),
  )
  expect(await screen.findByText('Usa tu usuario corporativo.')).toBeInTheDocument()
})

test('submits feedback after a chat answer and allows updating it', async () => {
  const fetchMock = renderAuthenticatedChat([
    csrfResponse(),
    sseResponse([
      ['answer-token', { delta: 'Usa credencial visible.' }],
      [
        'citations',
        {
          citations: [
            {
              query_audit_event_id: '33333333-3333-3333-3333-333333333333',
              chunk_id: '44444444-4444-4444-4444-444444444444',
              document_id: '55555555-5555-5555-5555-555555555555',
              document_version_id: '66666666-6666-6666-6666-666666666666',
              heading_path: ['Seguridad'],
            },
          ],
        },
      ],
      ['done', {}],
    ]),
    jsonResponse(200, {
      queryAuditEventId: '33333333-3333-3333-3333-333333333333',
      value: 'down',
      comment: 'Falto detalle',
    }),
    jsonResponse(200, {
      queryAuditEventId: '33333333-3333-3333-3333-333333333333',
      value: 'up',
      comment: '',
    }),
  ])
  const user = userEvent.setup()

  await user.type(await screen.findByRole('textbox', { name: 'Pregunta' }), 'Que regla aplica?')
  await user.click(screen.getByRole('button', { name: 'Enviar pregunta' }))

  expect(await screen.findByText('Usa credencial visible.')).toBeInTheDocument()
  await user.click(screen.getByRole('button', { name: 'No me sirvió' }))
  await user.type(screen.getByRole('textbox', { name: 'Comentario opcional' }), 'Falto detalle')
  await user.click(screen.getByRole('button', { name: 'Enviar feedback' }))

  expect(await screen.findByText('Feedback registrado.')).toBeInTheDocument()
  await user.click(screen.getByRole('button', { name: 'Me sirvió' }))
  await user.click(screen.getByRole('button', { name: 'Actualizar feedback' }))

  expect(fetchMock).toHaveBeenLastCalledWith(
    '/api/feedback/33333333-3333-3333-3333-333333333333',
    expect.objectContaining({ method: 'POST' }),
  )
})

test('shows a successful answer with citations and cache hit indicator', async () => {
  renderAuthenticatedChat([
    csrfResponse(),
    sseResponse([
      ['cache-hit', { cached_at: '2026-05-18T10:00:00Z' }],
      ['answer-token', { delta: 'Consultá el procedimiento de seguridad.' }],
      [
        'citations',
        {
          query_audit_event_id: '33333333-3333-3333-3333-333333333333',
          citations: [
            {
              document_id: '55555555-5555-5555-5555-555555555555',
              document_version_id: '66666666-6666-6666-6666-666666666666',
              heading_path: ['Seguridad'],
            },
          ],
        },
      ],
      ['usage', { input_tokens: 42, cached_tokens: 10, output_tokens: 18, cost_usd: 0.000001 }],
      ['done', {}],
    ]),
  ])
  const user = userEvent.setup()

  await user.type(await screen.findByRole('textbox', { name: 'Pregunta' }), 'Que regla aplica?')
  await user.click(screen.getByRole('button', { name: 'Enviar pregunta' }))

  expect(await screen.findByText('Consultá el procedimiento de seguridad.')).toBeInTheDocument()
  expect(screen.getByText('Respuesta desde caché semántico')).toBeInTheDocument()
  expect(screen.getByText('Costo estimado: USD 0.000001')).toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Abrir cita Seguridad' })).toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Me sirvió' })).toBeInTheDocument()
})

test('adds the latest question to the local conversation list', async () => {
  renderAuthenticatedChat([
    csrfResponse(),
    sseResponse([
      ['answer-token', { delta: 'ConsultÃ¡ el procedimiento de seguridad.' }],
      ['citations', { query_audit_event_id: '33333333-3333-3333-3333-333333333333', citations: [] }],
      ['done', {}],
    ]),
  ])
  const user = userEvent.setup()

  await user.type(await screen.findByRole('textbox', { name: 'Pregunta' }), 'Que regla aplica?')
  await user.click(screen.getByRole('button', { name: 'Enviar pregunta' }))

  const conversations = screen.getByRole('navigation', { name: 'Conversaciones' })
  expect(await screen.findByText('ConsultÃ¡ el procedimiento de seguridad.')).toBeInTheDocument()
  expect(conversations).toHaveTextContent('Que regla aplica?')
})

test('opens citation drawer from the citation rail', async () => {
  renderAuthenticatedChat([
    csrfResponse(),
    sseResponse([
      ['answer-token', { delta: 'ConsultÃ¡ el procedimiento de seguridad.' }],
      [
        'citations',
        {
          query_audit_event_id: '33333333-3333-3333-3333-333333333333',
          citations: [
            {
              document_id: '55555555-5555-5555-5555-555555555555',
              document_version_id: '66666666-6666-6666-6666-666666666666',
              heading_path: ['Seguridad'],
            },
          ],
        },
      ],
      ['done', {}],
    ]),
  ])
  const user = userEvent.setup()

  await user.type(await screen.findByRole('textbox', { name: 'Pregunta' }), 'Que regla aplica?')
  await user.click(screen.getByRole('button', { name: 'Enviar pregunta' }))
  await user.click(await screen.findByRole('button', { name: 'Ver citas' }))

  expect(screen.getByRole('dialog', { name: 'Citas' })).toHaveTextContent('Seguridad')
})

test('opens the command palette with the chat command', async () => {
  renderAuthenticatedChat([])

  expect(await screen.findByRole('heading', { name: 'Chat de instrucciones' })).toBeInTheDocument()
  window.dispatchEvent(new KeyboardEvent('keydown', { key: 'k', ctrlKey: true, bubbles: true }))

  expect(await screen.findByLabelText('Command palette')).toBeInTheDocument()
  expect(screen.getByText('Nueva pregunta')).toBeInTheDocument()
})

test('shows the monthly budget exhausted state', async () => {
  renderAuthenticatedChat([
    csrfResponse(),
    jsonResponse(
      429,
      apiError('AI_BUDGET_EXCEEDED', 'Monthly budget exceeded.', 'request-budget-1'),
    ),
  ])
  const user = userEvent.setup()

  await user.type(await screen.findByRole('textbox', { name: 'Pregunta' }), 'Puedo consultar?')
  await user.click(screen.getByRole('button', { name: 'Enviar pregunta' }))

  expect(await screen.findByRole('alert')).toHaveTextContent(
    'Alcanzaste el presupuesto mensual de uso de IA.',
  )
  expect(screen.getByText('Podés seguir abriendo documentos autorizados.')).toBeInTheDocument()
})

test('shows the auth-expired state when the unified session is rejected', async () => {
  renderAuthenticatedChat([
    csrfResponse(),
    jsonResponse(401, apiError('AUTH_REQUIRED', 'Session required.', 'request-auth-1')),
  ])
  const user = userEvent.setup()

  await user.type(await screen.findByRole('textbox', { name: 'Pregunta' }), 'Sigo autenticado?')
  await user.click(screen.getByRole('button', { name: 'Enviar pregunta' }))

  expect(await screen.findByRole('alert')).toHaveTextContent(
    'Tu sesión de chat expiró.',
  )
})

test('shows a safe generic error with request id', async () => {
  renderAuthenticatedChat([
    csrfResponse(),
    jsonResponse(503, apiError('RAG_PROVIDER_UNAVAILABLE', 'Provider unavailable.', 'request-503')),
  ])
  const user = userEvent.setup()

  await user.type(await screen.findByRole('textbox', { name: 'Pregunta' }), 'Que hago?')
  await user.click(screen.getByRole('button', { name: 'Enviar pregunta' }))

  expect(await screen.findByRole('alert')).toHaveTextContent('No se pudo responder la pregunta.')
  expect(screen.getByText('ID de solicitud: request-503')).toBeInTheDocument()
})

test('opens citations through session-based viewer links', async () => {
  const assign = vi.fn()
  Object.defineProperty(window, 'location', {
    configurable: true,
    value: { assign },
  })
  const fetchMock = renderAuthenticatedChat([
    csrfResponse(),
    sseResponse([
      ['answer-token', { delta: 'Usa credencial visible.' }],
      [
        'citations',
        {
          query_audit_event_id: '33333333-3333-3333-3333-333333333333',
          citations: [
            {
              document_id: '55555555-5555-5555-5555-555555555555',
              document_version_id: '66666666-6666-6666-6666-666666666666',
              heading_path: ['Seguridad'],
            },
          ],
        },
      ],
      ['done', {}],
    ]),
    jsonResponse(200, { url: 'https://docs.client.com/open?documentId=55555555-5555-5555-5555-555555555555' }),
  ])
  const user = userEvent.setup()

  await user.type(await screen.findByRole('textbox', { name: 'Pregunta' }), 'Que regla aplica?')
  await user.click(screen.getByRole('button', { name: 'Enviar pregunta' }))
  await user.click(await screen.findByRole('button', { name: 'Abrir cita Seguridad' }))

  expect(fetchMock).toHaveBeenLastCalledWith(
    '/api/viewer/links',
    expect.objectContaining({ method: 'POST' }),
  )
  expect(assign).toHaveBeenCalledWith('https://docs.client.com/open?documentId=55555555-5555-5555-5555-555555555555')
})

test('shows login when the chat host has no session and opens the chat after signing in', async () => {
  const fetchMock = stubFetch([
    jsonResponse(401, apiError('AUTH_REQUIRED', 'Authentication required.', 'session-request')),
    csrfResponse(),
    jsonResponse(200, {
      user: {
        id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
        email: 'viewer@example.com',
        displayName: 'Viewer User',
        roles: ['Viewer'],
        groups: [],
      },
    }),
  ])
  const user = userEvent.setup()

  render(<App />)

  const heading = await screen.findByRole('heading', { name: 'Iniciar sesion' })
  expect(heading.closest('main')).toHaveClass('auth-shell')
  expect(heading.closest('form')).toHaveClass('auth-card')
  await user.type(screen.getByRole('textbox', { name: 'Email' }), 'viewer@example.com')
  await user.type(screen.getByLabelText('Contrasena'), 'password')
  await user.click(screen.getByRole('button', { name: 'Entrar al chat' }))

  expect(await screen.findByRole('heading', { name: 'Chat de instrucciones' })).toBeInTheDocument()
  expect(fetchMock).toHaveBeenCalledWith(
    '/api/auth/login',
    expect.objectContaining({ method: 'POST' }),
  )
})

test('changes language from the visible language selector', async () => {
  renderAuthenticatedChat([])
  const user = userEvent.setup()

  await user.selectOptions(await screen.findByLabelText('Idioma'), 'en-US')

  expect(screen.queryByRole('option', { name: 'PT' })).not.toBeInTheDocument()
  expect(screen.getByRole('heading', { name: 'Instruction chat' })).toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Send question' })).toBeDisabled()
})

function renderAuthenticatedChat(responses: Array<Response | Promise<Response>>) {
  const fetchMock = stubFetch([
    jsonResponse(200, {
      user: {
        id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
        email: 'viewer@example.com',
        displayName: 'Viewer User',
        roles: ['Viewer'],
        groups: [],
      },
    }),
    ...responses,
  ])
  render(<App />)
  return fetchMock
}

function stubFetch(responses: Array<Response | Promise<Response>>) {
  const fetchMock = vi.fn(async () => {
    const response = responses.shift()
    if (!response) {
      throw new Error('Unexpected fetch call.')
    }

    return await response
  })

  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

function jsonResponse(status: number, body: unknown) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

function csrfResponse() {
  return new Response('{}', {
    status: 200,
    headers: { 'X-CSRF-Token': 'csrf-token' },
  })
}

function sseResponse(events: [string, unknown][]) {
  const body = events
    .map(([event, data]) => `event: ${event}\ndata: ${JSON.stringify(data)}\n\n`)
    .join('')
  return new Response(body, {
    status: 200,
    headers: { 'Content-Type': 'text/event-stream' },
  })
}

function apiError(code: string, message: string, requestId: string) {
  return {
    error: {
      code,
      message,
      requestId,
      details: null,
    },
  }
}

function deferred<T>() {
  let resolve!: (value: T) => void
  let reject!: (reason?: unknown) => void
  const promise = new Promise<T>((promiseResolve, promiseReject) => {
    resolve = promiseResolve
    reject = promiseReject
  })

  return { promise, resolve, reject }
}
