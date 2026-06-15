import { useEffect, useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { Pencil, Plus, Power, Trash2 } from 'lucide-react'
import { ApiError } from '../../lib/api-error'
import {
  createDocumentType,
  deleteDocumentType,
  listDocumentTypes,
  updateDocumentType,
  type DocumentTypeSummary,
} from '../../api/documentTypes'
import { Button, DataTable, Dialog, Input } from '@helpcenter/shared-ui'

type LoadState = 'loading' | 'ready' | 'error'

interface DialogState {
  mode: 'create' | 'edit'
  type: DocumentTypeSummary | null
}

export function DocumentTypesPage() {
  const { t } = useTranslation()
  const [types, setTypes] = useState<DocumentTypeSummary[]>([])
  const [loadState, setLoadState] = useState<LoadState>('loading')
  const [dialog, setDialog] = useState<DialogState | null>(null)
  const [errorMessage, setErrorMessage] = useState<string | null>(null)

  async function loadTypes() {
    setLoadState('loading')
    try {
      const loaded = await listDocumentTypes(true)
      setTypes(loaded)
      setLoadState('ready')
    } catch {
      setLoadState('error')
    }
  }

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect -- Initial load synchronizes remote data on mount.
    void loadTypes()
  }, [])

  const sortedTypes = useMemo(
    () =>
      [...types].sort(
        (left, right) => left.sortOrder - right.sortOrder || left.name.localeCompare(right.name),
      ),
    [types],
  )

  function upsertType(updated: DocumentTypeSummary) {
    setTypes((current) => {
      const exists = current.some((type) => type.id === updated.id)
      return exists
        ? current.map((type) => (type.id === updated.id ? updated : type))
        : [...current, updated]
    })
  }

  async function toggleActive(type: DocumentTypeSummary) {
    setErrorMessage(null)
    try {
      const updated = await updateDocumentType(type.id, { isActive: !type.isActive })
      upsertType(updated)
    } catch (error) {
      const reference = error instanceof ApiError ? error.requestId : 'unknown'
      setErrorMessage(t('documentTypes.update_error', { reference }))
    }
  }

  async function removeType(type: DocumentTypeSummary) {
    if (!window.confirm(t('documentTypes.delete_confirm', { name: type.name }))) {
      return
    }

    setErrorMessage(null)
    try {
      await deleteDocumentType(type.id)
      setTypes((current) => current.filter((item) => item.id !== type.id))
    } catch (error) {
      if (error instanceof ApiError && error.code === 'DOCUMENT_TYPE_IN_USE') {
        setErrorMessage(t('documentTypes.in_use_error'))
        return
      }

      const reference = error instanceof ApiError ? error.requestId : 'unknown'
      setErrorMessage(t('documentTypes.delete_error', { reference }))
    }
  }

  return (
    <section className="workspace" id="document-types">
      <header className="workspace-header">
        <div>
          <p className="eyebrow">{t('documentTypes.eyebrow')}</p>
          <h1>{t('documentTypes.title')}</h1>
        </div>
        <Button
          className="primary-button"
          type="button"
          onClick={() => setDialog({ mode: 'create', type: null })}
        >
          <Plus size={16} />
          {t('documentTypes.create')}
        </Button>
      </header>

      {errorMessage ? (
        <p className="status-message error" role="alert">
          {errorMessage}
        </p>
      ) : null}
      {loadState === 'loading' ? (
        <p className="status-message">{t('documentTypes.loading')}</p>
      ) : null}
      {loadState === 'error' ? (
        <p className="status-message error" role="alert">
          {t('documentTypes.load_error')}
        </p>
      ) : null}

      {loadState === 'ready' ? (
        sortedTypes.length === 0 ? (
          <p className="status-message">{t('documentTypes.empty')}</p>
        ) : (
          <DataTable
            columns={[
              {
                key: 'name',
                header: t('documentTypes.name_column'),
                render: (type) => type.name,
              },
              {
                key: 'order',
                header: t('documentTypes.order_column'),
                render: (type) => String(type.sortOrder),
              },
              {
                key: 'status',
                header: t('documentTypes.status_column'),
                render: (type) =>
                  type.isActive ? t('documentTypes.active') : t('documentTypes.inactive'),
              },
              {
                key: 'actions',
                header: t('documentTypes.actions_column'),
                render: (type) => (
                  <div className="row-actions">
                    <Button
                      className="icon-button"
                      type="button"
                      aria-label={t('documentTypes.edit_for', { name: type.name })}
                      tooltip={t('documentTypes.edit')}
                      onClick={() => setDialog({ mode: 'edit', type })}
                    >
                      <Pencil size={16} />
                    </Button>
                    <Button
                      className="icon-button"
                      type="button"
                      aria-label={
                        type.isActive
                          ? t('documentTypes.deactivate_for', { name: type.name })
                          : t('documentTypes.activate_for', { name: type.name })
                      }
                      tooltip={type.isActive ? t('documentTypes.deactivate') : t('documentTypes.activate')}
                      onClick={() => void toggleActive(type)}
                    >
                      <Power size={16} />
                    </Button>
                    <Button
                      className="icon-button"
                      type="button"
                      aria-label={t('documentTypes.delete_for', { name: type.name })}
                      tooltip={t('documentTypes.delete')}
                      onClick={() => void removeType(type)}
                    >
                      <Trash2 size={16} />
                    </Button>
                  </div>
                ),
              },
            ]}
            data={sortedTypes}
            getRowId={(type) => type.id}
          />
        )
      ) : null}

      {dialog ? (
        <DocumentTypeDialog
          state={dialog}
          onClose={() => setDialog(null)}
          onSaved={(type) => {
            upsertType(type)
            setDialog(null)
          }}
        />
      ) : null}
    </section>
  )
}

