import { afterEach, expect, test, vi } from 'vitest'
import { cleanup, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import App from './App'
import i18n from './i18n'

afterEach(() => {
  cleanup()
  void i18n.changeLanguage('es-AR')
  window.history.pushState(null, '', '/')
  vi.unstubAllGlobals()
})

test('shows the simplified chat workspace with a fixed left conversation drawer', async () => {
  renderAuthenticatedChat([])

  expect(await screen.findByRole('heading', { name: 'Chat de instrucciones' })).toBeInTheDocument()
  const drawer = screen.getByRole('complementary', { name: 'Menu de chat' })
  expect(drawer).toHaveTextContent('Advanced RAG')
  expect(drawer).toHaveTextContent('viewer@example.com')
  expect(drawer).toHaveTextContent('Viewer')
  expect(screen.getByRole('navigation', { name: 'Historial de conversaciones' })).toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Nueva conversación' })).toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Cerrar sesión' })).toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Cambiar tema' })).toBeInTheDocument()
  expect(screen.getByText('Hacé una pregunta sobre las instrucciones publicadas.')).toBeInTheDocument()
  expect(screen.getByRole('textbox', { name: 'Pregunta' })).toBeEnabled()
  expect(screen.getByText('0 / 4000')).toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Enviar pregunta' })).toBeDisabled()
})

test('consumes manage session handoff before loading the chat workspace', async () => {
  window.history.pushState(null, '', '/?handoff=chat-code')
  const replaceState = vi.spyOn(window.history, 'replaceState')
  const fetchMock = stubFetch([
    csrfResponse(),
    sessionResponse(),
    jsonResponse(200, { sessions: [] }),
  ])

  render(<App />)

  expect(await screen.findByRole('heading', { name: 'Chat de instrucciones' })).toBeInTheDocument()
  expect(fetchMock).toHaveBeenNthCalledWith(
    1,
    '/api/csrf',
    expect.objectContaining({ credentials: 'include' }),
  )
  expect(fetchMock).toHaveBeenNthCalledWith(
    2,
    '/api/auth/session-handoffs/consume',
    expect.objectContaining({ method: 'POST' }),
  )
  expect(JSON.parse((fetchMock.mock.calls[1][1] as RequestInit).body as string)).toEqual({
    handoffCode: 'chat-code',
    target: 'chat',
  })
  expect(String(replaceState.mock.calls[0][2])).toBe('/')
})

test('logs out from the fixed chat drawer and returns to login', async () => {
  const fetchMock = renderAuthenticatedChat([
    csrfResponse(),
    jsonResponse(200, { message: 'Logged out.' }),
  ])
  const user = userEvent.setup()

  await user.click(await screen.findByRole('button', { name: 'Cerrar sesión' }))

  expect(await screen.findByRole('heading', { name: 'Iniciar sesión' })).toBeInTheDocument()
  expect(fetchMock).toHaveBeenLastCalledWith(
    '/api/auth/logout',
    expect.objectContaining({ method: 'POST' }),
  )
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
    jsonResponse(200, { sessions: [] }),
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

test('allows clearing a selected feedback button before submitting', async () => {
  renderAuthenticatedChat([
    csrfResponse(),
    sseResponse([
      ['answer-token', { delta: 'Usa credencial visible.' }],
      ['citations', { query_audit_event_id: '33333333-3333-3333-3333-333333333333', citations: [] }],
      ['done', {}],
    ]),
    jsonResponse(200, { sessions: [] }),
  ])
  const user = userEvent.setup()

  await user.type(await screen.findByRole('textbox', { name: 'Pregunta' }), 'Que regla aplica?')
  await user.click(screen.getByRole('button', { name: 'Enviar pregunta' }))
  await user.click(await screen.findByRole('button', { name: 'Me sirvió' }))
  expect(screen.getByRole('textbox', { name: 'Comentario opcional' })).toBeInTheDocument()

  await user.click(screen.getByRole('button', { name: 'Me sirvió' }))

  expect(screen.queryByRole('textbox', { name: 'Comentario opcional' })).not.toBeInTheDocument()
  expect(screen.queryByRole('button', { name: 'Enviar feedback' })).not.toBeInTheDocument()
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
    jsonResponse(200, { sessions: [] }),
  ])
  const user = userEvent.setup()

  await user.type(await screen.findByRole('textbox', { name: 'Pregunta' }), 'Que regla aplica?')
  await user.click(screen.getByRole('button', { name: 'Enviar pregunta' }))

  expect(await screen.findByText('Consultá el procedimiento de seguridad.')).toBeInTheDocument()
  expect(screen.getByText('Respuesta desde caché semántico')).toBeInTheDocument()
  expect(screen.getByText('Costo estimado: USD 0.000001')).toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Ver citas (1)' })).toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Me sirvió' })).toBeInTheDocument()
})

