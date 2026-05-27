import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import {
  AlertCircle,
  Clock3,
  ExternalLink,
  FileText,
  MessageSquareText,
  Plus,
  ShieldCheck,
  ThumbsDown,
  ThumbsUp,
} from 'lucide-react'
import {
  AppShell,
  AuthCardHeader,
  AuthShell,
  Button,
  ChatComposer,
  ChatMessage,
  CitationCard,
  CitationDrawer,
  CommandPalette,
  ConversationList,
  DarkModeToggle,
  EmptyState,
  Input,
  LanguageSelect,
  Textarea,
} from '@helpcenter/shared-ui'
import {
  createViewerLink,
  getSession,
  login,
  submitFeedback,
  submitQuestion,
  type ChatCitation,
  type ChatUsage,
  type FeedbackValue,
} from './api/chat'
import { ApiError } from './lib/api-error'
import './i18n'
import './App.css'

type AppMode = 'loading' | 'login' | 'ready' | 'unavailable'
type ChatStatus = 'idle' | 'submitting'
const MaxQuestionChars = 4000
const WelcomeConversationId = 'welcome'

interface ChatErrorState {
  title: string
  detail?: string
  requestId?: string
  kind: 'generic' | 'budget' | 'auth'
}

export default function App() {
  const { t, i18n } = useTranslation()
  const [mode, setMode] = useState<AppMode>('loading')
  const [bootError, setBootError] = useState<string | null>(null)
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
  const [conversations, setConversations] = useState<LocalConversation[]>([
    { id: WelcomeConversationId, title: 'Nueva conversación' },
  ])
  const [activeConversationId, setActiveConversationId] = useState(WelcomeConversationId)
  const [isCitationDrawerOpen, setIsCitationDrawerOpen] = useState(false)

  const isSubmitting = status === 'submitting'
  const drawerCitations = useMemo(
    () =>
      citations.map((citation) => ({
        id: `${citation.documentId}-${citation.documentVersionId}`,
        title: citation.headingPath[0] ?? 'Documento citado',
        headingPath: citation.headingPath,
      })),
    [citations],
  )
  const commandGroups = useMemo(
    () => [
      {
        heading: 'Chat',
        items: [
          {
            id: 'new-question',
            label: 'Nueva pregunta',
            hint: 'Limpia la respuesta actual',
            icon: <Plus size={16} aria-hidden="true" />,
            onSelect: resetCurrentAnswer,
            shortcut: 'Ctrl K',
          },
          {
            id: 'show-citations',
            label: 'Ver citas',
            hint: citations.length > 0 ? `${citations.length} disponibles` : 'Sin citas',
            icon: <FileText size={16} aria-hidden="true" />,
            onSelect: () => setIsCitationDrawerOpen(true),
          },
        ],
      },
    ],
    [citations.length],
  )

  useEffect(() => {
    let cancelled = false

    async function boot() {
      try {
        await getSession()
        if (!cancelled) {
          setMode('ready')
        }
      } catch (caught) {
        if (cancelled) {
          return
        }

        if (caught instanceof ApiError && caught.code === 'AUTH_REQUIRED') {
          setMode('login')
          return
        }

        setBootError(formatApiError(caught, 'No se pudo iniciar el chat.'))
        setMode('unavailable')
      }
    }

    void boot()
    return () => {
      cancelled = true
    }
  }, [])

  async function handleAsk(nextQuestion: string) {
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
      const result = await submitQuestion(nextQuestion)
      setAnswer(result.answer)
      setQueryAuditEventId(result.queryAuditEventId)
      setCitations(result.citations)
      setCacheHit(result.cacheHit)
      setUsage(result.usage)
      const conversationId = result.queryAuditEventId ?? `local-${Date.now()}`
      setActiveConversationId(conversationId)
      setConversations((current) => [
        { id: conversationId, title: summarizeQuestion(nextQuestion) },
        ...current.filter((conversation) => conversation.id !== WelcomeConversationId),
      ])
    } catch (caught) {
      setError(toChatError(caught))
    } finally {
      setStatus('idle')
    }
  }

  function resetCurrentAnswer() {
    setAnswer('')
    setQueryAuditEventId(null)
    setCitations([])
    setUsage(null)
    setCacheHit(false)
    setFeedbackValue(null)
    setFeedbackSubmitted(false)
    setComment('')
    setError(null)
    setIsCitationDrawerOpen(false)
    setActiveConversationId(WelcomeConversationId)
    setConversations((current) =>
      current.some((conversation) => conversation.id === WelcomeConversationId)
        ? current
        : [{ id: WelcomeConversationId, title: 'Nueva conversación' }, ...current],
    )
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

  if (mode === 'loading') {
    return <ChatAuthFrame title="Cargando chat" detail="Verificando sesion." />
  }

  if (mode === 'unavailable') {
    return (
      <ChatAuthFrame
        title="Chat no disponible"
        detail={bootError ?? 'Revisa que la API este disponible.'}
        tone="error"
      />
    )
  }

  if (mode === 'login') {
    return <ChatLoginPage onAuthenticated={() => setMode('ready')} />
  }

  return (
    <AppShell className="chat-main">
      <CommandPalette groups={commandGroups} placeholder="Buscar acción..." />
      <CitationDrawer
        open={isCitationDrawerOpen}
        onOpenChange={setIsCitationDrawerOpen}
        citations={drawerCitations}
      />
      <section className="chat-shell" id="chat">
        <header className="chat-header">
          <div>
            <p className="eyebrow">Advanced RAG</p>
            <h1>{t('chat.page_title')}</h1>
            <p className="header-copy">{t('chat.header_copy')}</p>
          </div>
          <div className="chat-toolbar">
            <LanguageSelect
              label={t('common.language')}
              value={i18n.resolvedLanguage ?? i18n.language}
              onChange={(value) => void i18n.changeLanguage(value)}
              options={[
                { value: 'es-AR', label: 'ES' },
                { value: 'en-US', label: 'EN' },
              ]}
            />
            <DarkModeToggle label="Cambiar tema" />
          </div>
          <div className="chat-status-strip" aria-label="Estado del chat">
            <span>
              <ShieldCheck size={16} aria-hidden="true" />
              Sesion unificada
            </span>
            <span>
              <Clock3 size={16} aria-hidden="true" />
              Citas trazables
            </span>
          </div>
        </header>

        <section className="chat-workspace" aria-label="Workspace de chat">
          <aside className="chat-left-rail" aria-label="Historial de conversaciones">
            <div className="rail-header">
              <p className="eyebrow">Conversaciones</p>
              <Button type="button" variant="secondary" size="sm" onClick={resetCurrentAnswer}>
                <Plus size={15} aria-hidden="true" />
                Nueva
              </Button>
            </div>
            <ConversationList
              items={conversations.map((conversation) => ({
                ...conversation,
                active: conversation.id === activeConversationId,
              }))}
              onSelect={setActiveConversationId}
            />
          </aside>

          <section className="chat-panel" aria-label="Chat de instrucciones">
          <section className="question-form">
            <ChatComposer
              disabled={isSubmitting}
              maxLength={MaxQuestionChars}
              placeholder="Escribí tu consulta..."
              submitLabel={t('chat.send_question')}
              pendingLabel="Enviando"
              characterCountLabel={(count, maxLength) => `${count} / ${maxLength}`}
              onSubmit={(nextQuestion) => void handleAsk(nextQuestion)}
            />
          </section>

          {error ? <ChatErrorMessage error={error} /> : null}

          {!answer && !error && !isSubmitting ? (
            <EmptyState
              className="empty-state"
              title="Estado inicial"
              description="Las respuestas aparecen acá con sus citas cuando terminás la consulta."
              icon={<MessageSquareText size={20} aria-hidden="true" />}
            />
          ) : null}

          {isSubmitting ? (
            <article className="answer-panel streaming-answer" aria-label="Respuesta en curso">
              <div className="answer-heading">
                <MessageSquareText size={18} />
                <h2>Respuesta</h2>
              </div>
              <ChatMessage author="assistant" content={answer || ' '} pending />
              <p className="status-message" role="status">
                Buscando instrucciones y preparando la respuesta...
              </p>
            </article>
          ) : null}

          {answer && !isSubmitting ? (
            <article className="answer-panel">
              <div className="answer-heading">
                <MessageSquareText size={18} />
                <h2>Respuesta</h2>
                {cacheHit ? <span className="cache-badge">Respuesta desde caché semántico</span> : null}
              </div>
              <ChatMessage author="assistant" content={answer} />
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
                      aria-label={`Abrir cita ${citation.headingPath[0] ?? 'documento'} en respuesta`}
                      onClick={() => void openCitation(citation)}
                    >
                      <ExternalLink size={15} />
                      <CitationCard
                        title={`Abrir cita ${citation.headingPath[0] ?? 'documento'}`}
                        headingPath={citation.headingPath}
                        meta="Documento"
                      />
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
                  <Textarea
                    value={comment}
                    maxLength={1000}
                    onChange={(event) => setComment(event.target.value)}
                    disabled={isSendingFeedback}
                  />
                </label>
              ) : null}

              {feedbackValue ? (
                <Button className="primary-button" type="submit" disabled={isSendingFeedback}>
                  {feedbackSubmitted ? 'Actualizar feedback' : 'Enviar feedback'}
                </Button>
              ) : null}

              {feedbackSubmitted ? (
                <p className="status-message success" role="status">
                  Feedback registrado.
                </p>
              ) : null}
            </form>
          ) : null}
          </section>

          <aside className="chat-context-rail" aria-label="Contexto de respuesta">
            <section className="context-card">
              <div className="context-card-header">
                <h2>Citas</h2>
                <Button
                  type="button"
                  variant="secondary"
                  size="sm"
                  disabled={citations.length === 0}
                  onClick={() => setIsCitationDrawerOpen(true)}
                >
                  Ver citas
                </Button>
              </div>
              {citations.length > 0 ? (
                <div className="citation-list" aria-label="Citas">
                  {citations.map((citation) => (
                    <button
                      key={`${citation.documentId}-${citation.documentVersionId}`}
                      type="button"
                      className="citation-button"
                      aria-label={`Abrir cita ${citation.headingPath[0] ?? 'documento'}`}
                      onClick={() => void openCitation(citation)}
                    >
                      <ExternalLink size={15} />
                      <CitationCard
                        title={`Abrir cita ${citation.headingPath[0] ?? 'documento'}`}
                        headingPath={citation.headingPath}
                        meta="Documento"
                      />
                    </button>
                  ))}
                </div>
              ) : (
                <p className="muted-context">Sin citas todavía</p>
              )}
            </section>
          </aside>
        </section>
      </section>
    </AppShell>
  )
}

