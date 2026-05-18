import { useState } from 'react'
import { MessageSquareText, Send, ThumbsDown, ThumbsUp } from 'lucide-react'
import { submitFeedback, submitQuestion, type FeedbackValue } from './api/chat'
import './App.css'

export default function App() {
  const [question, setQuestion] = useState('')
  const [answer, setAnswer] = useState('')
  const [queryAuditEventId, setQueryAuditEventId] = useState<string | null>(null)
  const [feedbackValue, setFeedbackValue] = useState<FeedbackValue | null>(null)
  const [comment, setComment] = useState('')
  const [feedbackSubmitted, setFeedbackSubmitted] = useState(false)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [isSendingFeedback, setIsSendingFeedback] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function handleAsk(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const normalizedQuestion = question.trim()
    if (!normalizedQuestion) {
      return
    }

    setIsSubmitting(true)
    setError(null)
    setAnswer('')
    setFeedbackValue(null)
    setFeedbackSubmitted(false)
    setComment('')
    try {
      const result = await submitQuestion(normalizedQuestion)
      setAnswer(result.answer)
      setQueryAuditEventId(result.queryAuditEventId)
    } catch {
      setError('No se pudo responder la pregunta.')
    } finally {
      setIsSubmitting(false)
    }
  }

  async function handleFeedback(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!queryAuditEventId || !feedbackValue) {
      return
    }

    setIsSendingFeedback(true)
    setError(null)
    try {
      await submitFeedback(queryAuditEventId, feedbackValue, comment)
      setFeedbackSubmitted(true)
    } catch {
      setError('No se pudo registrar el feedback.')
    } finally {
      setIsSendingFeedback(false)
    }
  }

  return (
    <main className="chat-shell">
      <header className="chat-header">
        <div>
          <p className="eyebrow">Advanced RAG</p>
          <h1>Instruction Chat</h1>
        </div>
      </header>

      <section className="chat-panel" aria-label="Instruction Chat">
        <form className="question-form" onSubmit={handleAsk}>
          <label className="field">
            <span>Pregunta</span>
            <textarea
              value={question}
              onChange={(event) => setQuestion(event.target.value)}
              disabled={isSubmitting}
            />
          </label>
          <button className="primary-button" type="submit" disabled={isSubmitting}>
            <Send size={16} />
            <span>{isSubmitting ? 'Enviando...' : 'Enviar pregunta'}</span>
          </button>
        </form>

        {error ? (
          <p className="status-message error" role="alert">
            {error}
          </p>
        ) : null}

        {answer ? (
          <article className="answer-panel">
            <div className="answer-heading">
              <MessageSquareText size={18} />
              <h2>Respuesta</h2>
            </div>
            <p>{answer}</p>
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
                <span>Me sirvio</span>
              </button>
              <button
                className={feedbackValue === 'down' ? 'feedback-button selected' : 'feedback-button'}
                type="button"
                onClick={() => setFeedbackValue('down')}
              >
                <ThumbsDown size={16} />
                <span>No me sirvio</span>
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
