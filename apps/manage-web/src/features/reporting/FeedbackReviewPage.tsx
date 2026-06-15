import { useCallback, useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Download } from 'lucide-react'
import { Button, DataTable, Input, Pagination } from '@helpcenter/shared-ui'
import {
  listFeedbackReport,
  type FeedbackReportFilters,
  type FeedbackReportItem,
} from '../../api/reporting'
import { exportRowsToXlsx } from '../../lib/xlsx'

type PolarityFilter = 'all' | 'negative'

const FEEDBACK_PAGE_SIZE = 10

export function FeedbackReviewPage({ embedded = false }: { embedded?: boolean }) {
  const { t } = useTranslation()
  const [items, setItems] = useState<FeedbackReportItem[]>([])
  const [polarity, setPolarity] = useState<PolarityFilter>('all')
  const [citedDocumentId, setCitedDocumentId] = useState('')
  const [userId, setUserId] = useState('')
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [hasLoadedOnce, setHasLoadedOnce] = useState(false)
  const [isLoading, setIsLoading] = useState(false)
  const [loadError, setLoadError] = useState(false)
  const [page, setPage] = useState(1)

  const loadFeedback = useCallback(async (filters: FeedbackReportFilters) => {
    setIsLoading(true)
    setLoadError(false)
    try {
      const rows = await listFeedbackReport(filters)
      setItems(rows)
      setPage(1)
      setHasLoadedOnce(true)
    } catch {
      setLoadError(true)
    } finally {
      setIsLoading(false)
    }
  }, [])

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect -- Initial report load synchronizes remote data on mount.
    void loadFeedback({})
  }, [loadFeedback])

  async function exportFeedback() {
    // Columns mirror the on-screen table; the merged question/answer cell is split into two
    // columns and the internal IDs are dropped so the spreadsheet stays clean and filterable.
    await exportRowsToXlsx<FeedbackReportItem>(
      `feedback-${new Date().toISOString().slice(0, 10)}.xlsx`,
      {
        sheetName: t('feedback.title'),
        columns: [
          { header: t('feedback.question_column'), value: (item) => item.question, width: 48 },
          { header: t('feedback.answer_column'), value: (item) => item.answerSummary, width: 48 },
          { header: t('feedback.user'), value: (item) => item.userDisplayName, width: 24 },
          { header: t('feedback.email_column'), value: (item) => item.userEmail ?? '', width: 28 },
          {
            header: t('feedback.title'),
            value: (item) => feedbackLabel(item.feedbackValue, t),
            width: 16,
          },
          { header: t('feedback.comment_column'), value: (item) => item.feedbackComment, width: 40 },
          {
            header: t('feedback.cache_column'),
            value: (item) => (item.cacheHit ? t('feedback.cache_yes') : t('feedback.cache_no')),
            width: 10,
          },
          {
            header: t('feedback.date_column'),
            value: (item) => new Date(item.feedbackUpdatedAt ?? item.createdAt),
            numFmt: 'dd/mm/yyyy hh:mm',
            width: 20,
          },
        ],
        rows: items,
      },
    )
  }

  const columns = [
    {
      key: 'question',
      header: t('feedback.question_column'),
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
      header: t('feedback.user'),
      className: 'feedback-user-column',
      render: (item: FeedbackReportItem) => (
        <>
          <span className="user-name">{item.userDisplayName}</span>
          {item.userEmail ? <span className="user-email">{item.userEmail}</span> : null}
        </>
      ),
    },
    {
      key: 'feedback',
      header: t('feedback.title'),
      className: 'feedback-value-column feedback-value-cell',
      render: (item: FeedbackReportItem) => feedbackLabel(item.feedbackValue, t),
    },
    {
      key: 'comment',
      header: t('feedback.comment_column'),
      className: 'feedback-comment-column',
      render: (item: FeedbackReportItem) => item.feedbackComment ?? '-',
    },
    {
      key: 'cache',
      header: t('feedback.cache_column'),
      className: 'feedback-cache-column',
      render: (item: FeedbackReportItem) =>
        item.cacheHit ? t('feedback.cache_yes') : t('feedback.cache_no'),
    },
    {
      key: 'date',
      header: t('feedback.date_column'),
      className: 'feedback-date-column',
      render: (item: FeedbackReportItem) => formatDateTime(item.feedbackUpdatedAt ?? item.createdAt),
    },
  ]

  return (
    <section className={embedded ? 'audit-feedback-panel' : 'workspace'} id="feedback">
        <header className="workspace-header">
          <div>
            <p className="eyebrow">{t('feedback.eyebrow')}</p>
            {embedded ? <h2>{t('feedback.title')}</h2> : <h1>{t('feedback.title')}</h1>}
          </div>
          {items.length > 0 ? (
            <div className="workspace-actions">
              <Button className="text-button" type="button" onClick={() => void exportFeedback()}>
                <Download size={16} />
                {t('feedback.export')}
              </Button>
            </div>
          ) : null}
        </header>

        <section className="feedback-filter-grid" aria-label={t('feedback.filters_label')}>
          <label className="field filter-status">
            <span>{t('feedback.polarity')}</span>
            <select
              value={polarity}
              onChange={(event) => setPolarity(event.target.value as PolarityFilter)}
            >
              <option value="all">{t('feedback.all')}</option>
              <option value="negative">{t('feedback.negative')}</option>
            </select>
          </label>
          <label className="field">
            <span>{t('feedback.cited_document')}</span>
            <Input
              type="text"
              placeholder={t('feedback.document_id')}
              value={citedDocumentId}
              onChange={(event) => setCitedDocumentId(event.target.value)}
            />
          </label>
          <label className="field">
            <span>{t('feedback.user')}</span>
            <Input
              type="text"
              placeholder={t('feedback.user_id')}
              value={userId}
              onChange={(event) => setUserId(event.target.value)}
            />
          </label>
          <label className="field">
            <span>{t('feedback.from')}</span>
            <Input
              type="date"
              value={from}
              onChange={(event) => setFrom(event.target.value)}
            />
          </label>
          <label className="field">
            <span>{t('feedback.to')}</span>
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
            onClick={() =>
              void loadFeedback({
                negativeOnly: polarity === 'negative',
                citedDocumentId: citedDocumentId.trim(),
                userId: userId.trim(),
                from: from ? `${from}T00:00:00Z` : '',
                to: to ? `${to}T23:59:59Z` : '',
              })
            }
          >
            {t('feedback.apply_filters')}
          </Button>
        </section>

        {isLoading ? <p className="status-message">{t('feedback.loading')}</p> : null}
        {loadError ? (
          <p className="status-message error" role="alert">
            {t('feedback.load_error')}
          </p>
        ) : null}
        {hasLoadedOnce && !isLoading && items.length === 0 ? (
          <p className="status-message">{t('feedback.empty')}</p>
        ) : null}

        {items.length > 0 ? (
          <>
            <DataTable
              className="table-frame feedback-table"
              columns={columns}
              data={items.slice((page - 1) * FEEDBACK_PAGE_SIZE, page * FEEDBACK_PAGE_SIZE)}
              getRowId={(item) => item.queryAuditEventId}
            />
            {items.length > FEEDBACK_PAGE_SIZE ? (
              <div className="table-pagination">
                <Pagination
                  page={page}
                  pageCount={Math.ceil(items.length / FEEDBACK_PAGE_SIZE)}
                  onPageChange={setPage}
                />
              </div>
            ) : null}
          </>
        ) : null}
    </section>
  )
}

function feedbackLabel(value: FeedbackReportItem['feedbackValue'], t: (key: string) => string) {
  if (value === 'down') {
    return t('feedback.negative_label')
  }

  if (value === 'up') {
    return t('feedback.positive_label')
  }

  return t('feedback.no_feedback')
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
