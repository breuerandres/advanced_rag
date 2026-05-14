import { parseApiError } from '../lib/api-error'

export interface GroupSummary {
  id: string
  name: string
}

export interface UserSummary {
  id: string
  email: string
  displayName: string
  isActive: boolean
  roles: string[]
  groups: GroupSummary[]
  accessScopeHash: string
  monthlyBudgetUsd: number | null
  currentSpendUsd: number
  remainingBudgetUsd: number | null
  isBudgetDisabled: boolean
}

export interface SetAiBudgetRequest {
  monthlyBudgetUsd: number | null
  isDisabled: boolean
}

let csrfToken: string | null = null

export async function listUsers(): Promise<UserSummary[]> {
  return requestJson<UserSummary[]>('/api/users')
}

export async function listGroups(): Promise<GroupSummary[]> {
  return requestJson<GroupSummary[]>('/api/groups')
}

export async function updateUserBudget(
  userId: string,
  request: SetAiBudgetRequest,
): Promise<UserSummary> {
  await ensureCsrfToken()

  return requestJson<UserSummary>(`/api/users/${userId}/ai-budget`, {
    method: 'PUT',
    headers: {
      'Content-Type': 'application/json',
      'X-CSRF-Token': csrfToken ?? '',
    },
    body: JSON.stringify(request),
  })
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
