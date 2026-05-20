import { useEffect, useMemo, useState } from 'react'
import { ClipboardList, RefreshCw, Search } from 'lucide-react'
import { listAuditEvents, type ManagementAuditEvent } from '../../api/audit'
import { ApiError } from '../../lib/api-error'
import { Button } from '../../components/ui/button'

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
            <input
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
        <div className="empty-panel audit-functional-panel">
          <ClipboardList size={22} />
          <div>
            <h2>Eventos funcionales</h2>
            <p>Documentos, usuarios, grupos, presupuestos y revisiones.</p>
            <p className="muted-copy">
              Crea, guarda, envia a revision, archiva o restaura un documento para generar
              eventos funcionales visibles aca.
            </p>
          </div>
        </div>
      ) : null}

      {loadState === 'ready' && events.length > 0 && filteredEvents.length === 0 ? (
        <p className="status-message">No hay eventos que coincidan con los filtros.</p>
      ) : null}

      {loadState === 'ready' && filteredEvents.length > 0 ? (
        <>
          <h2 className="section-heading">Eventos funcionales</h2>
          <div className="table-frame">
            <table>
              <thead>
                <tr>
                  <th scope="col">Fecha</th>
                  <th scope="col">Evento</th>
                  <th scope="col">Actor</th>
                  <th scope="col">Entidad</th>
                  <th scope="col">Request ID</th>
                </tr>
              </thead>
              <tbody>
                {filteredEvents.map((event) => (
                  <tr key={event.id}>
                    <td>{formatDateTime(event.createdAt)}</td>
                    <th scope="row">
                      <span className="user-name">{event.eventLabel}</span>
                      <span className="user-email">{event.eventType}</span>
                    </th>
                    <td>{event.actorDisplayName ?? '-'}</td>
                    <td>
                      <span className="user-name">{displayEntityType(event.entityType)}</span>
                      <span className="user-email">{shortId(event.entityId)}</span>
                    </td>
                    <td>{event.requestId}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
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
    instruction: 'Documento',
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
