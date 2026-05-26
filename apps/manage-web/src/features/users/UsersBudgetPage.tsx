import { useEffect, useMemo, useState } from 'react'
import {
  FolderPlus,
  Pencil,
  RefreshCw,
  Search,
  UserCheck,
  UserCog,
  UserPlus,
  UserX,
  WalletCards,
} from 'lucide-react'
import { ApiError } from '../../lib/api-error'
import {
  createGroup,
  createUser,
  listGroups,
  listUsers,
  updateGroup,
  updateUserBudget,
  updateUserGroups,
  updateUserRoles,
  updateUserStatus,
} from '../../api/users'
import type { GroupSummary, UserSummary } from '../../api/users'
import { Button } from '../../components/ui/button'
import { Checkbox, DataTable, Dialog, Input } from '@helpcenter/shared-ui'

type LoadState = 'loading' | 'ready' | 'error'
type UserStatusFilter = 'all' | 'active' | 'inactive'

export function UsersBudgetPage() {
  const [users, setUsers] = useState<UserSummary[]>([])
  const [groups, setGroups] = useState<GroupSummary[]>([])
  const [loadState, setLoadState] = useState<LoadState>('loading')
  const [editingUser, setEditingUser] = useState<UserSummary | null>(null)
  const [managingUser, setManagingUser] = useState<UserSummary | null>(null)
  const [editingGroup, setEditingGroup] = useState<GroupSummary | null>(null)
  const [isCreatingGroup, setIsCreatingGroup] = useState(false)
  const [isCreatingUser, setIsCreatingUser] = useState(false)
  const [successMessage, setSuccessMessage] = useState<string | null>(null)
  const [errorMessage, setErrorMessage] = useState<string | null>(null)
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

  async function setActiveStatus(user: UserSummary, isActive: boolean) {
    setSuccessMessage(null)
    setErrorMessage(null)

    try {
      const updatedUser = await updateUserStatus(user.id, { isActive })
      setUsers((current) =>
        current.map((currentUser) =>
          currentUser.id === updatedUser.id ? updatedUser : currentUser,
        ),
      )
      setSuccessMessage(isActive ? 'Usuario reactivado.' : 'Usuario dado de baja.')
    } catch (error) {
      const reference = error instanceof ApiError ? error.requestId : 'unknown'
      setErrorMessage(
        isActive
          ? `No se pudo reactivar el usuario. Referencia: ${reference}.`
          : `No se pudo dar de baja el usuario. Referencia: ${reference}.`,
      )
    }
  }

  return (
    <>

      <section className="workspace" id="usuarios">
        <header className="workspace-header">
          <div>
            <p className="eyebrow">Administración</p>
            <h1>Usuarios y grupos</h1>
          </div>
          <div className="workspace-actions">
            {!isCreatingGroup && !isCreatingUser ? (
              <>
                <Button
                  className="text-button"
                  type="button"
                  onClick={() => {
                    setIsCreatingGroup(true)
                    setSuccessMessage(null)
                    setErrorMessage(null)
                  }}
                >
                  <FolderPlus size={16} />
                  Crear grupo
                </Button>
                <Button
                  className="primary-button"
                  type="button"
                  onClick={() => {
                    setIsCreatingUser(true)
                    setSuccessMessage(null)
                    setErrorMessage(null)
                  }}
                >
                  <UserPlus size={16} />
                  Crear usuario
                </Button>
              </>
            ) : null}
            <Button
              className="icon-button"
              type="button"
              aria-label="Actualizar usuarios"
              disabled={loadState === 'loading'}
              onClick={() => void loadUsers()}
            >
              <RefreshCw size={18} />
            </Button>
          </div>
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

        {errorMessage ? (
          <p className="status-message error" role="alert">
            {errorMessage}
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
          <DataTable<UserSummary>
            className="table-frame users-table"
            columns={[
              {
                key: 'user',
                header: 'Usuario',
                rowHeader: true,
                render: (user) => (
                  <>
                      <span className="user-name">{user.displayName}</span>
                      <span className="user-email">{user.email}</span>
                  </>
                ),
              },
              { key: 'roles', header: 'Roles', render: (user) => joinOrDash(user.roles) },
              {
                key: 'groups',
                header: 'Grupos',
                render: (user) => joinOrDash(user.groups.map((group) => group.name)),
              },
              {
                key: 'status',
                header: 'Estado',
                render: (user) => (
                      <span className={user.isActive ? 'badge active' : 'badge inactive'}>
                        {user.isActive ? 'Activo' : 'Inactivo'}
                      </span>
                ),
              },
              {
                key: 'monthlyBudget',
                header: 'Presupuesto mensual',
                render: (user) => formatBudget(user.monthlyBudgetUsd, user.isBudgetDisabled),
              },
              {
                key: 'currentSpend',
                header: 'Gasto actual',
                render: (user) => formatCurrency(user.currentSpendUsd),
              },
              {
                key: 'remaining',
                header: 'Restante',
                render: (user) => formatNullableCurrency(user.remainingBudgetUsd),
              },
              {
                key: 'actions',
                header: 'Acciones',
                render: (user) => (
                      <div className="row-actions">
                        <Button
                          className="icon-button"
                          type="button"
                          aria-label={`Editar usuario ${user.displayName}`}
                          onClick={() => {
                            setManagingUser(user)
                            setSuccessMessage(null)
                            setErrorMessage(null)
                          }}
                        >
                          <UserCog size={16} />
                        </Button>
                        <Button
                          className="icon-button"
                          type="button"
                          aria-label={`Editar presupuesto de ${user.displayName}`}
                          onClick={() => {
                            setEditingUser(user)
                            setSuccessMessage(null)
                            setErrorMessage(null)
                          }}
                        >
                          <WalletCards size={16} />
                        </Button>
                        <Button
                          className="icon-button"
                          type="button"
                          aria-label={
                            user.isActive
                              ? `Dar de baja a ${user.displayName}`
                              : `Reactivar a ${user.displayName}`
                          }
                          onClick={() => void setActiveStatus(user, !user.isActive)}
                        >
                          {user.isActive ? <UserX size={16} /> : <UserCheck size={16} />}
                        </Button>
                      </div>
                ),
              },
            ]}
            data={filteredUsers}
            getRowId={(user) => user.id}
          />
        ) : null}
      </section>

      {loadState === 'ready' ? (
        <section className="workspace compact-workspace" id="grupos">
          <header className="workspace-header">
            <div>
              <p className="eyebrow">Acceso documental</p>
              <h2>Grupos</h2>
            </div>
          </header>

          {groups.length === 0 ? (
            <p className="status-message">No hay grupos para mostrar.</p>
          ) : (
            <DataTable<GroupSummary>
              className="table-frame groups-table-frame groups-table"
              columns={[
                {
                  key: 'group',
                  header: 'Grupo',
                  rowHeader: true,
                  render: (group) => group.name,
                },
                {
                  key: 'users',
                  header: 'Usuarios',
                  render: (group) =>
                    users.filter((user) => user.groups.some((item) => item.id === group.id)).length,
                },
                {
                  key: 'actions',
                  header: 'Acciones',
                  render: (group) => (
                        <Button
                          className="icon-button"
                          type="button"
                          aria-label={`Editar grupo ${group.name}`}
                          onClick={() => {
                            setEditingGroup(group)
                            setSuccessMessage(null)
                            setErrorMessage(null)
                          }}
                        >
                          <Pencil size={16} />
                        </Button>
                  ),
                },
              ]}
              data={groups}
              getRowId={(group) => group.id}
            />
          )}
        </section>
      ) : null}

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

      {managingUser ? (
        <UserManagementDialog
          user={managingUser}
          groups={groups}
          onClose={() => setManagingUser(null)}
          onSaved={(updatedUser) => {
            setUsers((current) =>
              current.map((user) => (user.id === updatedUser.id ? updatedUser : user)),
            )
            setManagingUser(null)
            setSuccessMessage('Usuario actualizado.')
          }}
        />
      ) : null}

      {editingGroup ? (
        <GroupEditDialog
          group={editingGroup}
          onClose={() => setEditingGroup(null)}
          onSaved={(group) => {
            setGroups((current) =>
              current.map((item) => (item.id === group.id ? group : item)),
            )
            setUsers((current) =>
              current.map((user) => ({
                ...user,
                groups: user.groups.map((item) => (item.id === group.id ? group : item)),
              })),
            )
            setEditingGroup(null)
            setSuccessMessage('Grupo actualizado.')
          }}
        />
      ) : null}

      {isCreatingGroup ? (
        <GroupDialog
          onClose={() => setIsCreatingGroup(false)}
          onSaved={(group) => {
            setGroups((current) => [...current, group])
            setIsCreatingGroup(false)
            setSuccessMessage('Grupo creado.')
          }}
        />
      ) : null}

      {isCreatingUser ? (
        <UserDialog
          groups={groups}
          onClose={() => setIsCreatingUser(false)}
          onSaved={(user) => {
            setUsers((current) => [...current, user])
            setIsCreatingUser(false)
            setSuccessMessage('Usuario creado.')
          }}
        />
      ) : null}
    </>
  )
}

