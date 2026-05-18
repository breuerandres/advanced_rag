import { afterEach, describe, expect, test, vi } from 'vitest'
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import App from './App'

const usersResponse = [
  {
    id: '11111111-1111-1111-1111-111111111111',
    email: 'ana@example.com',
    displayName: 'Ana Gomez',
    isActive: true,
    roles: ['Admin'],
    groups: [{ id: '22222222-2222-2222-2222-222222222222', name: 'Operaciones' }],
    accessScopeHash: 'scope-hash',
    monthlyBudgetUsd: 5,
    currentSpendUsd: 1.25,
    remainingBudgetUsd: 3.75,
    isBudgetDisabled: false,
  },
]

const documentsResponse = [
  {
    id: '55555555-5555-5555-5555-555555555555',
    title: 'Politica de seguridad',
    state: 'Draft',
    draftVersionNumber: 1,
    publishedVersionNumber: null,
    indexingStatus: 'None',
    updatedAt: '2026-05-17T12:00:00Z',
  },
  {
    id: '66666666-6666-6666-6666-666666666666',
    title: 'Procedimiento de compras',
    state: 'In Review',
    draftVersionNumber: 2,
    publishedVersionNumber: 1,
    indexingStatus: 'Pending',
    updatedAt: '2026-05-17T13:00:00Z',
  },
]

const documentDetail = {
  id: '55555555-5555-5555-5555-555555555555',
  title: 'Politica de seguridad',
  state: 'Draft',
  currentDraftVersion: {
    id: '77777777-7777-7777-7777-777777777777',
    versionNumber: 1,
    state: 'Draft',
    title: 'Politica de seguridad',
    instructionType: 'Politica',
    audience: 'Todos',
    contentHtml: '<p>Usar credencial visible.</p>',
    indexingStatus: 'None',
  },
  currentPublishedVersion: null,
  allowedGroupIds: ['22222222-2222-2222-2222-222222222222'],
  updatedAt: '2026-05-17T12:00:00Z',
}

