import { useEffect, useMemo, useState } from 'react'
import { Pencil, RefreshCw, Search } from 'lucide-react'
import { ApiError } from '../../lib/api-error'
import { listGroups, listUsers, updateUserBudget } from '../../api/users'
import type { GroupSummary, UserSummary } from '../../api/users'
import { Button } from '../../components/ui/button'

type LoadState = 'loading' | 'ready' | 'error'
type UserStatusFilter = 'all' | 'active' | 'inactive'

export function UsersBudgetPage() {
  const [users, setUsers] = useState<UserSummary[]>([])
  const [groups, setGroups] = useState<GroupSummary[]>([])
  const [loadState, setLoadState] = useState<LoadState>('loading')
  const [editingUser, setEditingUser] = useState<UserSummary | null>(null)
  const [successMessage, setSuccessMessage] = useState<string | null>(null)
  const [searchQuery, setSearchQuery] = useState('')
  const [statusFilter, setStatusFilter] = useState<UserStatusFilter>('all')

  useEffect(() => {
    void loadUsers()
  }, [])

  async function loadUsers() {
    setLoadState('loading')
    try {
      const [loadedUsers, loadedGroups] = await Promise.all([
        listUsers(),
        listGroups(),
      ])

      setUsers(loadedUsers)
      setGroups(loadedGroups)
      setLoadState('ready')
    } catch {
      setLoadState('error')
    }
  }

  const activeUsers = useMemo(
    () => users.filter((user) => user.isActive).length,
    [users],
  )

  const filteredUsers = useMemo(() => {
    const normalizedQuery = searchQuery.trim().toLowerCase()

    return users.filter((user) => {
      const matchesStatus =
        statusFilter === 'all' ||
        (statusFilter === 'active' ? user.isActive : !user.isActive)

      if (!matchesStatus) {
        return false
      }

      if (normalizedQuery.length === 0) {
        return true
      }

      const searchableText = [
        user.displayName,
        user.email,
        ...user.roles,
        ...user.groups.map((group) => group.name),
      ]
        .join(' ')
        .toLowerCase()

      return searchableText.includes(normalizedQuery)
    })
  }, [searchQuery, statusFilter, users])

  return (
    <>

      <section className="workspace" id="usuarios">
        <header className="workspace-header">
          <div>
            <p className="eyebrow">Administración</p>
            <h1>Usuarios y presupuestos</h1>
          </div>
          <Button
            className="icon-button"
            type="button"
            aria-label="Actualizar usuarios"
            disabled={loadState === 'loading'}
            onClick={() => void loadUsers()}
          >
            <RefreshCw size={18} />
          </Button>
        </header>

        <section className="metrics-row" aria-label="Resumen de usuarios">
          <div>
            <span className="metric-label">Usuarios activos</span>
            <strong>{activeUsers}</strong>
          </div>
          <div>
            <span className="metric-label">Grupos disponibles</span>
            <strong>{groups.length}</strong>
          </div>
          <div>
            <span className="metric-label">Presupuesto base</span>
            <strong>USD 5.00</strong>
          </div>
        </section>

        {loadState === 'ready' && users.length > 0 ? (
          <section className="filter-bar" aria-label="Filtros de usuarios">
            <label className="field filter-search">
              <span>Buscar usuarios</span>
              <span className="search-control">
                <Search size={16} />
                <input
                  type="search"
                  placeholder="Nombre, email, rol o grupo"
                  value={searchQuery}
                  onChange={(event) => setSearchQuery(event.target.value)}
                />
              </span>
            </label>

            <label className="field filter-status">
              <span>Estado</span>
              <select
                value={statusFilter}
                onChange={(event) => setStatusFilter(event.target.value as UserStatusFilter)}
              >
                <option value="all">Todos</option>
                <option value="active">Activos</option>
                <option value="inactive">Inactivos</option>
              </select>
            </label>
          </section>
        ) : null}

        {successMessage ? (
          <p className="status-message success" role="status">
            {successMessage}
          </p>
        ) : null}

        {loadState === 'loading' ? (
          <p className="status-message">Cargando usuarios...</p>
        ) : null}

        {loadState === 'error' ? (
          <p className="status-message error" role="alert">
            No se pudo cargar la información de usuarios.
          </p>
        ) : null}

        {loadState === 'ready' && users.length === 0 ? (
          <p className="status-message">No hay usuarios para mostrar.</p>
        ) : null}

        {loadState === 'ready' && users.length > 0 && filteredUsers.length === 0 ? (
          <p className="status-message">No hay usuarios que coincidan con los filtros.</p>
        ) : null}

        {loadState === 'ready' && filteredUsers.length > 0 ? (
          <div className="table-frame">
            <table>
              <thead>
                <tr>
                  <th scope="col">Usuario</th>
                  <th scope="col">Roles</th>
                  <th scope="col">Grupos</th>
                  <th scope="col">Estado</th>
                  <th scope="col">Presupuesto mensual</th>
                  <th scope="col">Gasto actual</th>
                  <th scope="col">Restante</th>
                  <th scope="col">Acciones</th>
                </tr>
              </thead>
              <tbody>
                {filteredUsers.map((user) => (
                  <tr key={user.id}>
                    <th scope="row">
                      <span className="user-name">{user.displayName}</span>
                      <span className="user-email">{user.email}</span>
                    </th>
                    <td>{joinOrDash(user.roles)}</td>
                    <td>{joinOrDash(user.groups.map((group) => group.name))}</td>
                    <td>
                      <span className={user.isActive ? 'badge active' : 'badge inactive'}>
                        {user.isActive ? 'Activo' : 'Inactivo'}
                      </span>
                    </td>
                    <td>{formatBudget(user.monthlyBudgetUsd, user.isBudgetDisabled)}</td>
                    <td>{formatCurrency(user.currentSpendUsd)}</td>
                    <td>{formatNullableCurrency(user.remainingBudgetUsd)}</td>
                    <td>
                      <Button
                        className="icon-button"
                        type="button"
                        aria-label={`Editar presupuesto de ${user.displayName}`}
                        onClick={() => {
                          setEditingUser(user)
                          setSuccessMessage(null)
                        }}
                      >
                        <Pencil size={16} />
                      </Button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : null}
      </section>

      {editingUser ? (
        <BudgetDialog
          user={editingUser}
          onClose={() => setEditingUser(null)}
          onSaved={(updatedUser) => {
            setUsers((current) =>
              current.map((user) => (user.id === updatedUser.id ? updatedUser : user)),
            )
            setEditingUser(null)
            setSuccessMessage('Presupuesto actualizado.')
          }}
        />
      ) : null}
    </>
  )
}

function BudgetDialog({
  user,
  onClose,
  onSaved,
}: {
  user: UserSummary
  onClose: () => void
  onSaved: (user: UserSummary) => void
}) {
  const [monthlyBudget, setMonthlyBudget] = useState(
    user.monthlyBudgetUsd?.toString() ?? '',
  )
  const [isDisabled, setIsDisabled] = useState(user.isBudgetDisabled)
  const [validationError, setValidationError] = useState<string | null>(null)
  const [apiError, setApiError] = useState<string | null>(null)
  const [isSaving, setIsSaving] = useState(false)

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setValidationError(null)
    setApiError(null)

    const numericBudget = monthlyBudget.trim() === '' ? null : Number(monthlyBudget)
    if (!isDisabled && numericBudget !== null && numericBudget < 0) {
      setValidationError('El presupuesto no puede ser negativo.')
      return
    }

    setIsSaving(true)
    try {
      const updatedUser = await updateUserBudget(user.id, {
        monthlyBudgetUsd: isDisabled ? null : numericBudget,
        isDisabled,
      })
      onSaved(updatedUser)
    } catch (error) {
      const reference = error instanceof ApiError ? error.requestId : 'unknown'
      setApiError(`No se pudo actualizar el presupuesto. Referencia: ${reference}.`)
    } finally {
      setIsSaving(false)
    }
  }

  return (
    <div className="dialog-backdrop">
      <section
        aria-labelledby="budget-dialog-title"
        aria-modal="true"
        className="dialog"
        role="dialog"
      >
        <header className="dialog-header">
          <div>
            <p className="eyebrow">Presupuesto de IA</p>
            <h2 id="budget-dialog-title">Editar presupuesto</h2>
          </div>
          <Button className="text-button" type="button" onClick={onClose}>
            Cerrar
          </Button>
        </header>

        <form className="budget-form" noValidate onSubmit={handleSubmit}>
          <label className="field">
            <span>Presupuesto mensual (USD)</span>
            <input
              min="0"
              step="0.01"
              type="number"
              value={monthlyBudget}
              onChange={(event) => setMonthlyBudget(event.target.value)}
              disabled={isDisabled || isSaving}
            />
          </label>

          <label className="checkbox-field">
            <input
              type="checkbox"
              checked={isDisabled}
              onChange={(event) => setIsDisabled(event.target.checked)}
              disabled={isSaving}
            />
            <span>Sin límite mensual</span>
          </label>

          {validationError ? (
            <p className="status-message error" role="alert">
              {validationError}
            </p>
          ) : null}

          {apiError ? (
            <p className="status-message error" role="alert">
              {apiError}
            </p>
          ) : null}

          <div className="dialog-actions">
            <Button className="text-button" type="button" onClick={onClose}>
              Cancelar
            </Button>
            <Button className="primary-button" type="submit" disabled={isSaving}>
              {isSaving ? 'Guardando...' : 'Guardar'}
            </Button>
          </div>
        </form>
      </section>
    </div>
  )
}

function joinOrDash(values: string[]) {
  return values.length > 0 ? values.join(', ') : '-'
}

function formatBudget(value: number | null, isDisabled: boolean) {
  return isDisabled ? 'Sin límite' : formatNullableCurrency(value)
}

function formatNullableCurrency(value: number | null) {
  return value === null ? '-' : formatCurrency(value)
}

function formatCurrency(value: number) {
  return `USD ${value.toFixed(2)}`
}