function GroupEditDialog({
  group,
  onClose,
  onSaved,
}: {
  group: GroupSummary
  onClose: () => void
  onSaved: (group: GroupSummary) => void
}) {
  const [name, setName] = useState(group.name)
  const [validationError, setValidationError] = useState<string | null>(null)
  const [apiError, setApiError] = useState<string | null>(null)
  const [isSaving, setIsSaving] = useState(false)

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setValidationError(null)
    setApiError(null)

    const trimmedName = name.trim()
    if (trimmedName.length === 0) {
      setValidationError('El nombre del grupo es obligatorio.')
      return
    }

    setIsSaving(true)
    try {
      onSaved(await updateGroup(group.id, { name: trimmedName }))
    } catch (error) {
      const reference = error instanceof ApiError ? error.requestId : 'unknown'
      setApiError(`No se pudo actualizar el grupo. Referencia: ${reference}.`)
    } finally {
      setIsSaving(false)
    }
  }

  return (
    <Dialog
      open
      title="Editar grupo"
      description="Actualizá el nombre visible del grupo usado para permisos documentales."
      onOpenChange={(open) => {
        if (!open) {
          onClose()
        }
      }}
    >
        <form className="dialog-form" noValidate onSubmit={handleSubmit}>
          <label className="field">
            <span>Nombre del grupo</span>
            <Input
              type="text"
              value={name}
              onChange={(event) => setName(event.target.value)}
              disabled={isSaving}
            />
          </label>

          {validationError ? <p className="status-message error" role="alert">{validationError}</p> : null}
          {apiError ? <p className="status-message error" role="alert">{apiError}</p> : null}

          <div className="dialog-actions">
            <Button className="text-button" type="button" onClick={onClose}>
              Cancelar
            </Button>
            <Button className="primary-button" type="submit" disabled={isSaving}>
              {isSaving ? 'Guardando...' : 'Guardar grupo'}
            </Button>
          </div>
        </form>
    </Dialog>
  )
}

