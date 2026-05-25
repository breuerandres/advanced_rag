import { ApiError, parseApiError } from '../lib/api-error'

export interface ViewerDocument {
  documentId: string
  documentVersionId: string
  title: string
  state: string
  documentType: string
  audience: string
  contentHtml: string
  tokenExpiresAt: string
}

export interface SessionUser {
  id: string
  email: string
  displayName: string
  roles: string[]
  groups: ViewerDocumentGroup[]
}

export interface SessionResponse {
  user: SessionUser
}

export interface ViewerDocumentGroup {
  id: string
  name: string
}

export interface ViewerCatalogDocument {
  id: string
  title: string
  state: string
  documentType: string
  audience: string
  allowedGroups: ViewerDocumentGroup[]
  updatedAt: string
}

export interface ViewerDocumentCatalog {
  documents: ViewerCatalogDocument[]
  groups: ViewerDocumentGroup[]
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

export async function listViewerDocuments(): Promise<ViewerDocumentCatalog> {
  return requestJson<ViewerDocumentCatalog>('/api/viewer/documents')
}

export async function createViewerLink(documentId: string, purpose: 'chat' | 'management'): Promise<string> {
  await ensureCsrfToken()
  const response = await requestJson<{ url: string }>('/api/viewer/links', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-CSRF-Token': csrfToken ?? '',
    },
    body: JSON.stringify({ documentId, purpose }),
  })
  return response.url
}

export async function exchangeViewerCode(code: string): Promise<void> {
  await ensureCsrfToken()
  await requestJson('/api/viewer/exchange', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-CSRF-Token': csrfToken ?? '',
    },
    body: JSON.stringify({ code }),
  })
}

export async function getViewerDocument(): Promise<ViewerDocument> {
  return requestJson<ViewerDocument>('/api/viewer/document')
}

export function viewerErrorMessage(error: unknown): string {
  const code = error instanceof ApiError ? error.code : 'INTERNAL_ERROR'
  const messages: Record<string, string> = {
    VIEWER_CODE_EXPIRED: 'El enlace expiro. Pedi uno nuevo desde el chat.',
    VIEWER_CODE_USED: 'Este enlace ya fue usado. Pedi uno nuevo desde el chat.',
    VIEWER_CODE_INVALID: 'El enlace no es valido. Pedi uno nuevo desde el chat.',
    AUTH_FORBIDDEN: 'No tenes permiso para abrir este documento.',
    AUTH_REQUIRED: 'La sesion del visor expiro. Volve a abrir el enlace.',
    AUTH_TOKEN_EXPIRED: 'La sesion del visor expiro. Volve a abrir el enlace.',
    AUTH_TOKEN_INVALID: 'La sesion del visor no es valida. Volve a abrir el enlace.',
    NOT_FOUND: 'No encontramos el documento solicitado.',
  }
  return messages[code] ?? 'No pudimos abrir el documento.'
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
