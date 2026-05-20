import { parseApiError } from '../lib/api-error'

export interface SetupStatus {
  setupRequired: boolean
  adminExists: boolean
  databaseReady: boolean
  requiredRoles: string[]
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

export interface CreateFirstAdminRequest {
  email: string
  displayName: string
  password: string
}

export interface LoginRequest {
  email: string
  password: string
}

let csrfToken: string | null = null

export async function getSetupStatus(): Promise<SetupStatus> {
  return requestJson<SetupStatus>('/api/setup/status')
}

export async function getSession(): Promise<SessionResponse> {
  return requestJson<SessionResponse>('/api/session')
}

export async function createFirstAdmin(request: CreateFirstAdminRequest): Promise<void> {
  await ensureCsrfToken()
  await requestJson('/api/setup/admin', jsonInit('POST', request))
}

export async function login(request: LoginRequest): Promise<SessionResponse> {
  await ensureCsrfToken()
  return requestJson<SessionResponse>('/api/auth/login', jsonInit('POST', request))
}

export async function logout(): Promise<void> {
  await ensureCsrfToken()
  await requestJson('/api/auth/logout', jsonInit('POST', {}))
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

function jsonInit(method: string, body: unknown): RequestInit {
  return {
    method,
    headers: {
      'Content-Type': 'application/json',
      'X-CSRF-Token': csrfToken ?? '',
    },
    body: JSON.stringify(body),
  }
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