function GroupDialog({
  onClose,
  onSaved,
}: {
  onClose: () => void
  onSaved: (group: GroupSummary) => void
}) {
  const [name, setName] = useState('')
  const [validationError, setValidationError] = useState<string | null>(null)
  const [apiError, setApiError] = useState<string | null>(null)
  const [isSaving, setIsSaving] = useState(false)

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setValidationError(null)
    setApiError(null)

    const trimmedName = name.trim()
    if (trimmedName.length === 0) {
      setValidationError('El nombre del grupo es obligatorio.')
      return
    }

    setIsSaving(true)
    try {
      const group = await createGroup({ name: trimmedName })
      onSaved(group)
    } catch (error) {
      const reference = error instanceof ApiError ? error.requestId : 'unknown'
      setApiError(`No se pudo crear el grupo. Referencia: ${reference}.`)
    } finally {
      setIsSaving(false)
    }
  }

  return (
    <Dialog
      open
      title="Crear grupo"
      description="Creá un grupo para asignar acceso a usuarios y documentos."
      onOpenChange={(open) => {
        if (!open) {
          onClose()
        }
      }}
    >
        <form className="dialog-form" noValidate onSubmit={handleSubmit}>
          <label className="field">
            <span>Nombre del grupo</span>
            <Input
              type="text"
              value={name}
              onChange={(event) => setName(event.target.value)}
              disabled={isSaving}
            />
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
              {isSaving ? 'Guardando...' : 'Guardar grupo'}
            </Button>
          </div>
        </form>
    </Dialog>
  )
}

