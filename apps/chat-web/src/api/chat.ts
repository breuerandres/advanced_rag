import { ApiError, parseApiError } from '../lib/api-error'

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
  chunkId?: string
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

export interface ChatSessionSummary {
  sessionId: string
  title: string
  lastQuestion: string
  lastAnswer: string
  lastActivityAt: string
  turnCount: number
}

export interface ChatSessionListResponse {
  sessions: ChatSessionSummary[]
}

export interface ChatSessionTurn {
  queryAuditEventId: string
  question: string
  answer: string
  createdAt: string
  cacheHit: boolean
  feedbackValue: FeedbackValue | null
  feedbackComment: string | null
  citations: ChatCitation[]
}

export interface ChatSessionHistoryResponse {
  sessionId: string
  turns: ChatSessionTurn[]
}

export interface SubmitQuestionInput {
  question: string
  sessionId: string
  locale?: string
}

export interface ChatStreamHandlers {
  onAnswerToken?: (delta: string) => void
}

let csrfToken: string | null = null

export async function getSession(): Promise<SessionResponse> {
  csrfToken = null
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

export async function logout(): Promise<void> {
  await ensureCsrfToken()
  await requestJson('/api/auth/logout', {
    method: 'POST',
    headers: {
      'X-CSRF-Token': csrfToken ?? '',
    },
  })
  csrfToken = null
}

export async function listChatSessions(): Promise<ChatSessionListResponse> {
  return requestJson<ChatSessionListResponse>('/api/chat/sessions')
}

export async function getChatSession(sessionId: string): Promise<ChatSessionHistoryResponse> {
  return requestJson<ChatSessionHistoryResponse>(`/api/chat/sessions/${sessionId}`)
}

export async function submitQuestion(
  input: SubmitQuestionInput,
  handlers: ChatStreamHandlers = {},
): Promise<ChatResult> {
  await ensureCsrfToken()
  const response = await fetch('/api/chat', {
    method: 'POST',
    credentials: 'include',
    headers: {
      'Content-Type': 'application/json',
      'X-CSRF-Token': csrfToken ?? '',
      'X-Request-ID': createRequestId(),
    },
    body: JSON.stringify({
      question: input.question,
      sessionId: input.sessionId,
      locale: input.locale,
    }),
  })
  if (!response.ok) {
    const text = await response.text()
    throw parseApiError(response, safeJson(text))
  }

  if (!response.body) {
    return parseChatStream(await response.text(), handlers)
  }

  return parseChatReadableStream(response.body, handlers)
}

export async function submitFeedback(
  queryAuditEventId: string,
  value: FeedbackValue,
  comment: string,
): Promise<FeedbackResult> {
  await ensureCsrfToken()
  const response = await fetch(`/api/feedback/${queryAuditEventId}`, {
    method: 'POST',
    credentials: 'include',
    headers: {
      'Content-Type': 'application/json',
      'X-CSRF-Token': csrfToken ?? '',
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

async function parseChatReadableStream(
  stream: ReadableStream<Uint8Array>,
  handlers: ChatStreamHandlers,
): Promise<ChatResult> {
  const reader = stream.getReader()
  const decoder = new TextDecoder()
  const state = createChatStreamState(handlers)
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
    throw new Error('Chat stream ended before the done event.')
  }

  return toChatResult(state)
}

function parseChatStream(stream: string, handlers: ChatStreamHandlers = {}): ChatResult {
  const state = createChatStreamState(handlers)
  const events = stream.split('\n\n').filter(Boolean)
  for (const rawEvent of events) {
    processSseEvent(rawEvent, state)
  }
  return toChatResult(state)
}

interface ChatStreamState extends ChatResult {
  done: boolean
  handlers: ChatStreamHandlers
}

function createChatStreamState(handlers: ChatStreamHandlers): ChatStreamState {
  return {
    answer: '',
    queryAuditEventId: null,
    citations: [],
    cacheHit: false,
    requestId: null,
    usage: null,
    done: false,
    handlers,
  }
}

function processSseBuffer(buffer: string, state: ChatStreamState): string {
  const normalized = buffer.replace(/\r\n/g, '\n')
  const events = normalized.split('\n\n')
  const remainder = events.pop() ?? ''
  for (const rawEvent of events) {
    processSseEvent(rawEvent, state)
  }

  return remainder
}

function processSseEvent(rawEvent: string, state: ChatStreamState): void {
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
  if (eventName === 'cache-hit') {
    state.cacheHit = true
  }
  if (eventName === 'answer-token' && typeof payload.delta === 'string') {
    state.answer += payload.delta
    state.handlers.onAnswerToken?.(payload.delta)
  }
  if (eventName === 'citations') {
    applyCitationsEvent(payload, state)
  }
  if (eventName === 'usage') {
    state.usage = {
      inputTokens: numberValue(payload.input_tokens),
      cachedTokens: numberValue(payload.cached_tokens),
      outputTokens: numberValue(payload.output_tokens),
      costUsd: numberValue(payload.cost_usd),
    }
  }
  if (eventName === 'done') {
    state.done = true
  }
  if (eventName === 'error') {
    throw apiErrorFromSse(payload, state.requestId)
  }
}

function applyCitationsEvent(payload: Record<string, unknown>, state: ChatStreamState): void {
  const auditId = payload.query_audit_event_id ?? payload.queryAuditEventId
  if (typeof auditId === 'string') {
    state.queryAuditEventId = auditId
  } else if (Array.isArray(payload.citations)) {
    const [first] = payload.citations as Record<string, unknown>[]
    if (typeof first?.query_audit_event_id === 'string') {
      state.queryAuditEventId = first.query_audit_event_id
    }
  }

  if (!Array.isArray(payload.citations)) {
    return
  }

  for (const citation of payload.citations as Record<string, unknown>[]) {
    const headingPath = citation.heading_path ?? citation.headingPath
    if (
      typeof (citation.document_id ?? citation.documentId) === 'string' &&
      typeof (citation.document_version_id ?? citation.documentVersionId) === 'string'
    ) {
      state.citations.push({
        chunkId:
          typeof (citation.chunk_id ?? citation.chunkId) === 'string'
            ? String(citation.chunk_id ?? citation.chunkId)
            : undefined,
        documentId: String(citation.document_id ?? citation.documentId),
        documentVersionId: String(citation.document_version_id ?? citation.documentVersionId),
        headingPath: Array.isArray(headingPath)
          ? headingPath.filter((item): item is string => typeof item === 'string')
          : [],
      })
    }
  }
}

function apiErrorFromSse(payload: Record<string, unknown>, fallbackRequestId: string | null): ApiError {
  const error = isRecord(payload.error) ? payload.error : {}
  return new ApiError({
    code: typeof error.code === 'string' ? error.code : 'INTERNAL_ERROR',
    httpStatus: 500,
    requestId:
      typeof error.request_id === 'string'
        ? error.request_id
        : typeof error.requestId === 'string'
          ? error.requestId
          : fallbackRequestId ?? 'unknown',
    details: isRecord(error.details) ? error.details : null,
    message: typeof error.message === 'string' ? error.message : 'An unexpected error occurred.',
  })
}

function toChatResult(state: ChatStreamState): ChatResult {
  return {
    answer: state.answer,
    queryAuditEventId: state.queryAuditEventId,
    citations: state.citations,
    cacheHit: state.cacheHit,
    requestId: state.requestId,
    usage: state.usage,
  }
}

async function ensureCsrfToken(): Promise<void> {
  if (csrfToken) {
    return
  }

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

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
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
