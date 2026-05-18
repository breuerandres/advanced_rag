import { parseApiError } from '../lib/api-error'

export interface SecretConfigurationStatus {
  name: string
  status: string
}

export interface OperationalConfiguration {
  customerTimezone: string
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
