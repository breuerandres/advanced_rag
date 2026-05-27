import { useEffect, useState } from 'react'
import { Download } from 'lucide-react'
import { Button, DataTable, Input } from '@helpcenter/shared-ui'
import { listFeedbackReport, type FeedbackReportItem } from '../../api/reporting'

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

  const columns = [
    {
      key: 'question',
      header: 'Pregunta',
      className: 'feedback-question-column',
      render: (item: FeedbackReportItem) => (
        <>
          <span className="user-name">{item.question}</span>
          <span className="user-email">{item.answerSummary}</span>
        </>
      ),
    },
    {
      key: 'user',
      header: 'Usuario',
      className: 'feedback-user-column',
      render: (item: FeedbackReportItem) => item.userDisplayName,
    },
    {
      key: 'feedback',
      header: 'Feedback',
      className: 'feedback-value-column feedback-value-cell',
      render: (item: FeedbackReportItem) => (item.feedbackValue === 'down' ? 'No sirvio' : 'Sirvio'),
    },
    {
      key: 'comment',
      header: 'Comentario',
      className: 'feedback-comment-column',
      render: (item: FeedbackReportItem) => item.feedbackComment ?? '-',
    },
    {
      key: 'cache',
      header: 'Cache',
      className: 'feedback-cache-column',
      render: (item: FeedbackReportItem) => (item.cacheHit ? 'Si' : 'No'),
    },
    {
      key: 'date',
      header: 'Fecha',
      className: 'feedback-date-column',
      render: (item: FeedbackReportItem) => formatDateTime(item.feedbackUpdatedAt),
    },
  ]

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
            <Input
              type="text"
              placeholder="ID de documento"
              value={citedDocumentId}
              onChange={(event) => setCitedDocumentId(event.target.value)}
            />
          </label>
          <label className="field">
            <span>Usuario</span>
            <Input
              type="text"
              placeholder="ID de usuario"
              value={userId}
              onChange={(event) => setUserId(event.target.value)}
            />
          </label>
          <label className="field">
            <span>Desde</span>
            <Input
              type="date"
              value={from}
              onChange={(event) => setFrom(event.target.value)}
            />
          </label>
          <label className="field">
            <span>Hasta</span>
            <Input
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
          <DataTable
            className="table-frame feedback-table"
            columns={columns}
            data={items}
            getRowId={(item) => item.queryAuditEventId}
          />
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
