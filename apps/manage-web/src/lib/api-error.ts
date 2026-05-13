export interface ApiErrorEnvelope {
  error: {
    code: string
    message: string
    details?: Record<string, unknown> | null
    requestId: string
  }
}
