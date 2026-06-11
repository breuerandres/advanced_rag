import { useEffect, useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
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
  updateUserOrganizationalUnit,
  updateUserRoles,
  updateUserStatus,
} from '../../api/users'
import type { GroupSummary, UserSummary } from '../../api/users'
import { listOrganizationalUnits } from '../../api/orgUnits'
import type { OrganizationalUnitSummary } from '../../api/orgUnits'
import { flattenUnitsInTreeOrder } from '../orgUnits/unitTree'
import { UnitLevelBadge } from '../orgUnits/UnitLevelBadge'
import { Button, Checkbox, DataTable, Dialog, Input, Select } from '@helpcenter/shared-ui'
import type { SelectOption } from '@helpcenter/shared-ui'

type LoadState = 'loading' | 'ready' | 'error'
type UserStatusFilter = 'all' | 'active' | 'inactive'
type UsersWorkspaceTab = 'users' | 'groups'

interface UsersBudgetPageProps {
  userRoles: string[]
}

export function UsersBudgetPage({ userRoles }: UsersBudgetPageProps) {
  const { t } = useTranslation()
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
  const [activeTab, setActiveTab] = useState<UsersWorkspaceTab>('users')
  const isAdmin = userRoles.includes('Admin')
  const canManageGroups =
    isAdmin ||
    userRoles.includes('DocumentEditor') ||
    userRoles.includes('DocumentPublisher')

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
      setSuccessMessage(isActive ? t('users.status_reactivated') : t('users.status_deactivated'))
    } catch (error) {
      const reference = error instanceof ApiError ? error.requestId : 'unknown'
      setErrorMessage(
        isActive
          ? t('users.error_reactivate', { reference })
          : t('users.error_deactivate', { reference }),
      )
    }
  }

  return (
    <>

      <section className="workspace" id="usuarios">
        <header className="workspace-header">
          <div>
            <p className="eyebrow">{t('users.eyebrow')}</p>
            <h1>{t('users.title')}</h1>
          </div>
          <div className="workspace-actions">
            {!isCreatingGroup && !isCreatingUser ? (
              <>
                {activeTab === 'groups' && canManageGroups ? (
                  <Button
                    className="primary-button"
                    type="button"
                    onClick={() => {
                      setIsCreatingGroup(true)
                      setSuccessMessage(null)
                      setErrorMessage(null)
                    }}
                  >
                    <FolderPlus size={16} />
                    {t('users.create_group')}
                  </Button>
                ) : activeTab === 'users' && isAdmin ? (
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
                    {t('users.create_user')}
                  </Button>
                ) : null}
              </>
            ) : null}
            <Button
              className="icon-button"
              type="button"
              aria-label={t('users.refresh')}
              disabled={loadState === 'loading'}
              onClick={() => void loadUsers()}
            >
              <RefreshCw size={18} />
            </Button>
          </div>
        </header>

        <div className="workspace-tabs" role="tablist" aria-label={t('users.title')}>
          <button
            className="workspace-tab"
            id="users-tab"
            type="button"
            role="tab"
            aria-selected={activeTab === 'users'}
            aria-controls="users-panel"
            onClick={() => setActiveTab('users')}
          >
            {t('users.users_tab')}
          </button>
          <button
            className="workspace-tab"
            id="groups-tab"
            type="button"
            role="tab"
            aria-selected={activeTab === 'groups'}
            aria-controls="groups-panel"
            onClick={() => setActiveTab('groups')}
          >
            {t('users.groups_tab')}
          </button>
        </div>

        {activeTab === 'users' ? (
          <section
            className="tab-panel"
            id="users-panel"
            role="tabpanel"
            aria-labelledby="users-tab"
          >
        <section className="metrics-row" aria-label={t('users.summary_label')}>
          <div>
            <span className="metric-label">{t('users.active_users')}</span>
            <strong>{activeUsers}</strong>
          </div>
          <div>
            <span className="metric-label">{t('users.available_groups')}</span>
            <strong>{groups.length}</strong>
          </div>
          <div>
            <span className="metric-label">{t('users.base_budget')}</span>
            <strong>USD 5.00</strong>
          </div>
        </section>

        {loadState === 'ready' && users.length > 0 ? (
          <section className="filter-bar" aria-label={t('users.filters_label')}>
            <label className="field filter-search">
              <span>{t('users.search_users')}</span>
              <span className="search-control">
                <Search size={16} />
                <Input
                  type="search"
                  placeholder={t('users.search_placeholder')}
                  value={searchQuery}
                  onChange={(event) => setSearchQuery(event.target.value)}
                />
              </span>
            </label>

            <label className="field filter-status">
              <span>{t('users.status')}</span>
              <select
                value={statusFilter}
                onChange={(event) => setStatusFilter(event.target.value as UserStatusFilter)}
              >
                <option value="all">{t('users.status_all')}</option>
                <option value="active">{t('users.status_active_plural')}</option>
                <option value="inactive">{t('users.status_inactive_plural')}</option>
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
          <p className="status-message">{t('users.loading_users')}</p>
        ) : null}

        {loadState === 'error' ? (
          <p className="status-message error" role="alert">
            {t('users.load_error')}
          </p>
        ) : null}

        {loadState === 'ready' && users.length === 0 ? (
          <p className="status-message">{t('users.empty_users')}</p>
        ) : null}

        {loadState === 'ready' && users.length > 0 && filteredUsers.length === 0 ? (
          <p className="status-message">{t('users.empty_users_filtered')}</p>
        ) : null}

        {loadState === 'ready' && filteredUsers.length > 0 ? (
          <DataTable<UserSummary>
            className="table-frame users-table"
            columns={[
              {
                key: 'user',
                header: t('users.user_column'),
                rowHeader: true,
                render: (user) => (
                  <>
                      <span className="user-name">{user.displayName}</span>
                      <span className="user-email">{user.email}</span>
                  </>
                ),
              },
              { key: 'roles', header: t('users.roles_column'), render: (user) => joinOrDash(user.roles) },
              {
                key: 'groups',
                header: t('users.groups_column'),
                render: (user) => joinOrDash(user.groups.map((group) => group.name)),
              },
              {
                key: 'organizationalUnit',
                header: t('users.organizational_unit_column'),
                render: (user) => user.organizationalUnit?.name ?? '-',
              },
              {
                key: 'status',
                header: t('users.status_column'),
                render: (user) => (
                      <span className={user.isActive ? 'badge active' : 'badge inactive'}>
                        {user.isActive ? t('users.status_active') : t('users.status_inactive')}
                      </span>
                ),
              },
              {
                key: 'monthlyBudget',
                header: t('users.monthly_budget_column'),
                render: (user) => formatBudget(user.monthlyBudgetUsd, user.isBudgetDisabled),
              },
              {
                key: 'currentSpend',
                header: t('users.current_spend_column'),
                render: (user) => formatCurrency(user.currentSpendUsd),
              },
              {
                key: 'remaining',
                header: t('users.remaining_column'),
                render: (user) => formatNullableCurrency(user.remainingBudgetUsd),
              },
              {
                key: 'actions',
                header: t('users.actions_column'),
                render: (user) => (
                      <div className="row-actions">
                        {canManageGroups ? (
                          <Button
                            className="icon-button"
                            type="button"
                            aria-label={t('users.edit_user_for', { name: user.displayName })}
                            onClick={() => {
                              setManagingUser(user)
                              setSuccessMessage(null)
                              setErrorMessage(null)
                            }}
                          >
                            <UserCog size={16} />
                          </Button>
                        ) : null}
                        {isAdmin ? (
                          <>
                            <Button
                              className="icon-button"
                              type="button"
                              aria-label={t('users.edit_budget_for', { name: user.displayName })}
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
                                  ? t('users.deactivate_user_for', { name: user.displayName })
                                  : t('users.reactivate_user_for', { name: user.displayName })
                              }
                              onClick={() => void setActiveStatus(user, !user.isActive)}
                            >
                              {user.isActive ? <UserX size={16} /> : <UserCheck size={16} />}
                            </Button>
                          </>
                        ) : null}
                      </div>
                ),
              },
            ]}
            data={filteredUsers}
            getRowId={(user) => user.id}
          />
        ) : null}
          </section>
        ) : null}

        {activeTab === 'groups' ? (
          <section
            className="tab-panel"
            id="groups-panel"
            role="tabpanel"
            aria-labelledby="groups-tab"
          >
          <header className="workspace-header tab-panel-header">
            <div>
              <p className="eyebrow">Acceso documental</p>
              <h2>{t('users.groups_tab')}</h2>
            </div>
          </header>

          {loadState === 'loading' ? (
            <p className="status-message">{t('users.loading_groups')}</p>
          ) : null}

          {loadState === 'error' ? (
            <p className="status-message error" role="alert">
              {t('users.groups_load_error')}
            </p>
          ) : null}

          {loadState === 'ready' && groups.length === 0 ? (
            <p className="status-message">{t('users.empty_groups')}</p>
          ) : null}

          {loadState === 'ready' && groups.length > 0 ? (
            <DataTable<GroupSummary>
              className="table-frame groups-table-frame groups-table"
              columns={[
                {
                  key: 'group',
                  header: t('users.group_column'),
                  rowHeader: true,
                  render: (group) => group.name,
                },
                {
                  key: 'users',
                  header: t('users.users_column'),
                  render: (group) =>
                    users.filter((user) => user.groups.some((item) => item.id === group.id)).length,
                },
                {
                  key: 'ownerUnit',
                  header: t('users.owner_unit_column'),
                  render: (group) =>
                    group.ownerOrganizationalUnit?.name ?? t('users.no_owner_unit'),
                },
                {
                  key: 'publishingPolicy',
                  header: t('users.publishing_policy_column'),
                  render: (group) => group.publishingPolicy ?? '-',
                },
                {
                  key: 'actions',
                  header: t('users.actions_column'),
                  render: (group) => canManageGroups ? (
                        <Button
                          className="icon-button"
                          type="button"
                          aria-label={t('users.edit_group_for', { name: group.name })}
                          onClick={() => {
                            setEditingGroup(group)
                            setSuccessMessage(null)
                            setErrorMessage(null)
                          }}
                        >
                          <Pencil size={16} />
                        </Button>
                  ) : null,
                },
              ]}
              data={groups}
              getRowId={(group) => group.id}
            />
          ) : null}
        </section>
      ) : null}
      </section>

      {editingUser && isAdmin ? (
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

      {managingUser && canManageGroups ? (
        <UserManagementDialog
          user={managingUser}
          groups={groups}
          canEditRole={isAdmin}
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

      {editingGroup && canManageGroups ? (
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

      {isCreatingGroup && canManageGroups ? (
        <GroupDialog
          onClose={() => setIsCreatingGroup(false)}
          onSaved={(group) => {
            setGroups((current) => [...current, group])
            setIsCreatingGroup(false)
            setSuccessMessage('Grupo creado.')
          }}
        />
      ) : null}

      {isCreatingUser && isAdmin ? (
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
  const { t } = useTranslation()
  const [name, setName] = useState(group.name)
  const [ownerUnitId, setOwnerUnitId] = useState(group.ownerOrganizationalUnit?.id ?? '')
  const [publishingPolicy, setPublishingPolicy] = useState<string>(group.publishingPolicy ?? 'OwnerScope')
  const [organizationalUnits, setOrganizationalUnits] = useState<OrganizationalUnitSummary[]>([])
  const [validationError, setValidationError] = useState<string | null>(null)
  const [apiError, setApiError] = useState<string | null>(null)
  const [isSaving, setIsSaving] = useState(false)

  useEffect(() => {
    let active = true
    void (async () => {
      try {
        const loaded = await listOrganizationalUnits()
        if (active) {
          setOrganizationalUnits(loaded)
        }
      } catch {
        // Owner unit is optional; ignore load failure here.
      }
    })()
    return () => {
      active = false
    }
  }, [])

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
      onSaved(
        await updateGroup(group.id, {
          name: trimmedName,
          ownerOrganizationalUnitId: ownerUnitId === '' ? null : ownerUnitId,
          publishingPolicy,
        }),
      )
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

          <label className="field">
            <span>{t('users.owner_unit_field')}</span>
            <Select
              ariaLabel={t('users.owner_unit_field')}
              placeholder={t('users.owner_unit_none')}
              value={ownerUnitId === '' ? NONE_UNIT_VALUE : ownerUnitId}
              onValueChange={(next) => setOwnerUnitId(next === NONE_UNIT_VALUE ? '' : next)}
              disabled={isSaving}
              options={[
                { value: NONE_UNIT_VALUE, label: t('users.owner_unit_none') },
                ...unitSelectOptions(organizationalUnits),
              ]}
            />
          </label>

          <label className="field">
            <span>{t('users.publishing_policy_field')}</span>
            <select
              value={publishingPolicy}
              onChange={(event) => setPublishingPolicy(event.target.value)}
              disabled={isSaving}
            >
              {PUBLISHING_POLICIES.map((policy) => (
                <option key={policy} value={policy}>
                  {t(publishingPolicyLabelKey(policy))}
                </option>
              ))}
            </select>
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
  const { t } = useTranslation()
  const [name, setName] = useState('')
  const [ownerUnitId, setOwnerUnitId] = useState('')
  const [publishingPolicy, setPublishingPolicy] = useState<string>('OwnerScope')
  const [organizationalUnits, setOrganizationalUnits] = useState<OrganizationalUnitSummary[]>([])
  const [validationError, setValidationError] = useState<string | null>(null)
  const [apiError, setApiError] = useState<string | null>(null)
  const [isSaving, setIsSaving] = useState(false)

  useEffect(() => {
    let active = true
    void (async () => {
      try {
        const loaded = await listOrganizationalUnits()
        if (active) {
          setOrganizationalUnits(loaded)
        }
      } catch {
        // Owner unit is optional; ignore load failure here.
      }
    })()
    return () => {
      active = false
    }
  }, [])

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
      const group = await createGroup({
        name: trimmedName,
        ownerOrganizationalUnitId: ownerUnitId === '' ? null : ownerUnitId,
        publishingPolicy,
      })
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

          <label className="field">
            <span>{t('users.owner_unit_field')}</span>
            <Select
              ariaLabel={t('users.owner_unit_field')}
              placeholder={t('users.owner_unit_none')}
              value={ownerUnitId === '' ? NONE_UNIT_VALUE : ownerUnitId}
              onValueChange={(next) => setOwnerUnitId(next === NONE_UNIT_VALUE ? '' : next)}
              disabled={isSaving}
              options={[
                { value: NONE_UNIT_VALUE, label: t('users.owner_unit_none') },
                ...unitSelectOptions(organizationalUnits),
              ]}
            />
          </label>

          <label className="field">
            <span>{t('users.publishing_policy_field')}</span>
            <select
              value={publishingPolicy}
              onChange={(event) => setPublishingPolicy(event.target.value)}
              disabled={isSaving}
            >
              {PUBLISHING_POLICIES.map((policy) => (
                <option key={policy} value={policy}>
                  {t(publishingPolicyLabelKey(policy))}
                </option>
              ))}
            </select>
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
  const [organizationalUnits, setOrganizationalUnits] = useState<OrganizationalUnitSummary[]>([])
  const [organizationalUnitId, setOrganizationalUnitId] = useState('')
  const [orgUnitsError, setOrgUnitsError] = useState<string | null>(null)
  const [validationError, setValidationError] = useState<string | null>(null)
  const [apiError, setApiError] = useState<string | null>(null)
  const [isSaving, setIsSaving] = useState(false)

  useEffect(() => {
    let active = true
    void (async () => {
      try {
        const units = await listOrganizationalUnits()
        if (active) {
          setOrganizationalUnits(units)
        }
      } catch {
        if (active) {
          setOrgUnitsError('No se pudieron cargar las unidades organizativas.')
        }
      }
    })()
    return () => {
      active = false
    }
  }, [])

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
    if (organizationalUnitId.length === 0) {
      setValidationError('Seleccioná una unidad organizativa.')
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
        organizationalUnitId,
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
      description="Creá una cuenta local y asignale unidad organizativa, rol y grupos de acceso."
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
              <span>Unidad organizativa</span>
              <Select
                ariaLabel="Unidad organizativa"
                placeholder="Seleccioná una unidad"
                value={organizationalUnitId || undefined}
                onValueChange={setOrganizationalUnitId}
                disabled={isSaving}
                options={unitSelectOptions(organizationalUnits)}
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
                <option value="DocumentEditor">DocumentEditor</option>
                <option value="DocumentPublisher">DocumentPublisher</option>
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

          {orgUnitsError ? (
            <p className="status-message error" role="alert">
              {orgUnitsError}
            </p>
          ) : null}

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
  canEditRole,
  onClose,
  onSaved,
}: {
  user: UserSummary
  groups: GroupSummary[]
  canEditRole: boolean
  onClose: () => void
  onSaved: (user: UserSummary) => void
}) {
  const { t } = useTranslation()
  const [role, setRole] = useState(primaryRole(user.roles))
  const [selectedGroupIds, setSelectedGroupIds] = useState<string[]>(
    user.groups.map((group) => group.id),
  )
  const [organizationalUnitId, setOrganizationalUnitId] = useState(
    user.organizationalUnit?.id ?? '',
  )
  const [organizationalUnits, setOrganizationalUnits] = useState<OrganizationalUnitSummary[]>([])
  const [apiError, setApiError] = useState<string | null>(null)
  const [isSaving, setIsSaving] = useState(false)

  useEffect(() => {
    if (!canEditRole) {
      return
    }
    let active = true
    void (async () => {
      try {
        const loaded = await listOrganizationalUnits()
        if (active) {
          setOrganizationalUnits(loaded)
        }
      } catch {
        // Unit change is optional; ignore load failure.
      }
    })()
    return () => {
      active = false
    }
  }, [canEditRole])

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setApiError(null)
    setIsSaving(true)

    try {
      let updatedUser = user
      if (canEditRole && role !== primaryRole(user.roles)) {
        updatedUser = await updateUserRoles(user.id, { roles: [role] })
      }

      const originalGroupIds = user.groups.map((group) => group.id).sort().join(',')
      const nextGroupIds = [...selectedGroupIds].sort().join(',')
      if (nextGroupIds !== originalGroupIds) {
        updatedUser = await updateUserGroups(user.id, { groupIds: selectedGroupIds })
      }

      if (canEditRole && organizationalUnitId !== (user.organizationalUnit?.id ?? '')) {
        updatedUser = await updateUserOrganizationalUnit(user.id, { organizationalUnitId })
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
            {!canEditRole ? (
              <span>Unidad: {user.organizationalUnit?.name ?? '-'}</span>
            ) : null}
          </div>

          {canEditRole ? (
            <label className="field">
              <span>Rol</span>
              <select
                value={role}
                onChange={(event) => setRole(event.target.value)}
                disabled={isSaving}
              >
                <option value="Viewer">Viewer</option>
                <option value="DocumentEditor">DocumentEditor</option>
                <option value="DocumentPublisher">DocumentPublisher</option>
                <option value="Admin">Admin</option>
              </select>
            </label>
          ) : null}

          {canEditRole ? (
            <label className="field">
              <span>{t('users.change_unit_field')}</span>
              <Select
                ariaLabel={t('users.change_unit_field')}
                placeholder={t('users.change_unit_select')}
                value={organizationalUnitId || undefined}
                onValueChange={setOrganizationalUnitId}
                disabled={isSaving}
                options={unitSelectOptions(organizationalUnits)}
              />
            </label>
          ) : null}

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

  if (roles.includes('DocumentPublisher')) {
    return 'DocumentPublisher'
  }

  if (roles.includes('DocumentEditor')) {
    return 'DocumentEditor'
  }

  return roles.includes('Viewer') ? 'Viewer' : (roles[0] ?? 'Viewer')
}

function organizationalUnitLabel(unit: OrganizationalUnitSummary) {
  return unit.parentId === null ? `${unit.name} (toda la empresa)` : unit.name
}

// Radix Select disallows an empty-string item value, so the optional
// "no owner unit" choice uses a sentinel that maps back to '' on change.
const NONE_UNIT_VALUE = '__none__'

function unitSelectOptions(units: OrganizationalUnitSummary[]): SelectOption[] {
  return flattenUnitsInTreeOrder(units).map(({ unit, level }) => ({
    value: unit.id,
    label: organizationalUnitLabel(unit),
    depth: level,
    badge: <UnitLevelBadge level={level} />,
  }))
}

const PUBLISHING_POLICIES = ['OwnerScope', 'ExplicitGrantOnly', 'AdminOnly'] as const

function publishingPolicyLabelKey(policy: string): string {
  switch (policy) {
    case 'ExplicitGrantOnly':
      return 'users.publishing_policy_explicit_grant_only'
    case 'AdminOnly':
      return 'users.publishing_policy_admin_only'
    default:
      return 'users.publishing_policy_owner_scope'
  }
}
