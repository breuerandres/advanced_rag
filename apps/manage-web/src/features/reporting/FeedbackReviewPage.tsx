import { useEffect, useState } from 'react'
import { listFeedbackReport, type FeedbackReportItem } from '../../api/reporting'
import { Button } from '../../components/ui/button'

type PolarityFilter = 'all' | 'negative'

export function FeedbackReviewPage() {
  const [items, setItems] = useState<FeedbackReportItem[]>([])
  const [polarity, setPolarity] = useState<PolarityFilter>('all')
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
      })
      setItems(rows)
      setHasLoadedOnce(true)
    } catch {
      setLoadError(true)
    } finally {
      setIsLoading(false)
    }
  }

  return (
    <>

      <section className="workspace" id="feedback">
        <header className="workspace-header">
          <div>
            <p className="eyebrow">Reporting</p>
            <h1>Revision de feedback</h1>
          </div>
        </header>

        <section className="filter-bar" aria-label="Filtros de feedback">
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
            <table>
              <thead>
                <tr>
                  <th scope="col">Pregunta</th>
                  <th scope="col">Usuario</th>
                  <th scope="col">Feedback</th>
                  <th scope="col">Comentario</th>
                  <th scope="col">Citas</th>
                  <th scope="col">Request ID</th>
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
                    <td>{item.feedbackValue === 'down' ? 'No sirvio' : 'Sirvio'}</td>
                    <td>{item.feedbackComment ?? '-'}</td>
                    <td>{item.citations.map((citation) => citation.headingPath.join(' / ')).join(', ') || '-'}</td>
                    <td>{item.requestId}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : null}
      </section>
    </>
  )
}