test('deduplicates citations and keeps them in the right drawer', async () => {
  renderAuthenticatedChat([
    csrfResponse(),
    sseResponse([
      ['answer-token', { delta: 'Consultá el procedimiento de seguridad.' }],
      [
        'citations',
        {
          query_audit_event_id: '33333333-3333-3333-3333-333333333333',
          citations: [
            {
              chunk_id: '44444444-4444-4444-4444-444444444444',
              document_id: '55555555-5555-5555-5555-555555555555',
              document_version_id: '66666666-6666-6666-6666-666666666666',
              heading_path: ['Seguridad'],
            },
            {
              chunk_id: '77777777-7777-7777-7777-777777777777',
              document_id: '55555555-5555-5555-5555-555555555555',
              document_version_id: '66666666-6666-6666-6666-666666666666',
              heading_path: ['Seguridad', 'Ingreso'],
            },
          ],
        },
      ],
      ['done', {}],
    ]),
    jsonResponse(200, { sessions: [] }),
  ])
  const user = userEvent.setup()

  await user.type(await screen.findByRole('textbox', { name: 'Pregunta' }), 'Que regla aplica?')
  await user.click(screen.getByRole('button', { name: 'Enviar pregunta' }))

  expect(await screen.findByText('Consultá el procedimiento de seguridad.')).toBeInTheDocument()
  expect(screen.queryByRole('button', { name: 'Abrir cita Seguridad' })).not.toBeInTheDocument()
  await user.click(screen.getByRole('button', { name: 'Ver citas (1)' }))

  const drawer = screen.getByRole('dialog', { name: 'Citas' })
  expect(drawer).toHaveTextContent('Seguridad')
  expect(screen.getAllByText('Seguridad')).toHaveLength(1)
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

  const conversations = screen.getByRole('navigation', { name: 'Historial de conversaciones' })
  expect(await screen.findByText('ConsultÃ¡ el procedimiento de seguridad.')).toBeInTheDocument()
  expect(conversations).toHaveTextContent('Que regla aplica?')
})

test('loads persisted conversations and renders the selected transcript after reload', async () => {
  const sessionId = '77777777-7777-7777-7777-777777777777'
  stubFetch([
    sessionResponse(),
    jsonResponse(200, {
      sessions: [
        {
          sessionId,
          title: 'Credenciales de contratistas',
          lastQuestion: 'Y contratistas?',
          lastAnswer: 'Los contratistas usan credencial visitante.',
          lastActivityAt: '2026-06-04T12:00:00Z',
          turnCount: 2,
        },
      ],
    }),
    jsonResponse(200, {
      sessionId,
      turns: [
        {
          queryAuditEventId: '11111111-1111-1111-1111-111111111111',
          question: 'Como funcionan las credenciales?',
          answer: 'Usa credencial visible.',
          createdAt: '2026-06-04T11:59:00Z',
          cacheHit: false,
          feedbackValue: null,
          feedbackComment: null,
          citations: [
            {
              chunkId: '44444444-4444-4444-4444-444444444444',
              documentId: '55555555-5555-5555-5555-555555555555',
              documentVersionId: '66666666-6666-6666-6666-666666666666',
              headingPath: ['Seguridad'],
            },
          ],
        },
        {
          queryAuditEventId: '22222222-2222-2222-2222-222222222222',
          question: 'Y contratistas?',
          answer: 'Los contratistas usan credencial visitante.',
          createdAt: '2026-06-04T12:00:00Z',
          cacheHit: false,
          feedbackValue: 'up',
          feedbackComment: 'Claro',
          citations: [],
        },
      ],
    }),
  ])

  render(<App />)

  expect(await screen.findByText('Credenciales de contratistas')).toBeInTheDocument()
  expect(screen.getByText('Como funcionan las credenciales?')).toBeInTheDocument()
  expect(screen.getByText('Usa credencial visible.')).toBeInTheDocument()
  expect(screen.getByText('Y contratistas?')).toBeInTheDocument()
  expect(screen.getByText('Los contratistas usan credencial visitante.')).toBeInTheDocument()
})