function UserDialog({
  groups,
  onClose,
  onSaved,
}: {
  groups: GroupSummary[]
  onClose: () => void
  onSaved: (user: UserSummary) => void
}) {
  const [email, setEmail] = useState('')
  const [displayName, setDisplayName] = useState('')
  const [password, setPassword] = useState('')
  const [role, setRole] = useState('Viewer')
  const [selectedGroupIds, setSelectedGroupIds] = useState<string[]>([])
  const [validationError, setValidationError] = useState<string | null>(null)
  const [apiError, setApiError] = useState<string | null>(null)
  const [isSaving, setIsSaving] = useState(false)

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setValidationError(null)
    setApiError(null)

    const trimmedEmail = email.trim()
    const trimmedDisplayName = displayName.trim()
    if (
      trimmedEmail.length === 0 ||
      trimmedDisplayName.length === 0 ||
      password.length === 0
    ) {
      setValidationError('Completá email, nombre visible y contraseña temporal.')
      return
    }

    setIsSaving(true)
    try {
      const user = await createUser({
        email: trimmedEmail,
        displayName: trimmedDisplayName,
        password,
        roles: [role],
        groupIds: selectedGroupIds,
      })
      onSaved(user)
    } catch (error) {
      const reference = error instanceof ApiError ? error.requestId : 'unknown'
      setApiError(`No se pudo crear el usuario. Referencia: ${reference}.`)
    } finally {
      setIsSaving(false)
    }
  }

  function toggleGroup(groupId: string) {
    setSelectedGroupIds((current) =>
      current.includes(groupId)
        ? current.filter((selectedId) => selectedId !== groupId)
        : [...current, groupId],
    )
  }

  return (
    <Dialog
      open
      className="user-dialog"
      title="Crear usuario"
      description="Creá una cuenta local y asignale rol y grupos de acceso."
      onOpenChange={(open) => {
        if (!open) {
          onClose()
        }
      }}
    >
        <form className="dialog-form" noValidate onSubmit={handleSubmit}>
          <div className="dialog-grid">
            <label className="field">
              <span>Email</span>
              <Input
                type="email"
                value={email}
                onChange={(event) => setEmail(event.target.value)}
                disabled={isSaving}
              />
            </label>

            <label className="field">
              <span>Nombre visible</span>
              <Input
                type="text"
                value={displayName}
                onChange={(event) => setDisplayName(event.target.value)}
                disabled={isSaving}
              />
            </label>

            <label className="field">
              <span>Contraseña temporal</span>
              <Input
                type="password"
                value={password}
                onChange={(event) => setPassword(event.target.value)}
                disabled={isSaving}
              />
            </label>

            <label className="field">
              <span>Rol</span>
              <select
                value={role}
                onChange={(event) => setRole(event.target.value)}
                disabled={isSaving}
              >
                <option value="Viewer">Viewer</option>
                <option value="DocumentManager">DocumentManager</option>
                <option value="Admin">Admin</option>
              </select>
            </label>
          </div>

          <fieldset className="checkbox-list" disabled={isSaving}>
            <legend>Grupos</legend>
            {groups.length > 0 ? (
              groups.map((group) => (
                <Checkbox
                  className="checkbox-field"
                  key={group.id}
                  label={group.name}
                  checked={selectedGroupIds.includes(group.id)}
                  onCheckedChange={() => toggleGroup(group.id)}
                  disabled={isSaving}
                />
              ))
            ) : (
              <p className="muted-copy">No hay grupos disponibles.</p>
            )}
          </fieldset>

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
              {isSaving ? 'Creando...' : 'Crear usuario'}
            </Button>
          </div>
        </form>
    </Dialog>
  )
}

