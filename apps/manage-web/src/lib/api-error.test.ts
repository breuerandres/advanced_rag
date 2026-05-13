import { describe, expect, it } from 'vitest'
import { parseApiError } from './api-error'

describe('parseApiError', () => {
  it('extracts the shared error envelope', async () => {
    const response = new Response(null, { status: 400 })
    const error = parseApiError(response, {
      error: {
        code: 'VALIDATION_FAILED',
        message: 'Validation failed.',
        details: { field: 'question' },
        requestId: 'request-123',
      },
    })

    expect(error.code).toBe('VALIDATION_FAILED')
    expect(error.httpStatus).toBe(400)
    expect(error.requestId).toBe('request-123')
    expect(error.details).toEqual({ field: 'question' })
    expect(error.message).toBe('Validation failed.')
  })

  it('returns a safe fallback for malformed responses', async () => {
    const response = new Response(null, {
      status: 502,
      headers: { 'X-Request-ID': 'header-request-id' },
    })

    const error = parseApiError(response, { unexpected: true })

    expect(error.code).toBe('INTERNAL_ERROR')
    expect(error.httpStatus).toBe(502)
    expect(error.requestId).toBe('header-request-id')
    expect(error.details).toBeNull()
    expect(error.message).toBe('An unexpected error occurred.')
  })
})
