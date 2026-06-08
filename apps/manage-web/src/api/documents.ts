import { parseApiError } from '../lib/api-error'

export interface DocumentAccessRule {
  id: string
  organizationalUnitId: string | null
  groupIds: string[]
}

export interface DocumentAccessRuleInput {
  organizationalUnitId: string | null
  groupIds: string[]
}

export interface DocumentSummary {
  id: string
  title: string
  state: string
  documentType: string
  audience: string
  allowedGroupIds: string[]
  accessRules?: DocumentAccessRule[]
  draftVersionNumber: number | null
  publishedVersionNumber: number | null
  indexingStatus: string
  updatedAt: string
}

export interface DocumentVersion {
  id: string
  versionNumber: number
  state: string
  title: string
  documentType: string
  audience: string
  contentHtml: string
  indexingStatus: string
}

export interface DocumentDetail {
  id: string
  title: string
  state: string
  currentDraftVersion: DocumentVersion | null
  currentPublishedVersion: DocumentVersion | null
  allowedGroupIds: string[]
  accessRules?: DocumentAccessRule[]
  updatedAt: string
}

export interface SaveDocumentDraftRequest {
  title: string
  documentType: string
  audience: string
  contentHtml: string
  accessRules: DocumentAccessRuleInput[]
}

export interface ImportExtractionResult {
  text: string
  contentHtml?: string | null
  metadata: {
    originalFilename: string
    mimeType: string
    sizeBytes: number
    sha256Hash: string
    extractionStatus: string
  }
}

export interface DocumentImageUploadResult {
  imageId: string
  url: string
  altText: string
}

export interface ViewerLinkResult {
  url: string
  expiresAt: string
}

let csrfToken: string | null = null

export async function listDocuments(): Promise<DocumentSummary[]> {
  return requestJson<DocumentSummary[]>('/api/documents')
}

export async function getDocument(id: string): Promise<DocumentDetail> {
  return requestJson<DocumentDetail>(`/api/documents/${id}`)
}

export async function createDocumentDraft(
  request: SaveDocumentDraftRequest,
): Promise<DocumentDetail> {
  await ensureCsrfToken()
  return requestJson<DocumentDetail>('/api/documents', jsonInit('POST', request))
}

export async function saveDocumentDraft(
  id: string,
  request: SaveDocumentDraftRequest,
): Promise<DocumentDetail> {
  await ensureCsrfToken()
  return requestJson<DocumentDetail>(`/api/documents/${id}/draft`, jsonInit('PUT', request))
}

export async function sendDocumentToReview(id: string): Promise<DocumentDetail> {
  await ensureCsrfToken()
  return requestJson<DocumentDetail>(
    `/api/documents/${id}/send-to-review`,
    jsonInit('POST', { comment: null }),
  )
}

export async function requestPublish(id: string): Promise<DocumentDetail> {
  await ensureCsrfToken()
  return requestJson<DocumentDetail>(`/api/documents/${id}/request-publish`, {
    method: 'POST',
    headers: csrfHeaders(),
  })
}

export async function archiveDocument(id: string): Promise<DocumentDetail> {
  await ensureCsrfToken()
  return requestJson<DocumentDetail>(`/api/documents/${id}/archive`, { method: 'POST', headers: csrfHeaders() })
}

export async function restoreDocument(id: string): Promise<DocumentDetail> {
  await ensureCsrfToken()
  return requestJson<DocumentDetail>(`/api/documents/${id}/restore`, { method: 'POST', headers: csrfHeaders() })
}

export async function importDocumentText(file: File): Promise<ImportExtractionResult> {
  await ensureCsrfToken()
  const form = new FormData()
  form.append('file', file)
  return requestJson<ImportExtractionResult>('/api/documents/imports/extract', {
    method: 'POST',
    headers: csrfHeaders(),
    body: form,
  })
}

export async function uploadDocumentImage(
  documentId: string,
  file: File,
  altText: string,
): Promise<DocumentImageUploadResult> {
  await ensureCsrfToken()
  const form = new FormData()
  form.append('file', file)
  form.append('altText', altText)
  return requestJson<DocumentImageUploadResult>(`/api/documents/${documentId}/images`, {
    method: 'POST',
    headers: csrfHeaders(),
    body: form,
  })
}

export async function createManagementViewerLink(id: string): Promise<ViewerLinkResult> {
  await ensureCsrfToken()
  return requestJson<ViewerLinkResult>(
    '/api/viewer/links',
    jsonInit('POST', { documentId: id, purpose: 'management' }),
  )
}

function jsonInit(method: string, body: unknown): RequestInit {
  return {
    method,
    headers: {
      'Content-Type': 'application/json',
      ...csrfHeaders(),
    },
    body: JSON.stringify(body),
  }
}

function csrfHeaders(): Record<string, string> {
  return {
    'X-CSRF-Token': csrfToken ?? '',
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