const configurationResponse = {
  customerTimezone: 'America/Argentina/Buenos_Aires',
  chatModel: 'gpt-4.1-nano',
  embeddingModel: 'text-embedding-3-small',
  embeddingDimensions: 1536,
  defaultMonthlyAiBudgetUsd: 5,
  semanticCacheTtlHours: 24,
  semanticCacheSimilarityThreshold: 0.9,
  chatMaxQuestionChars: 4000,
  importMaxFileSizeMb: 10,
  secrets: [
    { name: 'OpenAI API key', status: 'Configured' },
    { name: 'JWT signing keys', status: 'Configured' },
  ],
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('management users and budgets', () => {
  test('shows persistent management navigation sections', async () => {
    stubFetch([jsonResponse(200, usersResponse), jsonResponse(200, [])])

    render(<App />)

    expect(
      await screen.findByRole('heading', { name: 'Usuarios y presupuestos' }),
    ).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Documentos' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Usuarios y grupos' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Auditoria' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Feedback' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Presupuestos IA' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Configuracion' })).toBeInTheDocument()
  })

  test('shows users with roles, groups, status, budget, spend, and remaining budget', async () => {
    stubFetch([
      jsonResponse(200, usersResponse),
      jsonResponse(200, [{ id: '22222222-2222-2222-2222-222222222222', name: 'Operaciones' }]),
    ])

    render(<App />)

    expect(
      await screen.findByRole('heading', { name: 'Usuarios y presupuestos' }),
    ).toBeInTheDocument()
    const row = screen.getByRole('row', { name: /Ana Gomez/i })

    expect(within(row).getByText('ana@example.com')).toBeInTheDocument()
    expect(within(row).getByText('Admin')).toBeInTheDocument()
    expect(within(row).getByText('Operaciones')).toBeInTheDocument()
    expect(within(row).getByText('Activo')).toBeInTheDocument()
    expect(within(row).getByText('USD 5.00')).toBeInTheDocument()
    expect(within(row).getByText('USD 1.25')).toBeInTheDocument()
    expect(within(row).getByText('USD 3.75')).toBeInTheDocument()
  })

  test('filters users by search text and active status', async () => {
    const mixedUsers = [
      usersResponse[0],
      {
        id: '33333333-3333-3333-3333-333333333333',
        email: 'bruno@example.com',
        displayName: 'Bruno Perez',
        isActive: false,
        roles: ['Viewer'],
        groups: [{ id: '44444444-4444-4444-4444-444444444444', name: 'Ventas' }],
        accessScopeHash: 'other-scope-hash',
        monthlyBudgetUsd: 5,
        currentSpendUsd: 0,
        remainingBudgetUsd: 5,
        isBudgetDisabled: false,
      },
    ]
    stubFetch([
      jsonResponse(200, mixedUsers),
      jsonResponse(200, [
        { id: '22222222-2222-2222-2222-222222222222', name: 'Operaciones' },
        { id: '44444444-4444-4444-4444-444444444444', name: 'Ventas' },
      ]),
    ])
    const user = userEvent.setup()

    render(<App />)

    expect(await screen.findByText('Ana Gomez')).toBeInTheDocument()
    expect(screen.getByText('Bruno Perez')).toBeInTheDocument()

    await user.type(screen.getByRole('searchbox', { name: 'Buscar usuarios' }), 'ventas')

    expect(screen.queryByText('Ana Gomez')).not.toBeInTheDocument()
    expect(screen.getByText('Bruno Perez')).toBeInTheDocument()

    await user.selectOptions(screen.getByRole('combobox', { name: 'Estado' }), 'active')

    expect(screen.queryByText('Ana Gomez')).not.toBeInTheDocument()
    expect(screen.queryByText('Bruno Perez')).not.toBeInTheDocument()
    expect(screen.getByText('No hay usuarios que coincidan con los filtros.')).toBeInTheDocument()
  })

  test('validates non-negative budget values in the edit dialog', async () => {
    stubFetch([jsonResponse(200, usersResponse), jsonResponse(200, [])])
    const user = userEvent.setup()

    render(<App />)

    await user.click(
      await screen.findByRole('button', {
        name: 'Editar presupuesto de Ana Gomez',
      }),
    )
    const input = screen.getByRole('spinbutton', {
      name: 'Presupuesto mensual (USD)',
    })
    await user.clear(input)
    await user.type(input, '-1')
    await user.click(screen.getByRole('button', { name: 'Guardar' }))

    expect(
      screen.getByText('El presupuesto no puede ser negativo.'),
    ).toBeInTheDocument()
  })

  test('saves a budget change and shows a success state', async () => {
    const updatedUser = { ...usersResponse[0], monthlyBudgetUsd: 7, remainingBudgetUsd: 5.75 }
    const fetchMock = stubFetch([
      jsonResponse(200, usersResponse),
      jsonResponse(200, []),
      csrfResponse(),
      jsonResponse(200, updatedUser),
    ])
    const user = userEvent.setup()

    render(<App />)

    await user.click(
      await screen.findByRole('button', {
        name: 'Editar presupuesto de Ana Gomez',
      }),
    )
    const input = screen.getByRole('spinbutton', {
      name: 'Presupuesto mensual (USD)',
    })
    await user.clear(input)
    await user.type(input, '7')
    await user.click(screen.getByRole('button', { name: 'Guardar' }))

    expect(await screen.findByText('Presupuesto actualizado.')).toBeInTheDocument()
    expect(screen.getByText('USD 7.00')).toBeInTheDocument()
    expect(fetchMock).toHaveBeenLastCalledWith(
      '/api/users/11111111-1111-1111-1111-111111111111/ai-budget',
      expect.objectContaining({ method: 'PUT' }),
    )
  })

  test('shows a safe API error state when saving a budget fails', async () => {
    stubFetch([
      jsonResponse(200, usersResponse),
      jsonResponse(200, []),
      csrfResponse(),
      jsonResponse(
        400,
        {
          error: {
            code: 'VALIDATION_FAILED',
            message: 'Monthly budget must be non-negative.',
            details: { field: 'monthlyBudgetUsd' },
            requestId: 'request-123',
          },
        },
        { 'X-Request-ID': 'request-123' },
      ),
    ])
    const user = userEvent.setup()

    render(<App />)

    await user.click(
      await screen.findByRole('button', {
        name: 'Editar presupuesto de Ana Gomez',
      }),
    )
    const input = screen.getByRole('spinbutton', {
      name: 'Presupuesto mensual (USD)',
    })
    await user.clear(input)
    await user.type(input, '9')
    await user.click(screen.getByRole('button', { name: 'Guardar' }))

    expect(
      await screen.findByText('No se pudo actualizar el presupuesto. Referencia: request-123.'),
    ).toBeInTheDocument()
  })
})

