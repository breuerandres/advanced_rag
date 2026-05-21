import { useEffect, useState } from 'react'
import { Download } from 'lucide-react'
import { listFeedbackReport, type FeedbackReportItem } from '../../api/reporting'
import { Button } from '../../components/ui/button'

type PolarityFilter = 'all' | 'negative'

export function FeedbackReviewPage({ embedded = false }: { embedded?: boolean }) {
  const [items, setItems] = useState<FeedbackReportItem[]>([])
  const [polarity, setPolarity] = useState<PolarityFilter>('all')
  const [citedDocumentId, setCitedDocumentId] = useState('')
  const [userId, setUserId] = useState('')
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [hasLoadedOnce, setHasLoadedOnce] = useState(false)
  const [isLoading, setIsLoading] = useState(false)
  const [loadError, setLoadError] = useState(false)

  useEffect(() => {
    void loadFeedback(false)
  }, [])

  async function loadFeedback(withFilters: boolean) {
    setIsLoading(true)
    setLoadError(false)
    try {
      const rows = await listFeedbackReport({
        negativeOnly: withFilters && polarity === 'negative',
        citedDocumentId: withFilters ? citedDocumentId.trim() : '',
        userId: withFilters ? userId.trim() : '',
        from: withFilters && from ? `${from}T00:00:00Z` : '',
        to: withFilters && to ? `${to}T23:59:59Z` : '',
      })
      setItems(rows)
      setHasLoadedOnce(true)
    } catch {
      setLoadError(true)
    } finally {
      setIsLoading(false)
    }
  }

  function exportToExcelCsv() {
    const rows = [
      [
        'queryAuditEventId',
        'userId',
        'userDisplayName',
        'question',
        'answerSummary',
        'feedbackValue',
        'feedbackComment',
        'feedbackUpdatedAt',
        'createdAt',
        'cacheHit',
        'requestId',
        'citations',
      ],
      ...items.map((item) => [
        item.queryAuditEventId,
        item.userId,
        item.userDisplayName,
        item.question,
        item.answerSummary,
        item.feedbackValue,
        item.feedbackComment ?? '',
        item.feedbackUpdatedAt,
        item.createdAt,
        item.cacheHit ? 'true' : 'false',
        item.requestId,
        item.citations
          .map((citation) =>
            [
              citation.documentId,
              citation.documentVersionId,
              citation.headingPath.join(' / '),
            ].join(' | '),
          )
          .join('; '),
      ]),
    ]
    const csv = rows.map((row) => row.map(csvCell).join(',')).join('\r\n')
    const blob = new Blob([`\uFEFF${csv}`], { type: 'text/csv;charset=utf-8' })
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = `feedback-${new Date().toISOString().slice(0, 10)}.csv`
    link.click()
    URL.revokeObjectURL(url)
  }

  return (
    <section className={embedded ? 'audit-feedback-panel' : 'workspace'} id="feedback">
        <header className="workspace-header">
          <div>
            <p className="eyebrow">Revision de chat</p>
            {embedded ? <h2>Feedback auditado</h2> : <h1>Feedback auditado</h1>}
          </div>
          {items.length > 0 ? (
            <div className="workspace-actions">
              <Button className="text-button" type="button" onClick={exportToExcelCsv}>
                <Download size={16} />
                Exportar a Excel
              </Button>
            </div>
          ) : null}
        </header>

        <section className="feedback-filter-grid" aria-label="Filtros de feedback">
          <label className="field filter-status">
            <span>Polaridad</span>
            <select
              value={polarity}
              onChange={(event) => setPolarity(event.target.value as PolarityFilter)}
            >
              <option value="all">Todas</option>
              <option value="negative">Negativo</option>
            </select>
          </label>
          <label className="field">
            <span>Documento citado</span>
            <input
              type="text"
              placeholder="ID de documento"
              value={citedDocumentId}
              onChange={(event) => setCitedDocumentId(event.target.value)}
            />
          </label>
          <label className="field">
            <span>Usuario</span>
            <input
              type="text"
              placeholder="ID de usuario"
              value={userId}
              onChange={(event) => setUserId(event.target.value)}
            />
          </label>
          <label className="field">
            <span>Desde</span>
            <input
              type="date"
              value={from}
              onChange={(event) => setFrom(event.target.value)}
            />
          </label>
          <label className="field">
            <span>Hasta</span>
            <input
              type="date"
              value={to}
              onChange={(event) => setTo(event.target.value)}
            />
          </label>
          <Button
            className="primary-button"
            type="button"
            disabled={isLoading}
            onClick={() => void loadFeedback(true)}
          >
            Aplicar filtros
          </Button>
        </section>

        {isLoading ? <p className="status-message">Cargando feedback...</p> : null}
        {loadError ? (
          <p className="status-message error" role="alert">
            No se pudo cargar el feedback.
          </p>
        ) : null}
        {hasLoadedOnce && !isLoading && items.length === 0 ? (
          <p className="status-message">Todavia no hay feedback registrado.</p>
        ) : null}

        {items.length > 0 ? (
          <div className="table-frame">
            <table className="data-table feedback-table">
              <colgroup>
                <col className="feedback-question-column" />
                <col className="feedback-user-column" />
                <col className="feedback-value-column" />
                <col className="feedback-comment-column" />
                <col className="feedback-cache-column" />
                <col className="feedback-date-column" />
              </colgroup>
              <thead>
                <tr>
                  <th scope="col">Pregunta</th>
                  <th scope="col">Usuario</th>
                  <th scope="col">Feedback</th>
                  <th scope="col">Comentario</th>
                  <th scope="col">Cache</th>
                  <th scope="col">Fecha</th>
                </tr>
              </thead>
              <tbody>
                {items.map((item) => (
                  <tr key={item.queryAuditEventId}>
                    <th scope="row">
                      <span className="user-name">{item.question}</span>
                      <span className="user-email">{item.answerSummary}</span>
                    </th>
                    <td>{item.userDisplayName}</td>
                    <td className="feedback-value-cell">{item.feedbackValue === 'down' ? 'No sirvio' : 'Sirvio'}</td>
                    <td>{item.feedbackComment ?? '-'}</td>
                    <td>{item.cacheHit ? 'Si' : 'No'}</td>
                    <td>{formatDateTime(item.feedbackUpdatedAt)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : null}
    </section>
  )
}

function csvCell(value: string) {
  return `"${value.replaceAll('"', '""')}"`
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