function UserManagementDialog({
  user,
  groups,
  onClose,
  onSaved,
}: {
  user: UserSummary
  groups: GroupSummary[]
  onClose: () => void
  onSaved: (user: UserSummary) => void
}) {
  const [role, setRole] = useState(primaryRole(user.roles))
  const [selectedGroupIds, setSelectedGroupIds] = useState<string[]>(
    user.groups.map((group) => group.id),
  )
  const [apiError, setApiError] = useState<string | null>(null)
  const [isSaving, setIsSaving] = useState(false)

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setApiError(null)
    setIsSaving(true)

    try {
      let updatedUser = user
      if (role !== primaryRole(user.roles)) {
        updatedUser = await updateUserRoles(user.id, { roles: [role] })
      }

      const originalGroupIds = user.groups.map((group) => group.id).sort().join(',')
      const nextGroupIds = [...selectedGroupIds].sort().join(',')
      if (nextGroupIds !== originalGroupIds) {
        updatedUser = await updateUserGroups(user.id, { groupIds: selectedGroupIds })
      }

      onSaved(updatedUser)
    } catch (error) {
      const reference = error instanceof ApiError ? error.requestId : 'unknown'
      setApiError(`No se pudo actualizar el usuario. Referencia: ${reference}.`)
    } finally {
      setIsSaving(false)
    }
  }

  function toggleGroup(groupId: string) {
    setSelectedGroupIds((current) =>
      current.includes(groupId)
        ? current.filter((selectedId) => selectedId !== groupId)
        : [...current, groupId],
    )
  }

  return (
    <Dialog
      open
      className="user-dialog"
      title="Editar usuario"
      description="Ajustá el rol primario y los grupos de acceso del usuario."
      onOpenChange={(open) => {
        if (!open) {
          onClose()
        }
      }}
    >
        <form className="dialog-form" noValidate onSubmit={handleSubmit}>
          <div className="readonly-summary">
            <strong>{user.displayName}</strong>
            <span>{user.email}</span>
          </div>

          <label className="field">
            <span>Rol</span>
            <select
              value={role}
              onChange={(event) => setRole(event.target.value)}
              disabled={isSaving}
            >
              <option value="Viewer">Viewer</option>
              <option value="DocumentManager">DocumentManager</option>
              <option value="Admin">Admin</option>
            </select>
          </label>

          <fieldset className="checkbox-list" disabled={isSaving}>
            <legend>Grupos</legend>
            {groups.length > 0 ? (
              groups.map((group) => (
                <Checkbox
                  className="checkbox-field"
                  key={group.id}
                  label={group.name}
                  checked={selectedGroupIds.includes(group.id)}
                  onCheckedChange={() => toggleGroup(group.id)}
                  disabled={isSaving}
                />
              ))
            ) : (
              <p className="muted-copy">No hay grupos disponibles.</p>
            )}
          </fieldset>

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
              {isSaving ? 'Guardando...' : 'Guardar usuario'}
            </Button>
          </div>
        </form>
    </Dialog>
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
    <Dialog
      open
      title="Editar presupuesto"
      description="Ajustá el límite mensual o deshabilitá el control de gasto para este usuario."
      onOpenChange={(open) => {
        if (!open) {
          onClose()
        }
      }}
    >
        <form className="budget-form" noValidate onSubmit={handleSubmit}>
          <label className="field">
            <span>Presupuesto mensual (USD)</span>
            <Input
              min="0"
              step="0.01"
              type="number"
              value={monthlyBudget}
              onChange={(event) => setMonthlyBudget(event.target.value)}
              disabled={isDisabled || isSaving}
            />
          </label>

          <Checkbox
            className="checkbox-field"
            label="Sin límite mensual"
            checked={isDisabled}
            onCheckedChange={setIsDisabled}
            disabled={isSaving}
          />

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
    </Dialog>
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

function primaryRole(roles: string[]) {
  if (roles.includes('Admin')) {
    return 'Admin'
  }

  if (roles.includes('DocumentManager')) {
    return 'DocumentManager'
  }

  return roles.includes('Viewer') ? 'Viewer' : (roles[0] ?? 'Viewer')
}
