import { parseApiError } from '../lib/api-error'
import { createRequestId, ensureCsrfToken } from '../lib/csrf'

export type FeedbackValue = 'up' | 'down'

export interface DocChatResult {
  answer: string
  queryAuditEventId: string | null
  requestId: string | null
}

export interface DocChatStreamHandlers {
  onAnswerToken?: (delta: string) => void
}

export interface AskDocumentInput {
  question: string
  documentId: string
  sessionId: string
  locale?: string
}

export async function askDocument(
  input: AskDocumentInput,
  handlers: DocChatStreamHandlers = {},
): Promise<DocChatResult> {
  const csrfToken = await ensureCsrfToken()
  const response = await fetch('/api/chat', {
    method: 'POST',
    credentials: 'include',
    headers: {
      'Content-Type': 'application/json',
      'X-CSRF-Token': csrfToken,
      'X-Request-ID': createRequestId(),
    },
    body: JSON.stringify({
      question: input.question,
      documentId: input.documentId,
      sessionId: input.sessionId,
      locale: input.locale,
    }),
  })
  if (!response.ok) {
    const text = await response.text()
    throw parseApiError(response, safeJson(text))
  }

  if (!response.body) {
    return parseSseText(await response.text(), handlers)
  }

  return parseSseStream(response.body, handlers)
}

export async function submitDocFeedback(
  queryAuditEventId: string,
  value: FeedbackValue,
  comment: string,
): Promise<void> {
  const csrfToken = await ensureCsrfToken()
  const response = await fetch(`/api/feedback/${queryAuditEventId}`, {
    method: 'POST',
    credentials: 'include',
    headers: {
      'Content-Type': 'application/json',
      'X-CSRF-Token': csrfToken,
      'X-Request-ID': createRequestId(),
    },
    body: JSON.stringify({ value, comment }),
  })
  if (!response.ok) {
    const text = await response.text()
    throw parseApiError(response, safeJson(text))
  }
}

interface SseState extends DocChatResult {
  done: boolean
  handlers: DocChatStreamHandlers
}

async function parseSseStream(
  stream: ReadableStream<Uint8Array>,
  handlers: DocChatStreamHandlers,
): Promise<DocChatResult> {
  const reader = stream.getReader()
  const decoder = new TextDecoder()
  const state = createSseState(handlers)
  let buffer = ''

  while (true) {
    const { done, value } = await reader.read()
    if (done) {
      break
    }

    buffer += decoder.decode(value, { stream: true })
    buffer = processSseBuffer(buffer, state)
  }

  buffer += decoder.decode()
  buffer = processSseBuffer(buffer, state)
  if (buffer.trim().length > 0) {
    processSseEvent(buffer, state)
  }
  if (!state.done) {
    throw new Error('Doc chat stream ended before the done event.')
  }

  return toResult(state)
}

function parseSseText(text: string, handlers: DocChatStreamHandlers): DocChatResult {
  const state = createSseState(handlers)
  for (const rawEvent of text.split('\n\n').filter(Boolean)) {
    processSseEvent(rawEvent, state)
  }
  return toResult(state)
}

function createSseState(handlers: DocChatStreamHandlers): SseState {
  return {
    answer: '',
    queryAuditEventId: null,
    requestId: null,
    done: false,
    handlers,
  }
}

function processSseBuffer(buffer: string, state: SseState): string {
  const normalized = buffer.replace(/\r\n/g, '\n')
  const events = normalized.split('\n\n')
  const remainder = events.pop() ?? ''
  for (const rawEvent of events) {
    processSseEvent(rawEvent, state)
  }

  return remainder
}

function processSseEvent(rawEvent: string, state: SseState): void {
  const eventName = rawEvent.match(/^event: (.+)$/m)?.[1]
  const dataLines = rawEvent
    .split('\n')
    .filter((line) => line.startsWith('data:'))
    .map((line) => line.slice('data:'.length).trimStart())

  if (!eventName || dataLines.length === 0) {
    return
  }

  const payload = safeJson(dataLines.join('\n')) as Record<string, unknown> | null
  if (!payload) {
    return
  }

  if (eventName === 'request-id' && typeof payload.request_id === 'string') {
    state.requestId = payload.request_id
  }
  if (eventName === 'answer-token' && typeof payload.delta === 'string') {
    state.answer += payload.delta
    state.handlers.onAnswerToken?.(payload.delta)
  }
  if (eventName === 'citations' && typeof payload.query_audit_event_id === 'string') {
    state.queryAuditEventId = payload.query_audit_event_id
  }
  if (eventName === 'done') {
    state.done = true
  }
}

function toResult(state: SseState): DocChatResult {
  return {
    answer: state.answer,
    queryAuditEventId: state.queryAuditEventId,
    requestId: state.requestId,
  }
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
