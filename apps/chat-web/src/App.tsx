import { useState, type FormEvent } from 'react'
import {
  AlertCircle,
  Clock3,
  ExternalLink,
  MessageSquareText,
  Send,
  ShieldCheck,
  ThumbsDown,
  ThumbsUp,
} from 'lucide-react'
import {
  createViewerLink,
  renewChatToken,
  submitFeedback,
  submitQuestion,
  type ChatCitation,
  type ChatResult,
  type ChatUsage,
  type FeedbackValue,
} from './api/chat'
import { ApiError } from './lib/api-error'
import './App.css'

type ChatStatus = 'idle' | 'submitting'
const MaxQuestionChars = 4000

interface ChatErrorState {
  title: string
  detail?: string
  requestId?: string
  kind: 'generic' | 'budget' | 'auth'
}

export default function App() {
  const [question, setQuestion] = useState('')
  const [answer, setAnswer] = useState('')
  const [queryAuditEventId, setQueryAuditEventId] = useState<string | null>(null)
  const [citations, setCitations] = useState<ChatCitation[]>([])
  const [usage, setUsage] = useState<ChatUsage | null>(null)
  const [cacheHit, setCacheHit] = useState(false)
  const [feedbackValue, setFeedbackValue] = useState<FeedbackValue | null>(null)
  const [comment, setComment] = useState('')
  const [feedbackSubmitted, setFeedbackSubmitted] = useState(false)
  const [status, setStatus] = useState<ChatStatus>('idle')
  const [isSendingFeedback, setIsSendingFeedback] = useState(false)
  const [error, setError] = useState<ChatErrorState | null>(null)

  const normalizedQuestion = question.trim()
  const isSubmitting = status === 'submitting'

  async function handleAsk(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!normalizedQuestion) {
      return
    }

    setStatus('submitting')
    setError(null)
    setAnswer('')
    setCitations([])
    setUsage(null)
    setCacheHit(false)
    setFeedbackValue(null)
    setFeedbackSubmitted(false)
    setComment('')
    try {
      const result = await askWithTokenRenewal(normalizedQuestion)
      setAnswer(result.answer)
      setQueryAuditEventId(result.queryAuditEventId)
      setCitations(result.citations)
      setCacheHit(result.cacheHit)
      setUsage(result.usage)
    } catch (caught) {
      setError(toChatError(caught))
    } finally {
      setStatus('idle')
    }
  }

  async function openCitation(citation: ChatCitation) {
    setError(null)
    try {
      const url = await createViewerLink(citation.documentId)
      window.location.assign(url)
    } catch (caught) {
      setError(toCitationError(caught))
    }
  }

  async function handleFeedback(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!queryAuditEventId || !feedbackValue) {
      return
    }

    setIsSendingFeedback(true)
    setError(null)
    try {
      await submitFeedback(queryAuditEventId, feedbackValue, comment)
      setFeedbackSubmitted(true)
    } catch (caught) {
      setError(toFeedbackError(caught))
    } finally {
      setIsSendingFeedback(false)
    }
  }

  return (
    <main className="chat-shell">
      <header className="chat-header">
        <div>
          <p className="eyebrow">Advanced RAG</p>
          <h1>Chat de instrucciones</h1>
          <p className="header-copy">Hacé una pregunta sobre las instrucciones publicadas.</p>
        </div>
        <div className="chat-status-strip" aria-label="Estado del chat">
          <span>
            <ShieldCheck size={16} aria-hidden="true" />
            Token de chat temporal
          </span>
          <span>
            <Clock3 size={16} aria-hidden="true" />
            Citas trazables
          </span>
        </div>
      </header>

      <section className="chat-panel" aria-label="Chat de instrucciones">
        <form className="question-form" onSubmit={handleAsk}>
          <label className="field">
            <span>Pregunta</span>
            <textarea
              maxLength={MaxQuestionChars}
              value={question}
              onChange={(event) => setQuestion(event.target.value)}
              disabled={isSubmitting}
              placeholder="Escribí tu consulta..."
            />
          </label>
          <div className="question-form-footer">
            <span>
              {question.length} / {MaxQuestionChars}
            </span>
            <button className="primary-button" type="submit" disabled={isSubmitting || !normalizedQuestion}>
              <Send size={16} />
              <span>{isSubmitting ? 'Enviando' : 'Enviar pregunta'}</span>
            </button>
          </div>
        </form>

        {error ? <ChatErrorMessage error={error} /> : null}

        {!answer && !error && !isSubmitting ? (
          <section className="empty-state" aria-label="Estado inicial">
            <MessageSquareText size={20} />
            <p>Las respuestas aparecen acá con sus citas cuando terminás la consulta.</p>
          </section>
        ) : null}

        {isSubmitting ? (
          <p className="status-message" role="status">
            Buscando instrucciones y preparando la respuesta...
          </p>
        ) : null}

        {answer ? (
          <article className="answer-panel">
            <div className="answer-heading">
              <MessageSquareText size={18} />
              <h2>Respuesta</h2>
              {cacheHit ? <span className="cache-badge">Respuesta desde caché semántico</span> : null}
            </div>
            <p>{answer}</p>
            {usage ? (
              <dl className="usage-row" aria-label="Uso de IA">
                <div>
                  <dt>Tokens</dt>
                  <dd>
                    Entrada {usage.inputTokens} / caché {usage.cachedTokens} / salida {usage.outputTokens}
                  </dd>
                </div>
                <div>
                  <dt>Costo</dt>
                  <dd>{formatUsageCost(usage.costUsd)}</dd>
                </div>
              </dl>
            ) : null}
            {citations.length > 0 ? (
              <div className="citation-list" aria-label="Citas">
                {citations.map((citation) => (
                  <button
                    key={`${citation.documentId}-${citation.documentVersionId}`}
                    type="button"
                    className="citation-button"
                    onClick={() => void openCitation(citation)}
                  >
                    <ExternalLink size={15} />
                    <span>Abrir cita {citation.headingPath[0] ?? 'documento'}</span>
                  </button>
                ))}
              </div>
            ) : null}
          </article>
        ) : null}

        {answer && queryAuditEventId ? (
          <form className="feedback-panel" onSubmit={handleFeedback}>
            <div className="feedback-actions" aria-label="Feedback de la respuesta">
              <button
                className={feedbackValue === 'up' ? 'feedback-button selected' : 'feedback-button'}
                type="button"
                onClick={() => setFeedbackValue('up')}
              >
                <ThumbsUp size={16} />
                <span>Me sirvió</span>
              </button>
              <button
                className={feedbackValue === 'down' ? 'feedback-button selected' : 'feedback-button'}
                type="button"
                onClick={() => setFeedbackValue('down')}
              >
                <ThumbsDown size={16} />
                <span>No me sirvió</span>
              </button>
            </div>

            {feedbackValue ? (
              <label className="field">
                <span>Comentario opcional</span>
                <textarea
                  value={comment}
                  maxLength={1000}
                  onChange={(event) => setComment(event.target.value)}
                  disabled={isSendingFeedback}
                />
              </label>
            ) : null}

            {feedbackValue ? (
              <button className="primary-button" type="submit" disabled={isSendingFeedback}>
                {feedbackSubmitted ? 'Actualizar feedback' : 'Enviar feedback'}
              </button>
            ) : null}

            {feedbackSubmitted ? (
              <p className="status-message success" role="status">
                Feedback registrado.
              </p>
            ) : null}
          </form>
        ) : null}
      </section>
    </main>
  )
}

