import { parseApiError } from '../lib/api-error'

export interface ManagementAuditEvent {
  id: string
  actorUserId: string | null
  actorDisplayName: string | null
  eventType: string
  eventLabel: string
  entityType: string
  entityId: string | null
  details: Record<string, unknown>
  requestId: string
  createdAt: string
}

export async function listAuditEvents(): Promise<ManagementAuditEvent[]> {
  return requestJson<ManagementAuditEvent[]>('/api/audit/events?limit=100')
}

async function requestJson<T>(path: string): Promise<T> {
  const response = await fetch(path, {
    credentials: 'include',
    headers: {
      'X-Request-ID': createRequestId(),
    },
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

function createRequestId(): string {
  if ('randomUUID' in crypto) {
    return crypto.randomUUID()
  }

  return `request-${Date.now()}`
}
