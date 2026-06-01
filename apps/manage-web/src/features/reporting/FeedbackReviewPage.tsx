import { useCallback, useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Download } from 'lucide-react'
import { Button, DataTable, Input } from '@helpcenter/shared-ui'
import {
  listFeedbackReport,
  type FeedbackReportFilters,
  type FeedbackReportItem,
} from '../../api/reporting'

type PolarityFilter = 'all' | 'negative'

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

  const loadFeedback = useCallback(async (filters: FeedbackReportFilters) => {
    setIsLoading(true)
    setLoadError(false)
    try {
      const rows = await listFeedbackReport(filters)
      setItems(rows)
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
        item.feedbackValue ?? '',
        item.feedbackComment ?? '',
        item.feedbackUpdatedAt ?? '',
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
      render: (item: FeedbackReportItem) => item.userDisplayName,
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
              <Button className="text-button" type="button" onClick={exportToExcelCsv}>
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
