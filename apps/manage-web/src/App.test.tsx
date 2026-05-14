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

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('management users and budgets', () => {
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