test('sends a stable sessionId for follow-up questions in the active conversation', async () => {
  const fetchMock = stubFetch([
    sessionResponse(),
    jsonResponse(200, { sessions: [] }),
    csrfResponse(),
    sseResponse([
      ['answer-token', { delta: 'Primera respuesta.' }],
      ['citations', { query_audit_event_id: '33333333-3333-3333-3333-333333333333', citations: [] }],
      ['done', {}],
    ]),
    jsonResponse(200, {
      sessions: [
        {
          sessionId: 'client-session',
          title: 'Que regla aplica?',
          lastQuestion: 'Que regla aplica?',
          lastAnswer: 'Primera respuesta.',
          lastActivityAt: '2026-06-04T12:00:00Z',
          turnCount: 1,
        },
      ],
    }),
    sseResponse([
      ['answer-token', { delta: 'Segunda respuesta.' }],
      ['citations', { query_audit_event_id: '44444444-4444-4444-4444-444444444444', citations: [] }],
      ['done', {}],
    ]),
    jsonResponse(200, {
      sessions: [
        {
          sessionId: 'client-session',
          title: 'Que regla aplica?',
          lastQuestion: 'Y contratistas?',
          lastAnswer: 'Segunda respuesta.',
          lastActivityAt: '2026-06-04T12:01:00Z',
          turnCount: 2,
        },
      ],
    }),
  ])
  const user = userEvent.setup()

  render(<App />)

  await user.type(await screen.findByRole('textbox', { name: 'Pregunta' }), 'Que regla aplica?')
  await user.click(screen.getByRole('button', { name: 'Enviar pregunta' }))
  expect(await screen.findByText('Primera respuesta.')).toBeInTheDocument()

  await user.type(screen.getByRole('textbox', { name: 'Pregunta' }), 'Y contratistas?')
  await user.click(screen.getByRole('button', { name: 'Enviar pregunta' }))
  expect(await screen.findByText('Segunda respuesta.')).toBeInTheDocument()

  const chatBodies = fetchMock.mock.calls.reduce<Array<Record<string, unknown>>>((bodies, call) => {
    const [path, init] = call as unknown as [string, RequestInit]
    if (path === '/api/chat') {
      bodies.push(JSON.parse(String(init.body)) as Record<string, unknown>)
    }
    return bodies
  }, [])

  expect(chatBodies).toHaveLength(2)
  expect(chatBodies[0].question).toBe('Que regla aplica?')
  expect(chatBodies[1].question).toBe('Y contratistas?')
  expect(chatBodies[0].sessionId).toBeTruthy()
  expect(chatBodies[1].sessionId).toBe(chatBodies[0].sessionId)
})

test('renders answer tokens as the SSE stream arrives before completion', async () => {
  const stream = controllableSseStream()
  renderAuthenticatedChat([csrfResponse(), stream.response, jsonResponse(200, { sessions: [] })])
  const user = userEvent.setup()

  await user.type(await screen.findByRole('textbox', { name: 'Pregunta' }), 'Que regla aplica?')
  await user.click(screen.getByRole('button', { name: 'Enviar pregunta' }))

  stream.send('answer-token', { delta: 'Primera parte' })

  expect(await screen.findByText('Primera parte')).toBeInTheDocument()
  expect(screen.queryByRole('button', { name: /^Me sirvi/i })).not.toBeInTheDocument()

  stream.send('answer-token', { delta: ' y final.' })
  stream.send('citations', {
    query_audit_event_id: '33333333-3333-3333-3333-333333333333',
    citations: [],
  })
  stream.send('usage', { input_tokens: 42, cached_tokens: 0, output_tokens: 12, cost_usd: 0.000001 })
  stream.send('done', {})
  stream.close()

  expect(await screen.findByText('Primera parte y final.')).toBeInTheDocument()
  expect(screen.getByRole('button', { name: /^Me sirvi/i })).toBeInTheDocument()
})

