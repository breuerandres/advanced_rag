import { parseApiError } from '../lib/api-error'
import type { SessionResponse } from './auth'

export interface UpdateEmailRequest {
  email: string
}

export interface ChangePasswordRequest {
  currentPassword: string
  newPassword: string
}

let csrfToken: string | null = null

export async function updateCurrentUserEmail(request: UpdateEmailRequest): Promise<SessionResponse> {
  await ensureCsrfToken()
  return requestJson<SessionResponse>('/api/account/email', jsonRequest('PUT', request))
}

export async function changeCurrentUserPassword(request: ChangePasswordRequest): Promise<void> {
  await ensureCsrfToken()
  await requestJson('/api/account/password', jsonRequest('PUT', request))
}

function jsonRequest(method: 'PUT', body: unknown): RequestInit {
  return {
    method,
    headers: {
      'Content-Type': 'application/json',
      'X-CSRF-Token': csrfToken ?? '',
    },
    body: JSON.stringify(body),
  }
}

async function ensureCsrfToken(): Promise<void> {
  const response = await fetch('/api/csrf', {
    credentials: 'include',
    headers: requestHeaders(),
  })
  const body = await readJson(response)
  if (!response.ok) {
    throw parseApiError(response, body)
  }

  csrfToken = response.headers.get('X-CSRF-Token')
}

async function requestJson<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers)
  for (const [key, value] of Object.entries(requestHeaders())) {
    if (!headers.has(key)) {
      headers.set(key, value)
    }
  }

  const response = await fetch(path, {
    ...init,
    credentials: 'include',
    headers,
  })
  const body = await readJson(response)

  if (!response.ok) {
    throw parseApiError(response, body)
  }

  return body as T
}

async function readJson(response: Response): Promise<unknown> {
  const text = await response.text()
  return text.length > 0 ? JSON.parse(text) : null
}

function requestHeaders(): Record<string, string> {
  return {
    'X-Request-ID': createRequestId(),
  }
}

function createRequestId(): string {
  if ('randomUUID' in crypto) {
    return crypto.randomUUID()
  }

  return `request-${Date.now()}`
}
