import { parseApiError } from '../lib/api-error'

export interface ChatResult {
  answer: string
  queryAuditEventId: string | null
}

export interface FeedbackResult {
  queryAuditEventId: string
  value: FeedbackValue
  comment: string | null
}

export type FeedbackValue = 'up' | 'down'

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

function parseChatStream(stream: string): ChatResult {
  let answer = ''
  let queryAuditEventId: string | null = null
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
    }
  }
  return { answer, queryAuditEventId }
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