function ChatLoginPage({ onAuthenticated }: { onAuthenticated: () => void }) {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError(null)
    setIsSubmitting(true)
    try {
      await login(email, password)
      onAuthenticated()
    } catch (caught) {
      setError(formatApiError(caught, 'No se pudo iniciar sesion.'))
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <AuthShell>
      <form className="auth-card" onSubmit={submit}>
        <AuthCardHeader eyebrow="Chat" title="Iniciar sesion" />
        {error ? <p className="status-message error">{error}</p> : null}
        <label className="field">
          <span>Email</span>
          <Input
            type="email"
            autoComplete="email"
            value={email}
            onChange={(event) => setEmail(event.target.value)}
          />
        </label>
        <label className="field">
          <span>Contrasena</span>
          <Input
            type="password"
            autoComplete="current-password"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
          />
        </label>
        <Button className="primary-button" type="submit" disabled={isSubmitting}>
          Entrar al chat
        </Button>
      </form>
    </AuthShell>
  )
}

interface LocalConversation {
  id: string
  title: string
}

function ChatAuthFrame({
  title,
  detail,
  tone = 'neutral',
}: {
  title: string
  detail: string
  tone?: 'neutral' | 'error'
}) {
  return (
    <AuthShell>
      <section className={`auth-card status-panel ${tone}`}>
        <h1>{title}</h1>
        <p>{detail}</p>
      </section>
    </AuthShell>
  )
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

function summarizeQuestion(question: string): string {
  const normalized = question.trim().replace(/\s+/g, ' ')
  return normalized.length > 48 ? `${normalized.slice(0, 45)}...` : normalized
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

function formatApiError(error: unknown, fallback: string): string {
  if (error instanceof ApiError && error.requestId) {
    return `${fallback} Referencia: ${error.requestId}.`
  }

  return fallback
}
