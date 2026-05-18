import { afterEach, expect, test, vi } from 'vitest'
import { cleanup, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import App from './App'

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

test('renders the chat shell label', () => {
  render(<App />)

  expect(screen.getByRole('heading', { name: 'Instruction Chat' })).toBeInTheDocument()
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
  await user.click(screen.getByRole('button', { name: 'No me sirvio' }))
  await user.type(screen.getByRole('textbox', { name: 'Comentario opcional' }), 'Falto detalle')
  await user.click(screen.getByRole('button', { name: 'Enviar feedback' }))

  expect(await screen.findByText('Feedback registrado.')).toBeInTheDocument()
  await user.click(screen.getByRole('button', { name: 'Me sirvio' }))
  await user.click(screen.getByRole('button', { name: 'Actualizar feedback' }))

  expect(fetchMock).toHaveBeenLastCalledWith(
    '/api/feedback/33333333-3333-3333-3333-333333333333',
    expect.objectContaining({ method: 'POST' }),
  )
})

function stubFetch(responses: Response[]) {
  const fetchMock = vi.fn(async () => {
    const response = responses.shift()
    if (!response) {
      throw new Error('Unexpected fetch call.')
    }

    return response
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

function sseResponse(events: [string, unknown][]) {
  const body = events
    .map(([event, data]) => `event: ${event}\ndata: ${JSON.stringify(data)}\n\n`)
    .join('')
  return new Response(body, {
    status: 200,
    headers: { 'Content-Type': 'text/event-stream' },
  })
}
