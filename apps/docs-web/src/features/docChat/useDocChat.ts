import { useCallback, useRef, useState } from 'react'
import { askDocument, submitDocFeedback, type FeedbackValue } from '../../api/docChat'
import { ApiError } from '../../lib/api-error'

export interface DocChatFeedback {
  value: FeedbackValue | null
  comment: string
  status: 'idle' | 'sending' | 'sent' | 'error'
}

export interface DocChatTurn {
  key: string
  question: string
  answer: string
  status: 'pending' | 'streaming' | 'done'
  queryAuditEventId: string | null
  feedback: DocChatFeedback
}

export interface DocChatFailure {
  messageKey: string
  question: string
}

const INITIAL_FEEDBACK: DocChatFeedback = { value: null, comment: '', status: 'idle' }

export function useDocChat(documentId: string, locale: string) {
  const [turns, setTurns] = useState<DocChatTurn[]>([])
  const [isStreaming, setIsStreaming] = useState(false)
  const [failure, setFailure] = useState<DocChatFailure | null>(null)
  const sessionIdRef = useRef(createId())

  const ask = useCallback(
    async (question: string) => {
      const key = createId()
      setFailure(null)
      setIsStreaming(true)
      setTurns((current) => [
        ...current,
        {
          key,
          question,
          answer: '',
          status: 'pending',
          queryAuditEventId: null,
          feedback: INITIAL_FEEDBACK,
        },
      ])
      try {
        const result = await askDocument(
          { question, documentId, sessionId: sessionIdRef.current, locale },
          {
            onAnswerToken: (delta) => {
              setTurns((current) =>
                current.map((turn) =>
                  turn.key === key
                    ? { ...turn, status: 'streaming', answer: turn.answer + delta }
                    : turn,
                ),
              )
            },
          },
        )
        setTurns((current) =>
          current.map((turn) =>
            turn.key === key
              ? {
                  ...turn,
                  status: 'done',
                  answer: result.answer,
                  queryAuditEventId: result.queryAuditEventId,
                }
              : turn,
          ),
        )
      } catch (error) {
        // Roll back the partial turn; the failed question stays available for retry.
        setTurns((current) => current.filter((turn) => turn.key !== key))
        setFailure({ messageKey: chatErrorKey(error), question })
      } finally {
        setIsStreaming(false)
      }
    },
    [documentId, locale],
  )

  const retry = useCallback(() => {
    if (failure) {
      void ask(failure.question)
    }
  }, [ask, failure])

  const setFeedbackValue = useCallback((key: string, value: FeedbackValue) => {
    setTurns((current) =>
      current.map((turn) =>
        turn.key === key
          ? {
              ...turn,
              feedback: {
                ...turn.feedback,
                value: turn.feedback.value === value ? null : value,
              },
            }
          : turn,
      ),
    )
  }, [])

  const setFeedbackComment = useCallback((key: string, comment: string) => {
    setTurns((current) =>
      current.map((turn) =>
        turn.key === key ? { ...turn, feedback: { ...turn.feedback, comment } } : turn,
      ),
    )
  }, [])

  const sendFeedback = useCallback(
    async (key: string) => {
      const turn = turns.find((item) => item.key === key)
      if (!turn || !turn.queryAuditEventId || !turn.feedback.value) {
        return
      }
      setTurns((current) =>
        current.map((item) =>
          item.key === key ? { ...item, feedback: { ...item.feedback, status: 'sending' } } : item,
        ),
      )
      try {
        await submitDocFeedback(turn.queryAuditEventId, turn.feedback.value, turn.feedback.comment)
        setTurns((current) =>
          current.map((item) =>
            item.key === key ? { ...item, feedback: { ...item.feedback, status: 'sent' } } : item,
          ),
        )
      } catch {
        setTurns((current) =>
          current.map((item) =>
            item.key === key ? { ...item, feedback: { ...item.feedback, status: 'error' } } : item,
          ),
        )
      }
    },
    [turns],
  )

  return { turns, isStreaming, failure, ask, retry, setFeedbackValue, setFeedbackComment, sendFeedback }
}

function chatErrorKey(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.code === 'AI_BUDGET_EXCEEDED') {
      return 'docChat.error_budget'
    }
    if (error.code === 'CHAT_RATE_LIMITED') {
      return 'docChat.error_rate_limited'
    }
    if (error.code === 'AUTH_REQUIRED') {
      return 'errors.auth_required'
    }
  }
  return 'docChat.error_generic'
}

function createId(): string {
  if ('randomUUID' in crypto) {
    return crypto.randomUUID()
  }
  return `00000000-0000-4000-8000-${Date.now().toString().padStart(12, '0').slice(-12)}`
}
