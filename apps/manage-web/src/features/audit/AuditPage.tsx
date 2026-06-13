import { useEffect, useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { ClipboardList, Download, RefreshCw, Search } from 'lucide-react'
import { Button, DataTable, EmptyState, Input } from '@helpcenter/shared-ui'
import { listAuditEvents, type ManagementAuditEvent } from '../../api/audit'
import { ApiError } from '../../lib/api-error'
import { exportRowsToXlsx } from '../../lib/xlsx'

type LoadState = 'loading' | 'ready' | 'error'

export function AuditPage() {
  const { t } = useTranslation()
  const [events, setEvents] = useState<ManagementAuditEvent[]>([])
  const [loadState, setLoadState] = useState<LoadState>('loading')
  const [searchQuery, setSearchQuery] = useState('')
  const [eventTypeFilter, setEventTypeFilter] = useState('all')
  const [errorMessage, setErrorMessage] = useState<string | null>(null)

  useEffect(() => {
    void loadEvents()
    // eslint-disable-next-line react-hooks/exhaustive-deps -- one-time mount load; loadEvents is stable within the component lifetime
  }, [])

  async function loadEvents() {
    setLoadState('loading')
    setErrorMessage(null)
    try {
      setEvents(await listAuditEvents())
      setLoadState('ready')
    } catch (error) {
      const reference = error instanceof ApiError ? error.requestId : 'unknown'
      setErrorMessage(t('audit.load_error', { reference }))
      setLoadState('error')
    }
  }

  const eventTypes = useMemo(
    () =>
      [...new Map(events.map((event) => [event.eventType, event.eventLabel])).entries()]
        .sort((left, right) => left[1].localeCompare(right[1])),
    [events],
  )

  const filteredEvents = useMemo(() => {
    const normalizedQuery = searchQuery.trim().toLowerCase()

    return events.filter((event) => {
      if (eventTypeFilter !== 'all' && event.eventType !== eventTypeFilter) {
        return false
      }

      if (normalizedQuery.length === 0) {
        return true
      }

      const searchableText = [
        event.eventLabel,
        event.eventType,
        event.actorDisplayName ?? '',
        event.entityType,
        event.entityId ?? '',
        event.requestId,
        detailsText(event.details),
      ]
        .join(' ')
        .toLowerCase()

      return searchableText.includes(normalizedQuery)
    })
  }, [eventTypeFilter, events, searchQuery])

  const columns = useMemo(
    () => [
      {
        key: 'createdAt',
        header: t('audit.date_column'),
        render: (event: ManagementAuditEvent) => formatDateTime(event.createdAt),
      },
      {
        key: 'event',
        header: t('audit.event_column'),
        render: (event: ManagementAuditEvent) => (
          <>
            <span className="user-name">{event.eventLabel}</span>
            <span className="user-email">{event.eventType}</span>
          </>
        ),
      },
      {
        key: 'actor',
        header: t('audit.actor_column'),
        render: (event: ManagementAuditEvent) => event.actorDisplayName ?? '-',
      },
      {
        key: 'entity',
        header: t('audit.entity_column'),
        render: (event: ManagementAuditEvent) => (
          <>
            <span className="user-name">{displayEntityType(event.entityType)}</span>
            <span className="user-email">{shortId(event.entityId)}</span>
          </>
        ),
      },
      {
        key: 'requestId',
        header: 'Request ID',
        render: (event: ManagementAuditEvent) => event.requestId,
      },
    ],
    [t],
  )

  async function exportAudit() {
    // Export the rows currently visible (respecting the search/type filters), splitting the
    // merged event and entity table cells into dedicated columns and using the full entity id.
    await exportRowsToXlsx<ManagementAuditEvent>(
      `auditoria-${new Date().toISOString().slice(0, 10)}.xlsx`,
      {
        sheetName: t('audit.title'),
        columns: [
          {
            header: t('audit.date_column'),
            value: (event) => new Date(event.createdAt),
            numFmt: 'dd/mm/yyyy hh:mm',
            width: 20,
          },
          { header: t('audit.event_column'), value: (event) => event.eventLabel, width: 32 },
          { header: t('audit.event_type_column'), value: (event) => event.eventType, width: 28 },
          { header: t('audit.actor_column'), value: (event) => event.actorDisplayName, width: 24 },
          {
            header: t('audit.entity_type_column'),
            value: (event) => displayEntityType(event.entityType),
            width: 18,
          },
          { header: t('audit.entity_id_column'), value: (event) => event.entityId, width: 38 },
          { header: t('audit.request_id_column'), value: (event) => event.requestId, width: 38 },
        ],
        rows: filteredEvents,
      },
    )
  }

  return (
    <section className="workspace" id="audit">
      <header className="workspace-header">
        <div>
          <p className="eyebrow">{t('audit.eyebrow')}</p>
          <h1>{t('audit.title')}</h1>
        </div>
        <div className="workspace-actions">
          {loadState === 'ready' && filteredEvents.length > 0 ? (
            <Button className="text-button" type="button" onClick={() => void exportAudit()}>
              <Download size={16} />
              {t('audit.export')}
            </Button>
          ) : null}
          <Button
            className="icon-button"
            type="button"
            aria-label={t('audit.refresh')}
            disabled={loadState === 'loading'}
            onClick={() => void loadEvents()}
          >
            <RefreshCw size={18} />
          </Button>
        </div>
      </header>

      <section className="filter-bar" aria-label={t('audit.filters_label')}>
        <label className="field filter-search">
          <span>{t('audit.search_events')}</span>
          <span className="search-control">
            <Search size={16} />
            <Input
              type="search"
              placeholder={t('audit.search_placeholder')}
              value={searchQuery}
              onChange={(event) => setSearchQuery(event.target.value)}
            />
          </span>
        </label>
        <label className="field filter-status">
          <span>{t('audit.type')}</span>
          <select
            value={eventTypeFilter}
            onChange={(event) => setEventTypeFilter(event.target.value)}
          >
            <option value="all">{t('audit.all')}</option>
            {eventTypes.map(([eventType, eventLabel]) => (
              <option key={eventType} value={eventType}>
                {eventLabel}
              </option>
            ))}
          </select>
        </label>
      </section>

      {loadState === 'loading' ? (
        <p className="status-message">{t('audit.loading')}</p>
      ) : null}

      {loadState === 'error' && errorMessage ? (
        <p className="status-message error" role="alert">
          {errorMessage}
        </p>
      ) : null}

      {loadState === 'ready' && events.length === 0 ? (
        <EmptyState
          className="empty-panel audit-functional-panel"
          title={t('audit.functional_events')}
          description={t('audit.empty_description')}
          icon={<ClipboardList size={22} aria-hidden="true" />}
        />
      ) : null}

      {loadState === 'ready' && events.length > 0 && filteredEvents.length === 0 ? (
        <p className="status-message">{t('audit.empty_filtered')}</p>
      ) : null}

      {loadState === 'ready' && filteredEvents.length > 0 ? (
        <>
          <h2 className="section-heading">{t('audit.functional_events')}</h2>
          <DataTable
            className="table-frame audit-table"
            columns={columns}
            data={filteredEvents}
            getRowId={(event) => event.id}
          />
        </>
      ) : null}
    </section>
  )
}

function detailsText(details: Record<string, unknown>) {
  return Object.values(details)
    .map((value) => String(value ?? ''))
    .join(' ')
}

function displayEntityType(entityType: string) {
  const labels: Record<string, string> = {
    document: 'Documento',
    user: 'Usuario',
    group: 'Grupo',
    budget: 'Presupuesto',
  }
  return labels[entityType] ?? entityType
}

function shortId(value: string | null) {
  return value ? value.slice(0, 8) : '-'
}

function formatDateTime(value: string) {
  return new Intl.DateTimeFormat('es-AR', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date(value))
}
