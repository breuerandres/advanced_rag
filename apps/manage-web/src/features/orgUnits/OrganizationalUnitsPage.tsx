import { useEffect, useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { ChevronDown, ChevronRight, Plus, Power, Pencil } from 'lucide-react'
import { ApiError } from '../../lib/api-error'
import {
  createOrganizationalUnit,
  listOrganizationalUnits,
  updateOrganizationalUnit,
} from '../../api/orgUnits'
import type { OrganizationalUnitSummary } from '../../api/orgUnits'
import { Button, Dialog, Input } from '@helpcenter/shared-ui'

type LoadState = 'loading' | 'ready' | 'error'

interface DialogState {
  mode: 'create' | 'rename'
  unit: OrganizationalUnitSummary
}

export function OrganizationalUnitsPage() {
  const { t } = useTranslation()
  const [units, setUnits] = useState<OrganizationalUnitSummary[]>([])
  const [loadState, setLoadState] = useState<LoadState>('loading')
  const [expanded, setExpanded] = useState<Set<string>>(new Set())
  const [dialog, setDialog] = useState<DialogState | null>(null)
  const [errorMessage, setErrorMessage] = useState<string | null>(null)

  useEffect(() => {
    void loadUnits()
  }, [])

  async function loadUnits() {
    setLoadState('loading')
    try {
      const loaded = await listOrganizationalUnits(true)
      setUnits(loaded)
      setExpanded(new Set(loaded.map((unit) => unit.id)))
      setLoadState('ready')
    } catch {
      setLoadState('error')
    }
  }

  const childrenByParent = useMemo(() => {
    const map = new Map<string | null, OrganizationalUnitSummary[]>()
    for (const unit of units) {
      const siblings = map.get(unit.parentId) ?? []
      siblings.push(unit)
      map.set(unit.parentId, siblings)
    }
    for (const siblings of map.values()) {
      siblings.sort((left, right) => left.name.localeCompare(right.name))
    }
    return map
  }, [units])

  const roots = childrenByParent.get(null) ?? []

  function toggle(id: string) {
    setExpanded((current) => {
      const next = new Set(current)
      if (next.has(id)) {
        next.delete(id)
      } else {
        next.add(id)
      }
      return next
    })
  }

  function upsertUnit(updated: OrganizationalUnitSummary) {
    setUnits((current) => {
      const exists = current.some((unit) => unit.id === updated.id)
      return exists
        ? current.map((unit) => (unit.id === updated.id ? updated : unit))
        : [...current, updated]
    })
    setExpanded((current) => new Set(current).add(updated.parentId ?? updated.id))
  }

  async function toggleActive(unit: OrganizationalUnitSummary) {
    setErrorMessage(null)
    try {
      const updated = await updateOrganizationalUnit(unit.id, { isActive: !unit.isActive })
      upsertUnit(updated)
    } catch (error) {
      const reference = error instanceof ApiError ? error.requestId : 'unknown'
      setErrorMessage(t('orgUnits.update_error', { reference }))
    }
  }

  function renderNode(unit: OrganizationalUnitSummary) {
    const children = childrenByParent.get(unit.id) ?? []
    const isExpanded = expanded.has(unit.id)
    const isRoot = unit.parentId === null
    return (
      <li key={unit.id} className="org-unit-node">
        <div className={unit.isActive ? 'org-unit-row' : 'org-unit-row inactive'}>
          {children.length > 0 ? (
            <button
              type="button"
              className="org-unit-toggle"
              aria-label={unit.name}
              onClick={() => toggle(unit.id)}
            >
              {isExpanded ? <ChevronDown size={16} /> : <ChevronRight size={16} />}
            </button>
          ) : (
            <span className="org-unit-toggle-spacer" aria-hidden="true" />
          )}
          <span className="org-unit-name">
            {unit.name}
            {isRoot ? ` (${t('orgUnits.company_wide')})` : null}
            {!unit.isActive ? ` (${t('orgUnits.inactive')})` : null}
          </span>
          <span className="org-unit-actions">
            <Button
              className="text-button"
              type="button"
              onClick={() => setDialog({ mode: 'create', unit })}
            >
              <Plus size={14} />
              {t('orgUnits.add_child')}
            </Button>
            <Button
              className="text-button"
              type="button"
              onClick={() => setDialog({ mode: 'rename', unit })}
            >
              <Pencil size={14} />
              {t('orgUnits.rename')}
            </Button>
            {!isRoot ? (
              <Button className="text-button" type="button" onClick={() => void toggleActive(unit)}>
                <Power size={14} />
                {unit.isActive ? t('orgUnits.deactivate') : t('orgUnits.activate')}
              </Button>
            ) : null}
          </span>
        </div>
        {isExpanded && children.length > 0 ? (
          <ul className="org-unit-children">{children.map(renderNode)}</ul>
        ) : null}
      </li>
    )
  }

  return (
    <section className="workspace" id="unidades">
      <header className="workspace-header">
        <div>
          <p className="eyebrow">{t('orgUnits.eyebrow')}</p>
          <h1>{t('orgUnits.title')}</h1>
        </div>
      </header>

      {errorMessage ? (
        <p className="status-message error" role="alert">
          {errorMessage}
        </p>
      ) : null}
      {loadState === 'loading' ? <p className="status-message">{t('orgUnits.loading')}</p> : null}
      {loadState === 'error' ? (
        <p className="status-message error" role="alert">
          {t('orgUnits.load_error')}
        </p>
      ) : null}

      {loadState === 'ready' ? (
        <ul className="org-unit-tree">{roots.map(renderNode)}</ul>
      ) : null}

      {dialog ? (
        <OrganizationalUnitDialog
          state={dialog}
          onClose={() => setDialog(null)}
          onSaved={(unit) => {
            upsertUnit(unit)
            setDialog(null)
          }}
        />
      ) : null}
    </section>
  )
}

function OrganizationalUnitDialog({
  state,
  onClose,
  onSaved,
}: {
  state: DialogState
  onClose: () => void
  onSaved: (unit: OrganizationalUnitSummary) => void
}) {
  const { t } = useTranslation()
  const [name, setName] = useState(state.mode === 'rename' ? state.unit.name : '')
  const [validationError, setValidationError] = useState<string | null>(null)
  const [apiError, setApiError] = useState<string | null>(null)
  const [isSaving, setIsSaving] = useState(false)

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setValidationError(null)
    setApiError(null)

    const trimmed = name.trim()
    if (trimmed.length === 0) {
      setValidationError(t('orgUnits.name_required'))
      return
    }

    setIsSaving(true)
    try {
      const saved =
        state.mode === 'create'
          ? await createOrganizationalUnit({ name: trimmed, parentId: state.unit.id })
          : await updateOrganizationalUnit(state.unit.id, { name: trimmed })
      onSaved(saved)
    } catch (error) {
      const reference = error instanceof ApiError ? error.requestId : 'unknown'
      setApiError(
        t(state.mode === 'create' ? 'orgUnits.create_error' : 'orgUnits.update_error', { reference }),
      )
    } finally {
      setIsSaving(false)
    }
  }

  return (
    <Dialog
      open
      title={t(state.mode === 'create' ? 'orgUnits.create_title' : 'orgUnits.rename_title')}
      description={state.mode === 'create' ? t('orgUnits.create_description') : undefined}
      onOpenChange={(open) => {
        if (!open) {
          onClose()
        }
      }}
    >
      <form className="dialog-form" noValidate onSubmit={handleSubmit}>
        <label className="field">
          <span>{t('orgUnits.name_field')}</span>
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
            {t('orgUnits.cancel')}
          </Button>
          <Button className="primary-button" type="submit" disabled={isSaving}>
            {isSaving ? t('orgUnits.saving') : t('orgUnits.save')}
          </Button>
        </div>
      </form>
    </Dialog>
  )
}
