import { parseApiError } from '../lib/api-error'

export interface ChatResult {
  answer: string
  queryAuditEventId: string | null
  citations: ChatCitation[]
}

export interface FeedbackResult {
  queryAuditEventId: string
  value: FeedbackValue
  comment: string | null
}

export type FeedbackValue = 'up' | 'down'

export interface ChatCitation {
  documentId: string
  instructionVersionId: string
  headingPath: string[]
}

let csrfToken: string | null = null

export async function submitQuestion(question: string): Promise<ChatResult> {
  const response = await fetch('/api/chat', {
    method: 'POST',
    credentials: 'include',
    headers: {
      'Content-Type': 'application/json',
      'X-Request-ID': createRequestId(),
    },
    body: JSON.stringify({ question }),
  })
  const text = await response.text()
  if (!response.ok) {
    throw parseApiError(response, safeJson(text))
  }

  return parseChatStream(text)
}

export async function submitFeedback(
  queryAuditEventId: string,
  value: FeedbackValue,
  comment: string,
): Promise<FeedbackResult> {
  const response = await fetch(`/api/feedback/${queryAuditEventId}`, {
    method: 'POST',
    credentials: 'include',
    headers: {
      'Content-Type': 'application/json',
      'X-Request-ID': createRequestId(),
    },
    body: JSON.stringify({ value, comment }),
  })
  const body = safeJson(await response.text())
  if (!response.ok) {
    throw parseApiError(response, body)
  }

  return body as FeedbackResult
}

export async function createViewerLink(documentId: string): Promise<string> {
  await ensureCsrfToken()
  const response = await fetch('/api/viewer/links', {
    method: 'POST',
    credentials: 'include',
    headers: {
      'Content-Type': 'application/json',
      'X-CSRF-Token': csrfToken ?? '',
      'X-Request-ID': createRequestId(),
    },
    body: JSON.stringify({ instructionId: documentId, purpose: 'chat' }),
  })
  const body = safeJson(await response.text())
  if (!response.ok) {
    throw parseApiError(response, body)
  }

  return (body as { url: string }).url
}

function parseChatStream(stream: string): ChatResult {
  let answer = ''
  let queryAuditEventId: string | null = null
  const citations: ChatCitation[] = []
  const events = stream.split('\n\n').filter(Boolean)
  for (const rawEvent of events) {
    const eventName = rawEvent.match(/^event: (.+)$/m)?.[1]
    const dataLine = rawEvent.match(/^data: (.+)$/m)?.[1]
    if (!eventName || !dataLine) {
      continue
    }
    const payload = JSON.parse(dataLine) as Record<string, unknown>
    if (eventName === 'answer-token' && typeof payload.delta === 'string') {
      answer += payload.delta
    }
    if (eventName === 'citations') {
      const auditId = payload.query_audit_event_id ?? payload.queryAuditEventId
      if (typeof auditId === 'string') {
        queryAuditEventId = auditId
      } else if (Array.isArray(payload.citations)) {
        const [first] = payload.citations as Record<string, unknown>[]
        if (typeof first?.query_audit_event_id === 'string') {
          queryAuditEventId = first.query_audit_event_id
        }
      }

      if (Array.isArray(payload.citations)) {
        for (const citation of payload.citations as Record<string, unknown>[]) {
          if (
            typeof citation.document_id === 'string' &&
            typeof citation.instruction_version_id === 'string'
          ) {
            citations.push({
              documentId: citation.document_id,
              instructionVersionId: citation.instruction_version_id,
              headingPath: Array.isArray(citation.heading_path)
                ? citation.heading_path.filter((item): item is string => typeof item === 'string')
                : [],
            })
          }
        }
      }
    }
  }
  return { answer, queryAuditEventId, citations }
}

async function ensureCsrfToken(): Promise<void> {
  const response = await fetch('/api/csrf', {
    credentials: 'include',
    headers: { 'X-Request-ID': createRequestId() },
  })
  const body = safeJson(await response.text())
  if (!response.ok) {
    throw parseApiError(response, body)
  }

  csrfToken = response.headers.get('X-CSRF-Token')
}

function safeJson(text: string): unknown {
  if (!text) {
    return null
  }
  try {
    return JSON.parse(text)
  } catch {
    return null
  }
}

function createRequestId(): string {
  if ('randomUUID' in crypto) {
    return crypto.randomUUID()
  }

  return `request-${Date.now()}`
}