test('drops the pending partial answer when the SSE stream fails before done', async () => {
  const stream = controllableSseStream()
  renderAuthenticatedChat([csrfResponse(), stream.response])
  const user = userEvent.setup()

  await user.type(await screen.findByRole('textbox', { name: 'Pregunta' }), 'Que regla aplica?')
  await user.click(screen.getByRole('button', { name: 'Enviar pregunta' }))

  stream.send('answer-token', { delta: 'Respuesta parcial' })
  expect(await screen.findByText('Respuesta parcial')).toBeInTheDocument()

  stream.fail(new Error('stream interrupted'))

  expect(await screen.findByRole('alert')).toHaveTextContent('No se pudo responder la pregunta.')
  await waitFor(() => {
    expect(screen.queryByText('Respuesta parcial')).not.toBeInTheDocument()
  })
})

test('opens citation drawer from the command palette', async () => {
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
  window.dispatchEvent(new KeyboardEvent('keydown', { key: 'k', ctrlKey: true, bubbles: true }))
  await user.click(await screen.findByText('Ver citas'))

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
  const open = vi.fn()
  vi.stubGlobal('open', open)
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
    jsonResponse(200, { sessions: [] }),
    jsonResponse(200, { url: 'https://docs.client.com/open?documentId=55555555-5555-5555-5555-555555555555' }),
  ])
  const user = userEvent.setup()

  await user.type(await screen.findByRole('textbox', { name: 'Pregunta' }), 'Que regla aplica?')
  await user.click(screen.getByRole('button', { name: 'Enviar pregunta' }))
  await user.click(await screen.findByRole('button', { name: 'Ver citas (1)' }))
  await user.click(await screen.findByRole('button', { name: 'Abrir cita Seguridad' }))

  expect(fetchMock).toHaveBeenLastCalledWith(
    '/api/viewer/links',
    expect.objectContaining({ method: 'POST' }),
  )
  expect(assign).not.toHaveBeenCalled()
  expect(open).toHaveBeenCalledWith(
    'https://docs.client.com/open?documentId=55555555-5555-5555-5555-555555555555',
    '_blank',
    'noopener,noreferrer',
  )
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

  const heading = await screen.findByRole('heading', { name: 'Iniciar sesión' })
  expect(heading.closest('main')).toHaveClass('auth-shell')
  expect(heading.closest('.auth-card-stack')).toHaveClass('auth-card-stack')
  expect(heading.closest('form')).toHaveClass('auth-card')
  expect(screen.getByText('Usá tu cuenta para acceder al chat de instrucciones.')).toBeInTheDocument()
  expect(screen.getByRole('textbox', { name: 'Email' }).closest('label')).toHaveClass('auth-field')
  expect(screen.getByRole('button', { name: 'Entrar al chat' })).toHaveClass('auth-submit')
  expect(screen.getByLabelText('Idioma')).toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Cambiar tema' })).toBeInTheDocument()
  await user.type(screen.getByRole('textbox', { name: 'Email' }), 'viewer@example.com')
  await user.type(screen.getByLabelText('Contraseña'), 'password')
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
    sessionResponse(),
    jsonResponse(200, { sessions: [] }),
    ...responses,
  ])
  render(<App />)
  return fetchMock
}

function sessionResponse() {
  return jsonResponse(200, {
    user: {
      id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
      email: 'viewer@example.com',
      displayName: 'Viewer User',
      roles: ['Viewer'],
      groups: [],
    },
  })
}

function stubFetch(responses: Array<Response | Promise<Response>>) {
  const fetchMock = vi.fn<typeof fetch>(async () => {
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

function controllableSseStream() {
  const encoder = new TextEncoder()
  let controller: ReadableStreamDefaultController<Uint8Array>
  const body = new ReadableStream<Uint8Array>({
    start(nextController) {
      controller = nextController
    },
  })

  return {
    response: new Response(body, {
      status: 200,
      headers: { 'Content-Type': 'text/event-stream' },
    }),
    send(event: string, data: unknown) {
      controller.enqueue(encoder.encode(`event: ${event}\ndata: ${JSON.stringify(data)}\n\n`))
    },
    close() {
      controller.close()
    },
    fail(reason: unknown) {
      controller.error(reason)
    },
  }
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