async function askWithTokenRenewal(question: string): Promise<ChatResult> {
  try {
    return await submitQuestion(question)
  } catch (caught) {
    if (caught instanceof ApiError && caught.code === 'AUTH_TOKEN_EXPIRED') {
      await renewChatToken()
      return await submitQuestion(question)
    }

    throw caught
  }
}

function ChatErrorMessage({ error }: { error: ChatErrorState }) {
  return (
    <section className={`status-message error ${error.kind}`} role="alert">
      <div className="status-heading">
        <AlertCircle size={17} />
        <p>{error.title}</p>
      </div>
      {error.detail ? <p className="status-detail">{error.detail}</p> : null}
      {error.requestId ? <p className="status-detail">ID de solicitud: {error.requestId}</p> : null}
    </section>
  )
}

function formatUsageCost(value: number): string {
  return `Costo estimado: USD ${value.toFixed(6)}`
}

function toChatError(caught: unknown): ChatErrorState {
  if (caught instanceof ApiError) {
    if (caught.code === 'AI_BUDGET_EXCEEDED') {
      return {
        kind: 'budget',
        title: 'Alcanzaste el presupuesto mensual de uso de IA.',
        detail: 'Podés seguir abriendo documentos autorizados.',
        requestId: caught.requestId,
      }
    }

    if (caught.code === 'AUTH_TOKEN_EXPIRED' || caught.code === 'AUTH_REQUIRED') {
      return {
        kind: 'auth',
        title: 'Tu sesión de chat expiró.',
        detail: 'Volvé a iniciar sesión para continuar.',
        requestId: caught.requestId,
      }
    }

    return {
      kind: 'generic',
      title: 'No se pudo responder la pregunta.',
      requestId: caught.requestId,
    }
  }

  return {
    kind: 'generic',
    title: 'No se pudo responder la pregunta.',
  }
}

function toCitationError(caught: unknown): ChatErrorState {
  return {
    kind: 'generic',
    title: 'No se pudo abrir la cita.',
    requestId: caught instanceof ApiError ? caught.requestId : undefined,
  }
}

function toFeedbackError(caught: unknown): ChatErrorState {
  return {
    kind: 'generic',
    title: 'No se pudo registrar el feedback.',
    requestId: caught instanceof ApiError ? caught.requestId : undefined,
  }
}
