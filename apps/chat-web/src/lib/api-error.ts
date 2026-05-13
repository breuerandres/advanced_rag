export interface ApiErrorEnvelope {
  error: {
    code: string
    message: string
    details?: Record<string, unknown> | null
    requestId: string
  }
}

export class ApiError extends Error {
  readonly code: string
  readonly httpStatus: number
  readonly requestId: string
  readonly details: Record<string, unknown> | null

  constructor(params: {
    code: string
    httpStatus: number
    requestId: string
    details: Record<string, unknown> | null
    message: string
  }) {
    super(params.message)
    this.name = 'ApiError'
    this.code = params.code
    this.httpStatus = params.httpStatus
    this.requestId = params.requestId
    this.details = params.details
  }
}

export function parseApiError(response: Response, body: unknown): ApiError {
  if (isApiErrorEnvelope(body)) {
    return new ApiError({
      code: body.error.code,
      httpStatus: response.status,
      requestId: body.error.requestId,
      details: body.error.details ?? null,
      message: body.error.message,
    })
  }

  return new ApiError({
    code: 'INTERNAL_ERROR',
    httpStatus: response.status,
    requestId:
      response.headers.get('X-Request-ID') ??
      response.headers.get('X-Request-Id') ??
      'unknown',
    details: null,
    message: 'An unexpected error occurred.',
  })
}

function isApiErrorEnvelope(value: unknown): value is ApiErrorEnvelope {
  if (!isRecord(value) || !isRecord(value.error)) {
    return false
  }

  const { code, message, details, requestId } = value.error
  const hasValidDetails =
    details === undefined || details === null || isRecord(details)

  return (
    typeof code === 'string' &&
    typeof message === 'string' &&
    typeof requestId === 'string' &&
    hasValidDetails
  )
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}
