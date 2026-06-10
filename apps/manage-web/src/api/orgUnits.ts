import { parseApiError } from '../lib/api-error'

export interface OrganizationalUnitSummary {
  id: string
  name: string
  parentId: string | null
  depth: number
  isActive: boolean
}

export interface CreateOrganizationalUnitRequest {
  name: string
  parentId: string
}

export interface UpdateOrganizationalUnitRequest {
  name?: string
  isActive?: boolean
}

let csrfToken: string | null = null

export async function listOrganizationalUnits(
  includeInactive = false,
): Promise<OrganizationalUnitSummary[]> {
  const query = includeInactive ? '?includeInactive=true' : ''
  return requestJson<OrganizationalUnitSummary[]>(`/api/organizational-units${query}`)
}

export async function createOrganizationalUnit(
  request: CreateOrganizationalUnitRequest,
): Promise<OrganizationalUnitSummary> {
  await ensureCsrfToken()
  return requestJson<OrganizationalUnitSummary>(
    '/api/organizational-units',
    jsonRequest('POST', request),
  )
}

export async function updateOrganizationalUnit(
  id: string,
  request: UpdateOrganizationalUnitRequest,
): Promise<OrganizationalUnitSummary> {
  await ensureCsrfToken()
  return requestJson<OrganizationalUnitSummary>(
    `/api/organizational-units/${id}`,
    jsonRequest('PATCH', request),
  )
}

function jsonRequest(method: 'PATCH' | 'POST', body: unknown): RequestInit {
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
