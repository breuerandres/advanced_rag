import { parseApiError } from './api-error'

export async function ensureCsrfToken(): Promise<string> {
  const response = await fetch('/api/csrf', {
    credentials: 'include',
    headers: { 'X-Request-ID': createRequestId() },
  })
  const text = await response.text()
  const body = text.length > 0 ? JSON.parse(text) : null
  if (!response.ok) {
    throw parseApiError(response, body)
  }

  return response.headers.get('X-CSRF-Token') ?? ''
}

export function createRequestId(): string {
  if ('randomUUID' in crypto) {
    return crypto.randomUUID()
  }

  return `request-${Date.now()}`
}
