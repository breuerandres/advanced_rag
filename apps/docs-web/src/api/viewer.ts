import { ApiError, parseApiError } from '../lib/api-error'
import { createRequestId, ensureCsrfToken } from '../lib/csrf'

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

export async function getSession(): Promise<SessionResponse> {
  return requestJson<SessionResponse>('/api/session')
}

export async function login(email: string, password: string): Promise<SessionResponse> {
  const csrfToken = await ensureCsrfToken()
  return requestJson<SessionResponse>('/api/auth/login', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-CSRF-Token': csrfToken,
    },
    body: JSON.stringify({ email, password }),
  })
}

export async function listViewerDocuments(): Promise<ViewerDocumentCatalog> {
  return requestJson<ViewerDocumentCatalog>('/api/viewer/documents')
}

export async function createViewerLink(documentId: string, purpose: 'chat' | 'management'): Promise<string> {
  const csrfToken = await ensureCsrfToken()
  const response = await requestJson<{ url: string }>('/api/viewer/links', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-CSRF-Token': csrfToken,
    },
    body: JSON.stringify({ documentId, purpose }),
  })
  return response.url
}

export async function consumeViewerHandoff(
  handoffCode: string,
  documentId: string,
): Promise<SessionResponse> {
  const csrfToken = await ensureCsrfToken()
  return requestJson<SessionResponse>('/api/viewer/session-handoff', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-CSRF-Token': csrfToken,
    },
    body: JSON.stringify({ documentId, handoffCode }),
  })
}

export async function consumeSessionHandoff(handoffCode: string): Promise<SessionResponse> {
  const csrfToken = await ensureCsrfToken()
  return requestJson<SessionResponse>('/api/auth/session-handoffs/consume', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-CSRF-Token': csrfToken,
    },
    body: JSON.stringify({ handoffCode, target: 'docs' }),
  })
}

export async function getViewerDocument(documentId: string): Promise<ViewerDocument> {
  return requestJson<ViewerDocument>(`/api/viewer/document?documentId=${encodeURIComponent(documentId)}`)
}

export function viewerErrorKey(error: unknown): string {
  const code = error instanceof ApiError ? error.code : 'INTERNAL_ERROR'
  const keys: Record<string, string> = {
    AUTH_FORBIDDEN: 'errors.forbidden',
    AUTH_REQUIRED: 'errors.auth_required',
    NOT_FOUND: 'errors.not_found',
    VIEWER_HANDOFF_EXPIRED: 'errors.handoff_expired',
    VIEWER_HANDOFF_INVALID: 'errors.handoff_invalid',
    VIEWER_HANDOFF_USED: 'errors.handoff_used',
  }
  return keys[code] ?? 'errors.generic'
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