function DocumentTypeDialog({
  state,
  onClose,
  onSaved,
}: {
  state: DialogState
  onClose: () => void
  onSaved: (type: DocumentTypeSummary) => void
}) {
  const { t } = useTranslation()
  const [name, setName] = useState(state.type?.name ?? '')
  const [sortOrder, setSortOrder] = useState(String(state.type?.sortOrder ?? 0))
  const [validationError, setValidationError] = useState<string | null>(null)
  const [apiError, setApiError] = useState<string | null>(null)
  const [isSaving, setIsSaving] = useState(false)

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setValidationError(null)
    setApiError(null)

    const trimmed = name.trim()
    if (trimmed.length === 0) {
      setValidationError(t('documentTypes.name_required'))
      return
    }

    const parsedOrder = Number.parseInt(sortOrder, 10)
    const order = Number.isNaN(parsedOrder) ? 0 : parsedOrder

    setIsSaving(true)
    try {
      const saved =
        state.mode === 'create'
          ? await createDocumentType({ name: trimmed, sortOrder: order })
          : await updateDocumentType(state.type!.id, { name: trimmed, sortOrder: order })
      onSaved(saved)
    } catch (error) {
      if (error instanceof ApiError && error.code === 'CONFLICT') {
        setApiError(t('documentTypes.name_taken_error'))
      } else {
        const reference = error instanceof ApiError ? error.requestId : 'unknown'
        setApiError(
          t(
            state.mode === 'create'
              ? 'documentTypes.create_error'
              : 'documentTypes.update_error',
            { reference },
          ),
        )
      }
    } finally {
      setIsSaving(false)
    }
  }

  return (
    <Dialog
      open
      title={t(state.mode === 'create' ? 'documentTypes.create_title' : 'documentTypes.edit_title')}
      onOpenChange={(open) => {
        if (!open) {
          onClose()
        }
      }}
    >
      <form className="dialog-form" noValidate onSubmit={handleSubmit}>
        <label className="field">
          <span>{t('documentTypes.name_field')}</span>
          <Input
            type="text"
            value={name}
            onChange={(event) => setName(event.target.value)}
            disabled={isSaving}
          />
        </label>
        <label className="field">
          <span>{t('documentTypes.order_field')}</span>
          <Input
            type="number"
            value={sortOrder}
            onChange={(event) => setSortOrder(event.target.value)}
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
            {t('documentTypes.cancel')}
          </Button>
          <Button className="primary-button" type="submit" disabled={isSaving}>
            {isSaving ? t('documentTypes.saving') : t('documentTypes.save')}
          </Button>
        </div>
      </form>
    </Dialog>
  )
}