describe('management documents', () => {
  test('shows document list filters and indexing status', async () => {
    stubFetch([
      jsonResponse(200, usersResponse),
      jsonResponse(200, [{ id: '22222222-2222-2222-2222-222222222222', name: 'Operaciones' }]),
      jsonResponse(200, documentsResponse),
    ])
    const user = userEvent.setup()

    render(<App />)

    await user.click(await screen.findByRole('link', { name: 'Documentos' }))

    expect(await screen.findByText('Politica de seguridad')).toBeInTheDocument()
    expect(screen.getByText('Procedimiento de compras')).toBeInTheDocument()
    expect(screen.getByText('Indexacion pendiente')).toBeInTheDocument()

    await user.selectOptions(screen.getByRole('combobox', { name: 'Estado del documento' }), 'Draft')

    expect(screen.getByText('Politica de seguridad')).toBeInTheDocument()
    expect(screen.queryByText('Procedimiento de compras')).not.toBeInTheDocument()
  })

  test('opens editor, tracks dirty state, and sends a valid draft to review', async () => {
    const fetchMock = stubFetch([
      jsonResponse(200, usersResponse),
      jsonResponse(200, [{ id: '22222222-2222-2222-2222-222222222222', name: 'Operaciones' }]),
      jsonResponse(200, documentsResponse),
      jsonResponse(200, documentDetail),
      csrfResponse(),
      jsonResponse(200, { ...documentDetail, title: 'Politica actualizada' }),
      csrfResponse(),
      jsonResponse(200, { ...documentDetail, state: 'In Review' }),
    ])
    const user = userEvent.setup()

    render(<App />)

    await user.click(await screen.findByRole('link', { name: 'Documentos' }))
    await user.click(await screen.findByRole('button', { name: 'Editar Politica de seguridad' }))
    await user.clear(screen.getByRole('textbox', { name: 'Titulo' }))
    await user.type(screen.getByRole('textbox', { name: 'Titulo' }), 'Politica actualizada')

    expect(screen.getByText('Cambios sin guardar')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Guardar borrador' }))
    expect(await screen.findByText('Borrador guardado.')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Enviar a revision' }))
    expect(await screen.findByText('Documento enviado a revision.')).toBeInTheDocument()
    expect(fetchMock).toHaveBeenLastCalledWith(
      '/api/documents/55555555-5555-5555-5555-555555555555/send-to-review',
      expect.objectContaining({ method: 'POST' }),
    )
  })

  test('shows review validation errors before send to review', async () => {
    stubFetch([
      jsonResponse(200, usersResponse),
      jsonResponse(200, []),
      jsonResponse(200, documentsResponse),
      jsonResponse(200, { ...documentDetail, allowedGroupIds: [], currentDraftVersion: { ...documentDetail.currentDraftVersion, title: '', contentHtml: '' } }),
    ])
    const user = userEvent.setup()

    render(<App />)

    await user.click(await screen.findByRole('link', { name: 'Documentos' }))
    await user.click(await screen.findByRole('button', { name: 'Editar Politica de seguridad' }))
    await user.click(await screen.findByRole('button', { name: 'Enviar a revision' }))

    expect(screen.getByText('Completa titulo, tipo, audiencia, grupos y contenido antes de enviar a revision.')).toBeInTheDocument()
  })

  test('imports extracted text and shows safe import errors', async () => {
    stubFetch([
      jsonResponse(200, usersResponse),
      jsonResponse(200, []),
      jsonResponse(200, documentsResponse),
      jsonResponse(200, documentDetail),
      csrfResponse(),
      jsonResponse(200, {
        text: 'Contenido importado',
        metadata: {
          originalFilename: 'manual.docx',
          mimeType: 'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
          sizeBytes: 2048,
          sha256Hash: 'hash',
          extractionStatus: 'Extracted',
        },
      }),
      csrfResponse(),
      jsonResponse(422, {
        error: {
          code: 'IMPORT_TEXT_NOT_EXTRACTABLE',
          message: 'Uploaded file has no extractable text.',
          details: null,
          requestId: 'request-import',
        },
      }),
    ])
    const user = userEvent.setup()

    render(<App />)

    await user.click(await screen.findByRole('link', { name: 'Documentos' }))
    await user.click(await screen.findByRole('button', { name: 'Editar Politica de seguridad' }))
    await user.upload(
      await screen.findByLabelText('Importar PDF o DOCX'),
      new File(['docx'], 'manual.docx', {
        type: 'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
      }),
    )

    expect(await screen.findByDisplayValue('Contenido importado')).toBeInTheDocument()
    expect(screen.getByText('Texto importado desde manual.docx.')).toBeInTheDocument()

    await user.upload(
      screen.getByLabelText('Importar PDF o DOCX'),
      new File(['pdf'], 'scan.pdf', { type: 'application/pdf' }),
    )

    expect(
      await screen.findByText('No se pudo extraer texto del archivo. Referencia: request-import.'),
    ).toBeInTheDocument()
  })

  test('archives and restores documents from the list', async () => {
    const fetchMock = stubFetch([
      jsonResponse(200, usersResponse),
      jsonResponse(200, []),
      jsonResponse(200, [
        documentsResponse[0],
        { ...documentsResponse[1], id: '88888888-8888-8888-8888-888888888888', state: 'Archived' },
      ]),
      csrfResponse(),
      jsonResponse(200, { ...documentDetail, state: 'Archived' }),
      csrfResponse(),
      jsonResponse(200, { ...documentDetail, id: '88888888-8888-8888-8888-888888888888', state: 'Draft' }),
    ])
    const user = userEvent.setup()

    render(<App />)

    await user.click(await screen.findByRole('link', { name: 'Documentos' }))
    await user.click(await screen.findByRole('button', { name: 'Archivar Politica de seguridad' }))
    expect(await screen.findByText('Documento archivado.')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Restaurar Procedimiento de compras' }))
    expect(await screen.findByText('Documento restaurado.')).toBeInTheDocument()
    expect(fetchMock).toHaveBeenLastCalledWith(
      '/api/documents/88888888-8888-8888-8888-888888888888/restore',
      expect.objectContaining({ method: 'POST' }),
    )
  })

  test('opens management document viewer through exchange links', async () => {
    const assign = vi.fn()
    Object.defineProperty(window, 'location', {
      configurable: true,
      value: { assign },
    })
    const fetchMock = stubFetch([
      jsonResponse(200, usersResponse),
      jsonResponse(200, []),
      jsonResponse(200, documentsResponse),
      csrfResponse(),
      jsonResponse(200, { url: 'https://docs.client.com/open?code=manager-code', expiresAt: '2026-05-18T12:01:00Z' }),
    ])
    const user = userEvent.setup()

    render(<App />)

    await user.click(await screen.findByRole('link', { name: 'Documentos' }))
    await user.click(await screen.findByRole('button', { name: 'Abrir visor de Politica de seguridad' }))

    expect(fetchMock).toHaveBeenLastCalledWith(
      '/api/viewer/links',
      expect.objectContaining({ method: 'POST' }),
    )
    expect(assign).toHaveBeenCalledWith('https://docs.client.com/open?code=manager-code')
  })

  test('retries failed indexing from the document list', async () => {
    const fetchMock = stubFetch([
      jsonResponse(200, usersResponse),
      jsonResponse(200, []),
      jsonResponse(200, [
        {
          ...documentsResponse[0],
          indexingStatus: 'Failed',
          state: 'In Review',
        },
      ]),
      csrfResponse(),
      jsonResponse(200, {
        ...documentDetail,
        state: 'Published',
        currentDraftVersion: {
          ...documentDetail.currentDraftVersion,
          indexingStatus: 'Succeeded',
        },
      }),
    ])
    const user = userEvent.setup()

    render(<App />)

    await user.click(await screen.findByRole('link', { name: 'Documentos' }))
    await user.click(await screen.findByRole('button', { name: 'Reintentar indexacion de Politica de seguridad' }))

    expect(await screen.findByText('Indexacion reintentada.')).toBeInTheDocument()
    expect(fetchMock).toHaveBeenLastCalledWith(
      '/api/documents/55555555-5555-5555-5555-555555555555/request-publish',
      expect.objectContaining({ method: 'POST' }),
    )
  })
})

describe('management configuration', () => {
  test('shows operational defaults and hides secret values', async () => {
    stubFetch([
      jsonResponse(200, usersResponse),
      jsonResponse(200, []),
      jsonResponse(200, configurationResponse),
    ])
    const user = userEvent.setup()

    render(<App />)

    await user.click(await screen.findByRole('link', { name: 'Configuracion' }))

    expect(await screen.findByRole('heading', { name: 'Configuracion operativa' })).toBeInTheDocument()
    expect(screen.getByText('America/Argentina/Buenos_Aires')).toBeInTheDocument()
    expect(screen.getByText('gpt-4.1-nano')).toBeInTheDocument()
    expect(screen.getByText('text-embedding-3-small')).toBeInTheDocument()
    expect(screen.getByText('1536 dimensiones')).toBeInTheDocument()
    expect(screen.getByText('USD 5.00')).toBeInTheDocument()
    expect(screen.getByText('24 horas')).toBeInTheDocument()
    expect(screen.getByText('0.90')).toBeInTheDocument()
    expect(screen.getByText('Valores protegidos por secretos')).toBeInTheDocument()
    expect(screen.queryByText(/sk-/i)).not.toBeInTheDocument()
  })

  test('shows configuration load errors with a safe state', async () => {
    stubFetch([
      jsonResponse(200, usersResponse),
      jsonResponse(200, []),
      jsonResponse(500, {
        error: {
          code: 'INTERNAL_ERROR',
          message: 'An internal error occurred.',
          details: null,
          requestId: 'config-request',
        },
      }),
    ])
    const user = userEvent.setup()

    render(<App />)

    await user.click(await screen.findByRole('link', { name: 'Configuracion' }))

    expect(
      await screen.findByText('No se pudo cargar la configuracion. Referencia: config-request.'),
    ).toBeInTheDocument()
  })
})

describe('management feedback reporting', () => {
  test('shows empty, filter, and result states for feedback review', async () => {
    stubFetch([
      jsonResponse(200, usersResponse),
      jsonResponse(200, [{ id: '22222222-2222-2222-2222-222222222222', name: 'Operaciones' }]),
      jsonResponse(200, []),
      jsonResponse(200, [
        {
          queryAuditEventId: '99999999-9999-9999-9999-999999999999',
          userId: '11111111-1111-1111-1111-111111111111',
          userDisplayName: 'Ana Gomez',
          question: 'Que regla aplica?',
          answerSummary: 'Usa credencial visible.',
          feedbackValue: 'down',
          feedbackComment: 'Falto detalle',
          feedbackUpdatedAt: '2026-05-18T12:00:00Z',
          createdAt: '2026-05-18T11:59:00Z',
          cacheHit: false,
          requestId: 'req-report',
          citations: [
            {
              instructionId: '55555555-5555-5555-5555-555555555555',
              instructionVersionId: '77777777-7777-7777-7777-777777777777',
              headingPath: ['Seguridad'],
            },
          ],
        },
      ]),
    ])
    const user = userEvent.setup()

    render(<App />)

    await user.click(await screen.findByRole('link', { name: 'Feedback' }))

    expect(await screen.findByText('Todavia no hay feedback registrado.')).toBeInTheDocument()
    await user.selectOptions(screen.getByRole('combobox', { name: 'Polaridad' }), 'negative')
    await user.click(screen.getByRole('button', { name: 'Aplicar filtros' }))

    expect(await screen.findByText('Que regla aplica?')).toBeInTheDocument()
    expect(screen.getByText('No sirvio')).toBeInTheDocument()
    expect(screen.getByText('Falto detalle')).toBeInTheDocument()
    expect(screen.getByText('req-report')).toBeInTheDocument()
  })
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

function jsonResponse(status: number, body: unknown, headers: Record<string, string> = {}) {
  return new Response(JSON.stringify(body), {
    status,
    headers: {
      'Content-Type': 'application/json',
      ...headers,
    },
  })
}

function csrfResponse() {
  return jsonResponse(200, { status: 'ok' }, { 'X-CSRF-Token': 'csrf-token' })
}
