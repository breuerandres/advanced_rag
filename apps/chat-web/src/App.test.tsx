import { afterEach, expect, test, vi } from 'vitest'
import { cleanup, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import App from './App'

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

test('shows the empty initial chat state', () => {
  render(<App />)

  expect(screen.getByRole('heading', { name: 'Chat de instrucciones' })).toBeInTheDocument()
  expect(screen.getByText('Token de chat temporal')).toBeInTheDocument()
  expect(screen.getByText('Hacé una pregunta sobre las instrucciones publicadas.')).toBeInTheDocument()
  expect(screen.getByRole('textbox', { name: 'Pregunta' })).toBeEnabled()
  expect(screen.getByText('0 / 4000')).toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Enviar pregunta' })).toBeDisabled()
})

test('disables the question input while submitting', async () => {
  const pendingChat = deferred<Response>()
  stubFetch([pendingChat.promise])
  const user = userEvent.setup()

  render(<App />)

  await user.type(screen.getByRole('textbox', { name: 'Pregunta' }), 'Como ingreso?')
  await user.click(screen.getByRole('button', { name: 'Enviar pregunta' }))

  expect(screen.getByRole('textbox', { name: 'Pregunta' })).toBeDisabled()
  expect(screen.getByRole('button', { name: /Enviando/ })).toBeDisabled()

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
  const fetchMock = stubFetch([
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
              instruction_version_id: '66666666-6666-6666-6666-666666666666',
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

  render(<App />)

  await user.type(screen.getByRole('textbox', { name: 'Pregunta' }), 'Que regla aplica?')
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
  stubFetch([
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
              instruction_version_id: '66666666-6666-6666-6666-666666666666',
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

  render(<App />)

  await user.type(screen.getByRole('textbox', { name: 'Pregunta' }), 'Que regla aplica?')
  await user.click(screen.getByRole('button', { name: 'Enviar pregunta' }))

  expect(await screen.findByText('Consultá el procedimiento de seguridad.')).toBeInTheDocument()
  expect(screen.getByText('Respuesta desde caché semántico')).toBeInTheDocument()
  expect(screen.getByText('Costo estimado: USD 0.000001')).toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Abrir cita Seguridad' })).toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Me sirvió' })).toBeInTheDocument()
})

test('shows the monthly budget exhausted state', async () => {
  stubFetch([
    jsonResponse(
      429,
      apiError('AI_BUDGET_EXCEEDED', 'Monthly budget exceeded.', 'request-budget-1'),
    ),
  ])
  const user = userEvent.setup()

  render(<App />)

  await user.type(screen.getByRole('textbox', { name: 'Pregunta' }), 'Puedo consultar?')
  await user.click(screen.getByRole('button', { name: 'Enviar pregunta' }))

  expect(await screen.findByRole('alert')).toHaveTextContent(
    'Alcanzaste el presupuesto mensual de uso de IA.',
  )
  expect(screen.getByText('Podés seguir abriendo documentos autorizados.')).toBeInTheDocument()
})

test('renews the chat token when the session token expires and retries the question', async () => {
  const fetchMock = stubFetch([
    jsonResponse(
      401,
      apiError('AUTH_TOKEN_EXPIRED', 'Chat token expired.', 'request-auth-1'),
    ),
    jsonResponse(200, { status: 'ok' }),
    sseResponse([
      ['answer-token', { delta: 'La sesión de chat fue renovada.' }],
      ['citations', { query_audit_event_id: '33333333-3333-3333-3333-333333333333', citations: [] }],
      ['done', {}],
    ]),
  ])
  const user = userEvent.setup()

  render(<App />)

  await user.type(screen.getByRole('textbox', { name: 'Pregunta' }), 'Sigo autenticado?')
  await user.click(screen.getByRole('button', { name: 'Enviar pregunta' }))

  expect(await screen.findByText('La sesión de chat fue renovada.')).toBeInTheDocument()
  expect(fetchMock).toHaveBeenNthCalledWith(
    2,
    '/api/auth/chat-token',
    expect.objectContaining({ method: 'POST' }),
  )
})

test('shows a safe generic error with request id', async () => {
  stubFetch([
    jsonResponse(503, apiError('RAG_PROVIDER_UNAVAILABLE', 'Provider unavailable.', 'request-503')),
  ])
  const user = userEvent.setup()

  render(<App />)

  await user.type(screen.getByRole('textbox', { name: 'Pregunta' }), 'Que hago?')
  await user.click(screen.getByRole('button', { name: 'Enviar pregunta' }))

  expect(await screen.findByRole('alert')).toHaveTextContent('No se pudo responder la pregunta.')
  expect(screen.getByText('ID de solicitud: request-503')).toBeInTheDocument()
})

test('opens citations through viewer exchange links', async () => {
  const assign = vi.fn()
  Object.defineProperty(window, 'location', {
    configurable: true,
    value: { assign },
  })
  const fetchMock = stubFetch([
    sseResponse([
      ['answer-token', { delta: 'Usa credencial visible.' }],
      [
        'citations',
        {
          query_audit_event_id: '33333333-3333-3333-3333-333333333333',
          citations: [
            {
              document_id: '55555555-5555-5555-5555-555555555555',
              instruction_version_id: '66666666-6666-6666-6666-666666666666',
              heading_path: ['Seguridad'],
            },
          ],
        },
      ],
      ['done', {}],
    ]),
    csrfResponse(),
    jsonResponse(200, { url: 'https://docs.client.com/open?code=abc' }),
  ])
  const user = userEvent.setup()

  render(<App />)

  await user.type(screen.getByRole('textbox', { name: 'Pregunta' }), 'Que regla aplica?')
  await user.click(screen.getByRole('button', { name: 'Enviar pregunta' }))
  await user.click(await screen.findByRole('button', { name: 'Abrir cita Seguridad' }))

  expect(fetchMock).toHaveBeenLastCalledWith(
    '/api/viewer/links',
    expect.objectContaining({ method: 'POST' }),
  )
  expect(assign).toHaveBeenCalledWith('https://docs.client.com/open?code=abc')
})

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
