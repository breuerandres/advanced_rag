import { parseApiError } from '../lib/api-error'

export interface DocumentTypeSummary {
  id: string
  name: string
  isActive: boolean
  sortOrder: number
}

export interface CreateDocumentTypeRequest {
  name: string
  sortOrder?: number
}

export interface UpdateDocumentTypeRequest {
  name?: string
  isActive?: boolean
  sortOrder?: number
}

let csrfToken: string | null = null

export async function listDocumentTypes(includeInactive = false): Promise<DocumentTypeSummary[]> {
  const query = includeInactive ? '?includeInactive=true' : ''
  return requestJson<DocumentTypeSummary[]>(`/api/document-types${query}`)
}

export async function createDocumentType(
  request: CreateDocumentTypeRequest,
): Promise<DocumentTypeSummary> {
  await ensureCsrfToken()
  return requestJson<DocumentTypeSummary>('/api/document-types', jsonRequest('POST', request))
}

export async function updateDocumentType(
  id: string,
  request: UpdateDocumentTypeRequest,
): Promise<DocumentTypeSummary> {
  await ensureCsrfToken()
  return requestJson<DocumentTypeSummary>(`/api/document-types/${id}`, jsonRequest('PUT', request))
}

export async function deleteDocumentType(id: string): Promise<void> {
  await ensureCsrfToken()
  await requestVoid(`/api/document-types/${id}`, {
    method: 'DELETE',
    headers: { 'X-CSRF-Token': csrfToken ?? '' },
  })
}

function jsonRequest(method: 'POST' | 'PUT', body: unknown): RequestInit {
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
  const response = await sendRequest(path, init)
  const body = await readJson(response)

  if (!response.ok) {
    throw parseApiError(response, body)
  }

  return body as T
}

async function requestVoid(path: string, init: RequestInit = {}): Promise<void> {
  const response = await sendRequest(path, init)
  if (!response.ok) {
    throw parseApiError(response, await readJson(response))
  }
}

async function sendRequest(path: string, init: RequestInit): Promise<Response> {
  const headers = new Headers(init.headers)
  for (const [key, value] of Object.entries(requestHeaders())) {
    if (!headers.has(key)) {
      headers.set(key, value)
    }
  }

  return fetch(path, {
    ...init,
    credentials: 'include',
    headers,
  })
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
