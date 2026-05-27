import { useEffect, useMemo, useState } from 'react'
import { ClipboardList, RefreshCw, Search } from 'lucide-react'
import { Button, DataTable, EmptyState, Input } from '@helpcenter/shared-ui'
import { listAuditEvents, type ManagementAuditEvent } from '../../api/audit'
import { ApiError } from '../../lib/api-error'

type LoadState = 'loading' | 'ready' | 'error'

export function AuditPage() {
  const [events, setEvents] = useState<ManagementAuditEvent[]>([])
  const [loadState, setLoadState] = useState<LoadState>('loading')
  const [searchQuery, setSearchQuery] = useState('')
  const [eventTypeFilter, setEventTypeFilter] = useState('all')
  const [errorMessage, setErrorMessage] = useState<string | null>(null)

  useEffect(() => {
    void loadEvents()
  }, [])

  async function loadEvents() {
    setLoadState('loading')
    setErrorMessage(null)
    try {
      setEvents(await listAuditEvents())
      setLoadState('ready')
    } catch (error) {
      const reference = error instanceof ApiError ? error.requestId : 'unknown'
      setErrorMessage(`No se pudo cargar la auditoria. Referencia: ${reference}.`)
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
        header: 'Fecha',
        render: (event: ManagementAuditEvent) => formatDateTime(event.createdAt),
      },
      {
        key: 'event',
        header: 'Evento',
        render: (event: ManagementAuditEvent) => (
          <>
            <span className="user-name">{event.eventLabel}</span>
            <span className="user-email">{event.eventType}</span>
          </>
        ),
      },
      {
        key: 'actor',
        header: 'Actor',
        render: (event: ManagementAuditEvent) => event.actorDisplayName ?? '-',
      },
      {
        key: 'entity',
        header: 'Entidad',
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
    [],
  )

  return (
    <section className="workspace" id="audit">
      <header className="workspace-header">
        <div>
          <p className="eyebrow">Control</p>
          <h1>Auditoria</h1>
        </div>
        <div className="workspace-actions">
          <Button
            className="icon-button"
            type="button"
            aria-label="Actualizar auditoria"
            disabled={loadState === 'loading'}
            onClick={() => void loadEvents()}
          >
            <RefreshCw size={18} />
          </Button>
        </div>
      </header>

      <section className="filter-bar" aria-label="Filtros de auditoria">
        <label className="field filter-search">
          <span>Buscar eventos</span>
          <span className="search-control">
            <Search size={16} />
            <Input
              type="search"
              placeholder="Documento, usuario o request ID"
              value={searchQuery}
              onChange={(event) => setSearchQuery(event.target.value)}
            />
          </span>
        </label>
        <label className="field filter-status">
          <span>Tipo</span>
          <select
            value={eventTypeFilter}
            onChange={(event) => setEventTypeFilter(event.target.value)}
          >
            <option value="all">Todos</option>
            {eventTypes.map(([eventType, eventLabel]) => (
              <option key={eventType} value={eventType}>
                {eventLabel}
              </option>
            ))}
          </select>
        </label>
      </section>

      {loadState === 'loading' ? (
        <p className="status-message">Cargando eventos de auditoria...</p>
      ) : null}

      {loadState === 'error' && errorMessage ? (
        <p className="status-message error" role="alert">
          {errorMessage}
        </p>
      ) : null}

      {loadState === 'ready' && events.length === 0 ? (
        <EmptyState
          className="empty-panel audit-functional-panel"
          title="Eventos funcionales"
          description="Documentos, usuarios, grupos, presupuestos y revisiones. Crea, guarda, envia a revision, archiva o restaura un documento para generar eventos funcionales visibles aca."
          icon={<ClipboardList size={22} aria-hidden="true" />}
        />
      ) : null}

      {loadState === 'ready' && events.length > 0 && filteredEvents.length === 0 ? (
        <p className="status-message">No hay eventos que coincidan con los filtros.</p>
      ) : null}

      {loadState === 'ready' && filteredEvents.length > 0 ? (
        <>
          <h2 className="section-heading">Eventos funcionales</h2>
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
