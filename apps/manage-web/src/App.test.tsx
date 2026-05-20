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

const sessionUser = {
  id: '11111111-1111-1111-1111-111111111111',
  email: 'ana@example.com',
  displayName: 'Ana Gomez',
  roles: ['Admin'],
  groups: [{ id: '22222222-2222-2222-2222-222222222222', name: 'Operaciones' }],
}

const documentManagerSessionUser = {
  ...sessionUser,
  roles: ['DocumentManager'],
}

const documentsResponse = [
  {
    id: '55555555-5555-5555-5555-555555555555',
    title: 'Politica de seguridad',
    state: 'Draft',
    instructionType: 'Politica',
    audience: 'Todos',
    allowedGroupIds: ['22222222-2222-2222-2222-222222222222'],
    draftVersionNumber: 1,
    publishedVersionNumber: null,
    indexingStatus: 'None',
    updatedAt: '2026-05-17T12:00:00Z',
  },
  {
    id: '66666666-6666-6666-6666-666666666666',
    title: 'Procedimiento de compras',
    state: 'In Review',
    instructionType: 'Procedimiento',
    audience: 'Compras',
    allowedGroupIds: ['44444444-4444-4444-4444-444444444444'],
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

const inReviewDocumentDetail = {
  ...documentDetail,
  id: '66666666-6666-6666-6666-666666666666',
  title: 'Procedimiento de compras',
  state: 'In Review',
  currentDraftVersion: {
    ...documentDetail.currentDraftVersion!,
    id: '99999999-9999-9999-9999-999999999999',
    versionNumber: 2,
    state: 'In Review',
    title: 'Procedimiento de compras',
    instructionType: 'Procedimiento',
    audience: 'Compras',
    indexingStatus: 'Pending',
  },
  currentPublishedVersion: {
    ...documentDetail.currentDraftVersion!,
    id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
    versionNumber: 1,
    state: 'Published',
    title: 'Procedimiento de compras',
    instructionType: 'Procedimiento',
    audience: 'Compras',
    indexingStatus: 'Succeeded',
  },
  allowedGroupIds: ['44444444-4444-4444-4444-444444444444'],
  updatedAt: '2026-05-17T13:00:00Z',
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

const auditEventsResponse = [
  {
    id: '99999999-9999-9999-9999-999999999999',
    actorUserId: '11111111-1111-1111-1111-111111111111',
    actorDisplayName: 'Ana Gomez',
    eventType: 'instruction.created',
    eventLabel: 'Documento creado',
    entityType: 'instruction',
    entityId: '55555555-5555-5555-5555-555555555555',
    details: { instructionId: '55555555-5555-5555-5555-555555555555' },
    requestId: 'req-doc-create',
    createdAt: '2026-05-20T12:00:00Z',
  },
]

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('management users and budgets', () => {
  test('shows first-run setup and creates the first administrator', async () => {
    const fetchMock = stubFetch(
      [
        csrfResponse(),
        jsonResponse(201, {
          user: {
            id: '99999999-9999-9999-9999-999999999999',
            email: 'admin@example.com',
            displayName: 'Admin Inicial',
            roles: ['Admin'],
          },
        }),
      ],
      { setupRequired: true },
    )
    const user = userEvent.setup()

    render(<App />)

    expect(await screen.findByRole('heading', { name: 'Configurá el primer administrador' })).toBeInTheDocument()

    await user.type(screen.getByRole('textbox', { name: 'Email' }), 'admin@example.com')
    await user.type(screen.getByRole('textbox', { name: 'Nombre visible' }), 'Admin Inicial')
    await user.type(screen.getByLabelText('Contraseña'), 'Correct Horse Battery Staple 42!')
    await user.click(screen.getByRole('button', { name: 'Crear administrador' }))

    expect(await screen.findByText('Administrador creado. Iniciá sesión para continuar.')).toBeInTheDocument()
    expect(fetchMock).toHaveBeenLastCalledWith(
      '/api/setup/admin',
      expect.objectContaining({ method: 'POST' }),
    )
  })

  test('shows login when setup is complete and opens the shell after authentication', async () => {
    stubFetch(
      [
        csrfResponse(),
        jsonResponse(200, { user: sessionUser }),
        jsonResponse(200, usersResponse),
        jsonResponse(200, [{ id: '22222222-2222-2222-2222-222222222222', name: 'Operaciones' }]),
      ],
      { session: null },
    )
    const user = userEvent.setup()

    render(<App />)

    expect(await screen.findByRole('heading', { name: 'Ingresá a la consola' })).toBeInTheDocument()

    await user.type(screen.getByRole('textbox', { name: 'Email' }), 'ana@example.com')
    await user.type(screen.getByLabelText('Contraseña'), 'password')
    await user.click(screen.getByRole('button', { name: 'Ingresar' }))

    expect(await screen.findByRole('heading', { name: 'Usuarios y grupos' })).toBeInTheDocument()
    expect(screen.getByText('Ana Gomez')).toBeInTheDocument()
  })

  test('logs out and returns to the login surface', async () => {
    stubFetch([
      jsonResponse(200, usersResponse),
      jsonResponse(200, []),
      csrfResponse(),
      jsonResponse(200, { status: 'ok' }),
    ])
    const user = userEvent.setup()

    render(<App />)

    expect(await screen.findByRole('heading', { name: 'Usuarios y grupos' })).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Cerrar sesión' }))

    expect(await screen.findByRole('heading', { name: 'Ingresá a la consola' })).toBeInTheDocument()
  })

  test('shows persistent management navigation sections', async () => {
    stubFetch([jsonResponse(200, usersResponse), jsonResponse(200, [])])

    render(<App />)

    expect(
      await screen.findByRole('heading', { name: 'Usuarios y grupos' }),
    ).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Documentos' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Usuarios y grupos' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Auditoria' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Feedback' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Configuracion' })).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Presupuestos IA' })).not.toBeInTheDocument()
  })

  test('keeps active session controls at the bottom of the sidebar', async () => {
    stubFetch([jsonResponse(200, usersResponse), jsonResponse(200, [])])

    render(<App />)

    expect(
      await screen.findByRole('heading', { name: 'Usuarios y grupos' }),
    ).toBeInTheDocument()
    const sidebar = screen.getByLabelText('Navegacion principal')

    expect(within(sidebar).getByText('Sesión activa')).toBeInTheDocument()
    expect(within(sidebar).getByText('ana@example.com')).toBeInTheDocument()
    expect(within(sidebar).getByText('Admin')).toBeInTheDocument()
    expect(within(sidebar).getByRole('button', { name: 'Cerrar sesión' })).toBeInTheDocument()
  })

  test('shows users with roles, groups, status, budget, spend, and remaining budget', async () => {
    stubFetch([
      jsonResponse(200, usersResponse),
      jsonResponse(200, [{ id: '22222222-2222-2222-2222-222222222222', name: 'Operaciones' }]),
    ])

    render(<App />)

    expect(
      await screen.findByRole('heading', { name: 'Usuarios y grupos' }),
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

  test('adds tooltips to user action buttons', async () => {
    stubFetch([jsonResponse(200, usersResponse), jsonResponse(200, [])])

    render(<App />)

    expect(
      await screen.findByRole('heading', { name: 'Usuarios y grupos' }),
    ).toBeInTheDocument()

    expectActionTooltip(screen.getByRole('button', { name: 'Actualizar usuarios' }))
    expectActionTooltip(screen.getByRole('button', { name: 'Editar presupuesto de Ana Gomez' }))
    expectActionTooltip(screen.getByRole('button', { name: 'Dar de baja a Ana Gomez' }))
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

  test('deactivates a user with a logical delete action', async () => {
    const inactiveUser = { ...usersResponse[0], isActive: false }
    const fetchMock = stubFetch([
      jsonResponse(200, usersResponse),
      jsonResponse(200, []),
      csrfResponse(),
      jsonResponse(200, inactiveUser),
    ])
    const user = userEvent.setup()

    render(<App />)

    await user.click(await screen.findByRole('button', { name: 'Dar de baja a Ana Gomez' }))

    expect(await screen.findByText('Usuario dado de baja.')).toBeInTheDocument()
    expect(screen.getByText('Inactivo')).toBeInTheDocument()
    expect(fetchMock).toHaveBeenLastCalledWith(
      '/api/users/11111111-1111-1111-1111-111111111111/status',
      expect.objectContaining({ method: 'PATCH' }),
    )
  })

  test('creates a group from the management UI', async () => {
    const fetchMock = stubFetch([
      jsonResponse(200, usersResponse),
      jsonResponse(200, []),
      csrfResponse(),
      jsonResponse(201, { id: '33333333-3333-3333-3333-333333333333', name: 'Ventas' }),
    ])
    const user = userEvent.setup()

    render(<App />)

    await user.click(await screen.findByRole('button', { name: 'Crear grupo' }))
    await user.type(screen.getByRole('textbox', { name: 'Nombre del grupo' }), 'Ventas')
    await user.click(screen.getByRole('button', { name: 'Guardar grupo' }))

    expect(await screen.findByText('Grupo creado.')).toBeInTheDocument()
    expect(fetchMock).toHaveBeenLastCalledWith(
      '/api/groups',
      expect.objectContaining({ method: 'POST' }),
    )
  })

  test('creates a viewer user with group access from the management UI', async () => {
    const createdViewer = {
      id: '44444444-4444-4444-4444-444444444444',
      email: 'viewer@example.com',
      displayName: 'Viewer Demo',
      isActive: true,
      roles: ['Viewer'],
      groups: [{ id: '22222222-2222-2222-2222-222222222222', name: 'Operaciones' }],
      accessScopeHash: 'viewer-scope',
      monthlyBudgetUsd: 5,
      currentSpendUsd: 0,
      remainingBudgetUsd: 5,
      isBudgetDisabled: false,
    }
    const fetchMock = stubFetch([
      jsonResponse(200, usersResponse),
      jsonResponse(200, [{ id: '22222222-2222-2222-2222-222222222222', name: 'Operaciones' }]),
      csrfResponse(),
      jsonResponse(201, createdViewer),
    ])
    const user = userEvent.setup()

    render(<App />)

    await user.click(await screen.findByRole('button', { name: 'Crear usuario' }))
    await user.type(screen.getByRole('textbox', { name: 'Email' }), 'viewer@example.com')
    await user.type(screen.getByRole('textbox', { name: 'Nombre visible' }), 'Viewer Demo')
    await user.type(screen.getByLabelText('Contraseña temporal'), 'DemoPassword!42')
    await user.selectOptions(screen.getByRole('combobox', { name: 'Rol' }), 'Viewer')
    await user.click(screen.getByRole('checkbox', { name: 'Operaciones' }))
    await user.click(screen.getByRole('button', { name: 'Crear usuario' }))

    expect(await screen.findByText('Usuario creado.')).toBeInTheDocument()
    expect(screen.getByText('Viewer Demo')).toBeInTheDocument()
    expect(fetchMock).toHaveBeenLastCalledWith(
      '/api/users',
      expect.objectContaining({ method: 'POST' }),
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
  test('shows document filters for searchable document attributes', async () => {
    stubFetch([
      jsonResponse(200, usersResponse),
      jsonResponse(200, [
        { id: '22222222-2222-2222-2222-222222222222', name: 'Operaciones' },
        { id: '44444444-4444-4444-4444-444444444444', name: 'Compras' },
      ]),
      jsonResponse(200, documentsResponse),
      jsonResponse(200, [
        { id: '22222222-2222-2222-2222-222222222222', name: 'Operaciones' },
        { id: '44444444-4444-4444-4444-444444444444', name: 'Compras' },
      ]),
    ])
    const user = userEvent.setup()

    render(<App />)

    await user.click(await screen.findByRole('link', { name: 'Documentos' }))

    expect(await screen.findByText('Politica de seguridad')).toBeInTheDocument()
    expect(screen.getByText('Procedimiento de compras')).toBeInTheDocument()
    expect(screen.getByText('Indexacion pendiente')).toBeInTheDocument()

    await user.type(screen.getByRole('searchbox', { name: 'Buscar documentos' }), 'compras')

    expect(screen.queryByText('Politica de seguridad')).not.toBeInTheDocument()
    expect(screen.getByText('Procedimiento de compras')).toBeInTheDocument()

    await user.clear(screen.getByRole('searchbox', { name: 'Buscar documentos' }))
    await user.selectOptions(screen.getByRole('combobox', { name: 'Estado del documento' }), 'Draft')

    expect(screen.getByText('Politica de seguridad')).toBeInTheDocument()
    expect(screen.queryByText('Procedimiento de compras')).not.toBeInTheDocument()

    await user.selectOptions(screen.getByRole('combobox', { name: 'Estado del documento' }), 'all')
    await user.selectOptions(screen.getByRole('combobox', { name: 'Indexacion' }), 'Pending')

    expect(screen.queryByText('Politica de seguridad')).not.toBeInTheDocument()
    expect(screen.getByText('Procedimiento de compras')).toBeInTheDocument()

    await user.selectOptions(screen.getByRole('combobox', { name: 'Tipo' }), 'Procedimiento')
    await user.selectOptions(screen.getByRole('combobox', { name: 'Audiencia' }), 'Compras')
    await user.selectOptions(screen.getByRole('combobox', { name: 'Acceso' }), 'with-groups')

    expect(screen.getByText('Procedimiento de compras')).toBeInTheDocument()
  })

  test('adds tooltips to document row and editor action buttons', async () => {
    stubFetch([
      jsonResponse(200, usersResponse),
      jsonResponse(200, [
        { id: '22222222-2222-2222-2222-222222222222', name: 'Operaciones' },
        { id: '44444444-4444-4444-4444-444444444444', name: 'Compras' },
      ]),
      jsonResponse(200, documentsResponse),
      jsonResponse(200, [
        { id: '22222222-2222-2222-2222-222222222222', name: 'Operaciones' },
        { id: '44444444-4444-4444-4444-444444444444', name: 'Compras' },
      ]),
    ])
    const user = userEvent.setup()

    render(<App />)

    await user.click(await screen.findByRole('link', { name: 'Documentos' }))

    expectActionTooltip(await screen.findByRole('button', { name: 'Actualizar documentos' }))
    expectActionTooltip(screen.getByRole('button', { name: 'Editar Politica de seguridad' }))
    expectActionTooltip(screen.getByRole('button', { name: 'Abrir visor de Politica de seguridad' }))
    expectActionTooltip(screen.getByRole('button', { name: 'Archivar Politica de seguridad' }))

    await user.click(screen.getByRole('button', { name: 'Crear documento' }))

    expectActionTooltip(await screen.findByRole('button', { name: 'Negrita' }))
    expectActionTooltip(screen.getByRole('button', { name: 'Insertar tabla' }))
    expectActionTooltip(screen.getByRole('button', { name: 'Insertar imagen' }))
  })

  test('creates a document with the full-screen TipTap editor and assigned groups', async () => {
    const createdDocument = {
      ...documentDetail,
      id: '99999999-9999-9999-9999-999999999999',
      title: 'Nueva instruccion',
      currentDraftVersion: {
        ...documentDetail.currentDraftVersion!,
        title: 'Nueva instruccion',
        instructionType: 'Procedimiento',
        audience: 'Operaciones',
        contentHtml: '<p>Usar casco visible.</p>',
      },
    }
    const fetchMock = stubFetch([
      jsonResponse(200, usersResponse),
      jsonResponse(200, [{ id: '22222222-2222-2222-2222-222222222222', name: 'Operaciones' }]),
      jsonResponse(200, documentsResponse),
      jsonResponse(200, [{ id: '22222222-2222-2222-2222-222222222222', name: 'Operaciones' }]),
      csrfResponse(),
      jsonResponse(201, createdDocument),
    ])
    const user = userEvent.setup()

    render(<App />)

    await user.click(await screen.findByRole('link', { name: 'Documentos' }))
    await user.click(await screen.findByRole('button', { name: 'Crear documento' }))

    expect(await screen.findByRole('heading', { name: 'Crear documento' })).toBeInTheDocument()
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(screen.getByRole('tab', { name: 'Editor' })).toHaveAttribute('aria-selected', 'true')
    expect(screen.getByRole('tab', { name: 'Listado' })).toHaveAttribute('aria-selected', 'false')
    expect(screen.getByRole('button', { name: 'Volver al listado' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Negrita' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Cursiva' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Subrayado' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Titulo 2' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Lista con vinetas' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Insertar tabla' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Insertar imagen' })).toBeInTheDocument()

    await user.type(screen.getByRole('textbox', { name: 'Titulo' }), 'Nueva instruccion')
    await user.type(screen.getByRole('textbox', { name: 'Tipo' }), 'Procedimiento')
    await user.type(screen.getByRole('textbox', { name: 'Audiencia' }), 'Operaciones')
    await user.click(screen.getByRole('checkbox', { name: 'Operaciones' }))
    await user.type(screen.getByRole('textbox', { name: 'Contenido del documento' }), 'Usar casco visible.')
    await user.click(screen.getByRole('button', { name: 'Guardar borrador' }))

    expect(await screen.findByText('Documento creado.')).toBeInTheDocument()
    expect(fetchMock).toHaveBeenLastCalledWith(
      '/api/documents',
      expect.objectContaining({ method: 'POST' }),
    )
  })

  test('opens editor, tracks dirty state, and sends a valid draft to review', async () => {
    const fetchMock = stubFetch([
      jsonResponse(200, usersResponse),
      jsonResponse(200, [{ id: '22222222-2222-2222-2222-222222222222', name: 'Operaciones' }]),
      jsonResponse(200, documentsResponse),
      jsonResponse(200, [{ id: '22222222-2222-2222-2222-222222222222', name: 'Operaciones' }]),
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
      jsonResponse(200, []),
      jsonResponse(200, { ...documentDetail, allowedGroupIds: [], currentDraftVersion: { ...documentDetail.currentDraftVersion!, title: '', contentHtml: '' } }),
    ])
    const user = userEvent.setup()

    render(<App />)

    await user.click(await screen.findByRole('link', { name: 'Documentos' }))
    await user.click(await screen.findByRole('button', { name: 'Editar Politica de seguridad' }))
    await user.click(await screen.findByRole('button', { name: 'Enviar a revision' }))

    expect(screen.getByText('Completa titulo, tipo, audiencia, grupos y contenido antes de enviar a revision.')).toBeInTheDocument()
  })

  test('shows publish instead of send to review for admins when a document is in review', async () => {
    const fetchMock = stubFetch([
      jsonResponse(200, usersResponse),
      jsonResponse(200, [
        { id: '22222222-2222-2222-2222-222222222222', name: 'Operaciones' },
        { id: '44444444-4444-4444-4444-444444444444', name: 'Compras' },
      ]),
      jsonResponse(200, documentsResponse),
      jsonResponse(200, [
        { id: '22222222-2222-2222-2222-222222222222', name: 'Operaciones' },
        { id: '44444444-4444-4444-4444-444444444444', name: 'Compras' },
      ]),
      jsonResponse(200, inReviewDocumentDetail),
      csrfResponse(),
      jsonResponse(200, { ...inReviewDocumentDetail, state: 'Published', currentDraftVersion: null }),
    ])
    const user = userEvent.setup()

    render(<App />)

    await user.click(await screen.findByRole('link', { name: 'Documentos' }))
    await user.click(await screen.findByRole('button', { name: 'Editar Procedimiento de compras' }))

    expect(await screen.findByRole('button', { name: 'Publicar' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Enviar a revision' })).not.toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Publicar' }))

    expect(await screen.findByText('Documento publicado.')).toBeInTheDocument()
    expect(fetchMock).toHaveBeenLastCalledWith(
      '/api/documents/66666666-6666-6666-6666-666666666666/request-publish',
      expect.objectContaining({ method: 'POST' }),
    )
  })

  test('hides publish and send-to-review actions from document managers when a document is in review', async () => {
    stubFetch(
      [
        jsonResponse(200, usersResponse),
        jsonResponse(200, [
          { id: '22222222-2222-2222-2222-222222222222', name: 'Operaciones' },
          { id: '44444444-4444-4444-4444-444444444444', name: 'Compras' },
        ]),
        jsonResponse(200, documentsResponse),
        jsonResponse(200, [
          { id: '22222222-2222-2222-2222-222222222222', name: 'Operaciones' },
          { id: '44444444-4444-4444-4444-444444444444', name: 'Compras' },
        ]),
        jsonResponse(200, inReviewDocumentDetail),
      ],
      { session: documentManagerSessionUser },
    )
    const user = userEvent.setup()

    render(<App />)

    await user.click(await screen.findByRole('link', { name: 'Documentos' }))
    await user.click(await screen.findByRole('button', { name: 'Editar Procedimiento de compras' }))

    expect(await screen.findByRole('heading', { name: 'Editar documento' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Publicar' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Enviar a revision' })).not.toBeInTheDocument()
  })

  test('imports extracted text and shows safe import errors', async () => {
    stubFetch([
      jsonResponse(200, usersResponse),
      jsonResponse(200, []),
      jsonResponse(200, documentsResponse),
      jsonResponse(200, []),
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

    expect(screen.getByText('Seleccionar archivo')).toBeInTheDocument()
    expect(screen.getByText('PDF o DOCX, maximo 10 MB.')).toBeInTheDocument()

    await user.upload(
      await screen.findByLabelText('Importar PDF o DOCX'),
      new File(['docx'], 'manual.docx', {
        type: 'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
      }),
    )

    expect(await screen.findByRole('textbox', { name: 'Contenido del documento' })).toHaveTextContent('Contenido importado')
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
      jsonResponse(200, []),
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
      jsonResponse(200, []),
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
      jsonResponse(200, []),
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
  test('keeps functional audit separate from feedback review', async () => {
    stubFetch([
      jsonResponse(200, usersResponse),
      jsonResponse(200, [{ id: '22222222-2222-2222-2222-222222222222', name: 'Operaciones' }]),
      jsonResponse(200, auditEventsResponse),
    ])
    const user = userEvent.setup()

    render(<App />)

    await user.click(await screen.findByRole('link', { name: 'Auditoria' }))

    expect(await screen.findByRole('heading', { name: 'Auditoria' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Eventos funcionales' })).toBeInTheDocument()
    const auditRow = await screen.findByRole('row', { name: /Documento creado/i })
    expect(within(auditRow).getByText('Ana Gomez')).toBeInTheDocument()
    expect(within(auditRow).getByText('req-doc-create')).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Feedback auditado' })).not.toBeInTheDocument()
    expect(screen.queryByRole('combobox', { name: 'Polaridad' })).not.toBeInTheDocument()
  })

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

    expect(await screen.findByRole('heading', { name: 'Feedback auditado' })).toBeInTheDocument()
    expect(await screen.findByText('Todavia no hay feedback registrado.')).toBeInTheDocument()
    await user.selectOptions(screen.getByRole('combobox', { name: 'Polaridad' }), 'negative')
    await user.click(screen.getByRole('button', { name: 'Aplicar filtros' }))

    expect(await screen.findByText('Que regla aplica?')).toBeInTheDocument()
    expect(screen.getByText('No sirvio')).toBeInTheDocument()
    expect(screen.getByText('Falto detalle')).toBeInTheDocument()
    expect(screen.getByText('req-report')).toBeInTheDocument()
  })
})

interface StubFetchOptions {
  setupRequired?: boolean
  session?: typeof sessionUser | null
}

function stubFetch(responses: Response[], options: StubFetchOptions = {}) {
  const setupRequired = options.setupRequired ?? false
  const session = options.session === undefined ? sessionUser : options.session
  const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
    const path = typeof input === 'string' ? input : input instanceof URL ? input.pathname : input.url
    if (path === '/api/setup/status') {
      return jsonResponse(200, {
        setupRequired,
        adminExists: !setupRequired,
        databaseReady: true,
        requiredRoles: ['Admin', 'DocumentManager', 'Viewer'],
      })
    }

    if (path === '/api/session') {
      return session
        ? jsonResponse(200, { user: session })
        : jsonResponse(401, {
            error: {
              code: 'AUTH_REQUIRED',
              message: 'Authentication required.',
              details: null,
              requestId: 'session-request',
            },
          })
    }

    const response = responses.shift()
    if (!response) {
      throw new Error(`Unexpected fetch call to ${path}.`)
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

function expectActionTooltip(button: HTMLElement) {
  const label = button.getAttribute('aria-label')

  expect(label).toBeTruthy()
  expect(button).not.toHaveAttribute('title')
  expect(button).toHaveAttribute('data-tooltip', label!)
}
