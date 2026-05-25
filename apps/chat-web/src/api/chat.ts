import { parseApiError } from '../lib/api-error'

export interface ChatResult {
  answer: string
  queryAuditEventId: string | null
  citations: ChatCitation[]
  cacheHit: boolean
  requestId: string | null
  usage: ChatUsage | null
}

export interface FeedbackResult {
  queryAuditEventId: string
  value: FeedbackValue
  comment: string | null
}

export type FeedbackValue = 'up' | 'down'

export interface ChatCitation {
  documentId: string
  documentVersionId: string
  headingPath: string[]
}

export interface ChatUsage {
  inputTokens: number
  cachedTokens: number
  outputTokens: number
  costUsd: number
}

export interface SessionUser {
  id: string
  email: string
  displayName: string
  roles: string[]
  groups: Array<{ id: string; name: string }>
}

export interface SessionResponse {
  user: SessionUser
}

let csrfToken: string | null = null

export async function getSession(): Promise<SessionResponse> {
  return requestJson<SessionResponse>('/api/session')
}

export async function login(email: string, password: string): Promise<SessionResponse> {
  await ensureCsrfToken()
  return requestJson<SessionResponse>('/api/auth/login', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-CSRF-Token': csrfToken ?? '',
    },
    body: JSON.stringify({ email, password }),
  })
}

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

export async function renewChatToken(): Promise<void> {
  await ensureCsrfToken()
  await requestJson('/api/auth/chat-token', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-CSRF-Token': csrfToken ?? '',
    },
  })
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
    body: JSON.stringify({ documentId: documentId, purpose: 'chat' }),
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
  let cacheHit = false
  let requestId: string | null = null
  let usage: ChatUsage | null = null
  const citations: ChatCitation[] = []
  const events = stream.split('\n\n').filter(Boolean)
  for (const rawEvent of events) {
    const eventName = rawEvent.match(/^event: (.+)$/m)?.[1]
    const dataLine = rawEvent.match(/^data: (.+)$/m)?.[1]
    if (!eventName || !dataLine) {
      continue
    }
    const payload = JSON.parse(dataLine) as Record<string, unknown>
    if (eventName === 'request-id' && typeof payload.request_id === 'string') {
      requestId = payload.request_id
    }
    if (eventName === 'cache-hit') {
      cacheHit = true
    }
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
            typeof citation.document_version_id === 'string'
          ) {
            citations.push({
              documentId: citation.document_id,
              documentVersionId: citation.document_version_id,
              headingPath: Array.isArray(citation.heading_path)
                ? citation.heading_path.filter((item): item is string => typeof item === 'string')
                : [],
            })
          }
        }
      }
    }
    if (eventName === 'usage') {
      usage = {
        inputTokens: numberValue(payload.input_tokens),
        cachedTokens: numberValue(payload.cached_tokens),
        outputTokens: numberValue(payload.output_tokens),
        costUsd: numberValue(payload.cost_usd),
      }
    }
  }
  return { answer, queryAuditEventId, citations, cacheHit, requestId, usage }
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

async function requestJson<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers)
  if (!headers.has('X-Request-ID')) {
    headers.set('X-Request-ID', createRequestId())
  }

  const response = await fetch(path, {
    ...init,
    credentials: 'include',
    headers,
  })
  const body = safeJson(await response.text())
  if (!response.ok) {
    throw parseApiError(response, body)
  }

  return body as T
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

function numberValue(value: unknown): number {
  return typeof value === 'number' && Number.isFinite(value) ? value : 0
}
