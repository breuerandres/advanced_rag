import { parseApiError } from '../lib/api-error'

export interface FeedbackReportCitation {
  documentId: string
  documentVersionId: string
  headingPath: string[]
}

export interface FeedbackReportItem {
  queryAuditEventId: string
  userId: string
  userDisplayName: string
  question: string
  answerSummary: string
  feedbackValue: 'up' | 'down'
  feedbackComment: string | null
  feedbackUpdatedAt: string
  createdAt: string
  cacheHit: boolean
  requestId: string
  citations: FeedbackReportCitation[]
}

export interface FeedbackReportFilters {
  negativeOnly?: boolean
  citedDocumentId?: string
  userId?: string
  from?: string
  to?: string
}

export async function listFeedbackReport(
  filters: FeedbackReportFilters = {},
): Promise<FeedbackReportItem[]> {
  const params = new URLSearchParams()
  if (filters.negativeOnly) {
    params.set('negativeOnly', 'true')
  }
  if (filters.citedDocumentId) {
    params.set('citedDocumentId', filters.citedDocumentId)
  }
  if (filters.userId) {
    params.set('userId', filters.userId)
  }
  if (filters.from) {
    params.set('from', filters.from)
  }
  if (filters.to) {
    params.set('to', filters.to)
  }
  const suffix = params.toString() ? `?${params}` : ''
  const response = await fetch(`/api/reporting/feedback${suffix}`, {
    credentials: 'include',
    headers: {
      'X-Request-ID': createRequestId(),
    },
  })
  const body = await readJson(response)
  if (!response.ok) {
    throw parseApiError(response, body)
  }

  return body as FeedbackReportItem[]
}

async function readJson(response: Response): Promise<unknown> {
  const text = await response.text()
  return text.length > 0 ? JSON.parse(text) : null
}

function createRequestId(): string {
  if ('randomUUID' in crypto) {
    return crypto.randomUUID()
  }

  return `request-${Date.now()}`
}
