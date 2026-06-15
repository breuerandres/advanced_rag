import { parseApiError } from '../lib/api-error'

export interface SecretConfigurationStatus {
  name: string
  status: string
}

export interface OperationalConfiguration {
  customerTimezone: string
  llmProvider: string
  chatModel: string
  embeddingModel: string
  embeddingDimensions: number
  defaultMonthlyAiBudgetUsd: number
  semanticCacheTtlHours: number
  semanticCacheSimilarityThreshold: number
  chatMaxQuestionChars: number
  importMaxFileSizeMb: number
  secrets: SecretConfigurationStatus[]
}

export interface UpdateOperationalConfigurationRequest {
  chatModel: string
  customerTimezone: string
  defaultMonthlyAiBudgetUsd: number
  semanticCacheTtlHours: number
  semanticCacheSimilarityThreshold: number
  chatMaxQuestionChars: number
  importMaxFileSizeMb: number
}

let csrfToken: string | null = null

export async function getOperationalConfiguration(): Promise<OperationalConfiguration> {
  const response = await fetch('/api/configuration', {
    credentials: 'include',
    headers: {
      'X-Request-ID': createRequestId(),
    },
  })
  const body = await readJson(response)
  if (!response.ok) {
    throw parseApiError(response, body)
  }

  return body as OperationalConfiguration
}

export async function updateOperationalConfiguration(
  request: UpdateOperationalConfigurationRequest,
): Promise<OperationalConfiguration> {
  await ensureCsrfToken()
  return requestJson<OperationalConfiguration>('/api/configuration', jsonRequest('PUT', request))
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

function requestHeaders(): Record<string, string> {
  return {
    'X-Request-ID': createRequestId(),
  }
}

async function readJson(response: Response): Promise<unknown> {
  const text = await response.text()
  return text.length > 0 ? JSON.parse(text) : null
}

function createRequestId(): string {
  if ('randomUUID' in crypto) {
    return crypto.randomUUID()
  }

  return `request-${Date.now()}`
}
