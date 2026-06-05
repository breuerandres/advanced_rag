import { useEffect, useMemo, useState, type FormEvent, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import {
  AlertCircle,
  FileText,
  LogOut,
  MessageSquareText,
  Plus,
  ThumbsDown,
  ThumbsUp,
} from 'lucide-react'
import {
  AuthCardHeader,
  AuthShell,
  Button,
  ChatComposer,
  ChatMessage,
  CitationDrawer,
  CommandPalette,
  DarkModeToggle,
  EmptyState,
  Input,
  LanguageSelect,
  Textarea,
} from '@helpcenter/shared-ui'
import {
  createViewerLink,
  getChatSession,
  getSession,
  listChatSessions,
  login,
  logout,
  submitFeedback,
  submitQuestion,
  type ChatCitation,
  type ChatSessionSummary,
  type ChatSessionTurn,
  type ChatUsage,
  type FeedbackValue,
  type SessionUser,
} from './api/chat'
import { ApiError } from './lib/api-error'
import './i18n'
import './App.css'

type AppMode = 'loading' | 'login' | 'ready' | 'unavailable'
type ChatStatus = 'idle' | 'submitting'
const MaxQuestionChars = 4000
const NewConversationTitle = 'Nueva conversación'

interface ChatErrorState {
  title: string
  detail?: string
  requestId?: string
  kind: 'generic' | 'budget' | 'auth'
}

interface ConversationTurn {
  id: string
  question: string
  answer: string
  queryAuditEventId: string | null
  citations: ChatCitation[]
  cacheHit: boolean
  usage: ChatUsage | null
  feedbackValue: FeedbackValue | null
  feedbackComment: string
  pending?: boolean
}

export default function App() {
  const { t, i18n } = useTranslation()
  const [initialConversationId] = useState(() => createConversationId())
  const [mode, setMode] = useState<AppMode>('loading')
  const [bootError, setBootError] = useState<string | null>(null)
  const [sessionUser, setSessionUser] = useState<SessionUser | null>(null)
  const [turns, setTurns] = useState<ConversationTurn[]>([])
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
    { id: initialConversationId, title: NewConversationTitle, isDraft: true },
  ])
  const [activeConversationId, setActiveConversationId] = useState(initialConversationId)
  const [isCitationDrawerOpen, setIsCitationDrawerOpen] = useState(false)
  const [activeDrawerCitations, setActiveDrawerCitations] = useState<ChatCitation[]>([])

  const isSubmitting = status === 'submitting'
  const drawerCitations = useMemo(
    () =>
      uniqueCitations(activeDrawerCitations).map((citation) => ({
        id: `${citation.documentId}-${citation.documentVersionId}`,
        title: citation.headingPath[0] ?? 'Documento citado',
        headingPath: citation.headingPath.length > 1 ? citation.headingPath : undefined,
        onOpen: () => void openCitation(citation),
      })),
    [activeDrawerCitations],
  )
  const visibleCitations = uniqueCitations(citations)
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
            hint: visibleCitations.length > 0 ? `${visibleCitations.length} disponibles` : 'Sin citas',
            icon: <FileText size={16} aria-hidden="true" />,
            onSelect: () => openCitationDrawer(citations),
          },
        ],
      },
    ],
    [citations, visibleCitations.length],
  )

  useEffect(() => {
    let cancelled = false

    async function boot() {
      try {
        const session = await getSession()
        const sessionList = await listChatSessions()
        if (cancelled) {
          return
        }
        setSessionUser(session.user)
        if (sessionList.sessions.length > 0) {
          const [firstSession] = sessionList.sessions
          setConversations(sessionList.sessions.map(toLocalConversation))
          setActiveConversationId(firstSession.sessionId)
          const history = await getChatSession(firstSession.sessionId)
          if (cancelled) {
            return
          }
          applyHistory(history.turns)
        }
        if (!cancelled) {
          setMode('ready')
        }
      } catch (caught) {
        if (cancelled) {
          return
        }

        if (caught instanceof ApiError && caught.code === 'AUTH_REQUIRED') {
          setSessionUser(null)
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
    const sessionId = activeConversationId || createConversationId()
    const pendingTurnId = `pending-${Date.now()}`
    const latestCompletedTurn = turns[turns.length - 1] ?? null
    let streamedAnswer = ''
    const pendingTurn: ConversationTurn = {
      id: pendingTurnId,
      question: nextQuestion,
      answer: '',
      queryAuditEventId: null,
      citations: [],
      cacheHit: false,
      usage: null,
      feedbackValue: null,
      feedbackComment: '',
      pending: true,
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
    setTurns((current) => [...current, pendingTurn])
    setConversations((current) => upsertDraftConversation(current, sessionId, summarizeQuestion(nextQuestion)))
    try {
      const result = await submitQuestion({
        question: nextQuestion,
        sessionId,
        locale: i18n.resolvedLanguage ?? i18n.language,
      }, {
        onAnswerToken(delta) {
          streamedAnswer += delta
          setAnswer(streamedAnswer)
          setTurns((current) =>
            current.map((turn) =>
              turn.id === pendingTurnId ? { ...turn, answer: streamedAnswer } : turn,
            ),
          )
        },
      })
      const completedTurn: ConversationTurn = {
        id: pendingTurnId,
        question: nextQuestion,
        answer: result.answer,
        queryAuditEventId: result.queryAuditEventId,
        citations: result.citations,
        cacheHit: result.cacheHit,
        usage: result.usage,
        feedbackValue: null,
        feedbackComment: '',
      }
      setTurns((current) =>
        current.map((turn) => (turn.id === pendingTurnId ? completedTurn : turn)),
      )
      setAnswer(result.answer)
      setQueryAuditEventId(result.queryAuditEventId)
      setCitations(result.citations)
      setCacheHit(result.cacheHit)
      setUsage(result.usage)
      void refreshConversationList(sessionId, summarizeQuestion(nextQuestion))
    } catch (caught) {
      setTurns((current) => current.filter((turn) => turn.id !== pendingTurnId))
      applyTurnSelection(latestCompletedTurn)
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
    setActiveDrawerCitations([])
    const nextId = createConversationId()
    setActiveConversationId(nextId)
    setTurns([])
    setConversations((current) => [
      { id: nextId, title: NewConversationTitle, isDraft: true },
      ...current.filter((conversation) => !conversation.isDraft),
    ])
  }

  async function selectConversation(conversationId: string) {
    setActiveConversationId(conversationId)
    setError(null)
    const selected = conversations.find((conversation) => conversation.id === conversationId)
    if (selected?.isDraft) {
      setTurns([])
      setAnswer('')
      setQueryAuditEventId(null)
      setCitations([])
      setUsage(null)
      setCacheHit(false)
      setFeedbackValue(null)
      setFeedbackSubmitted(false)
      setComment('')
      setActiveDrawerCitations([])
      setIsCitationDrawerOpen(false)
      return
    }

    try {
      const history = await getChatSession(conversationId)
      applyHistory(history.turns)
    } catch (caught) {
      setError(toChatError(caught))
    }
  }

  async function refreshConversationList(activeSessionId: string, fallbackTitle: string) {
    try {
      const sessionList = await listChatSessions()
      const remoteSessions = Array.isArray(sessionList.sessions) ? sessionList.sessions : []
      setConversations((current) => {
        const remote = remoteSessions.map(toLocalConversation)
        if (remote.some((conversation) => conversation.id === activeSessionId)) {
          return remote
        }
        const existingDraft = current.find((conversation) => conversation.id === activeSessionId)
        return [
          {
            id: activeSessionId,
            title: existingDraft?.title ?? fallbackTitle,
            isDraft: true,
          },
          ...remote,
        ]
      })
    } catch {
      // Non-critical after a successful answer. A reload hydrates audited sessions.
    }
  }

  function applyHistory(serverTurns: ChatSessionTurn[]) {
    const mappedTurns = serverTurns.map(toConversationTurn)
    setTurns(mappedTurns)
    const latestTurn = mappedTurns[mappedTurns.length - 1]
    applyTurnSelection(latestTurn ?? null)
  }

  function applyTurnSelection(turn: ConversationTurn | null) {
    setAnswer(turn?.answer ?? '')
    setQueryAuditEventId(turn?.queryAuditEventId ?? null)
    setCitations(turn?.citations ?? [])
    setCacheHit(turn?.cacheHit ?? false)
    setUsage(turn?.usage ?? null)
    setFeedbackValue(turn?.feedbackValue ?? null)
    setComment(turn?.feedbackComment ?? '')
    setFeedbackSubmitted(Boolean(turn?.feedbackValue))
    setActiveDrawerCitations([])
    setIsCitationDrawerOpen(false)
  }

  async function openCitation(citation: ChatCitation) {
    setError(null)
    try {
      const url = await createViewerLink(citation.documentId)
      window.open(url, '_blank', 'noopener,noreferrer')
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
      setTurns((current) =>
        current.map((turn) =>
          turn.queryAuditEventId === queryAuditEventId
            ? { ...turn, feedbackValue, feedbackComment: comment }
            : turn,
        ),
      )
      setFeedbackSubmitted(true)
    } catch (caught) {
      setError(toFeedbackError(caught))
    } finally {
      setIsSendingFeedback(false)
    }
  }

  function openCitationDrawer(nextCitations: ChatCitation[]) {
    const next = uniqueCitations(nextCitations)
    setActiveDrawerCitations(next)
    setIsCitationDrawerOpen(next.length > 0)
  }

  function toggleFeedbackValue(nextValue: FeedbackValue) {
    setFeedbackValue((current) => {
      if (current === nextValue) {
        setComment('')
        return null
      }

      return nextValue
    })
  }

  async function handleLogout() {
    setError(null)
    try {
      await logout()
      setTurns([])
      setAnswer('')
      setQueryAuditEventId(null)
      setCitations([])
      setUsage(null)
      setCacheHit(false)
      setFeedbackValue(null)
      setFeedbackSubmitted(false)
      setComment('')
      setActiveDrawerCitations([])
      setIsCitationDrawerOpen(false)
      setSessionUser(null)
      setMode('login')
    } catch (caught) {
      setError(toChatError(caught))
    }
  }

  if (mode === 'loading') {
    return <ChatAuthFrame title="Cargando chat" detail="Verificando sesión." />
  }

  if (mode === 'unavailable') {
    return (
      <ChatAuthFrame
        title="Chat no disponible"
        detail={bootError ?? 'Revisá que la API esté disponible.'}
        tone="error"
      />
    )
  }

  if (mode === 'login') {
    return (
      <ChatLoginPage
        onAuthenticated={(user) => {
          setSessionUser(user)
          setMode('ready')
        }}
      />
    )
  }

  return (
    <div className="chat-app-layout">
      <ChatSidebar
        conversations={conversations}
        user={sessionUser}
        activeConversationId={activeConversationId}
        languageLabel={t('common.language')}
        languageValue={i18n.resolvedLanguage ?? i18n.language}
        onLanguageChange={(value) => void i18n.changeLanguage(value)}
        onNewConversation={resetCurrentAnswer}
        onSelectConversation={(conversationId) => void selectConversation(conversationId)}
        onLogout={() => void handleLogout()}
      />
      <main className="chat-main">
      <CommandPalette groups={commandGroups} placeholder="Buscar acción..." />
      <CitationDrawer
        open={isCitationDrawerOpen}
        onOpenChange={setIsCitationDrawerOpen}
        citations={drawerCitations}
      />
      <section className="chat-shell" id="chat">
        <section className="chat-panel" aria-label="Chat de instrucciones">
          <header className="chat-thread-header">
            <div>
              <h1>{t('chat.page_title')}</h1>
              <p className="header-copy">{t('chat.header_copy')}</p>
            </div>
            <span className="session-badge">Sesión activa</span>
          </header>

          {error ? <ChatErrorMessage error={error} /> : null}

          {turns.length === 0 && !error && !isSubmitting ? (
            <EmptyState
              className="empty-state"
              title="Estado inicial"
              description="Las respuestas aparecen acá con sus citas cuando terminás la consulta."
              icon={<MessageSquareText size={20} aria-hidden="true" />}
            />
          ) : null}

          {turns.length === 0 && isSubmitting ? (
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

          {turns.length === 0 && answer && !isSubmitting ? (
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
              <CitationSummaryButton citations={citations} onOpen={openCitationDrawer} />
            </article>
          ) : null}

          {turns.length > 0 ? (
            <section className="conversation-transcript" aria-label="Conversación activa">
              {turns.map((turn) => (
                <div className="conversation-turn" key={turn.id}>
                  <article className="answer-panel user-turn">
                    <div className="answer-heading">
                      <MessageSquareText size={18} />
                      <h2>Pregunta</h2>
                    </div>
                    <ChatMessage author="user" content={turn.question} />
                  </article>
                  <article
                    className={turn.pending ? 'answer-panel streaming-answer' : 'answer-panel'}
                    aria-label={turn.pending ? 'Respuesta en curso' : undefined}
                  >
                    <div className="answer-heading">
                      <MessageSquareText size={18} />
                      <h2>Respuesta</h2>
                      {turn.cacheHit ? <span className="cache-badge">Respuesta desde caché semántico</span> : null}
                    </div>
                    <ChatMessage author="assistant" content={turn.answer || ' '} pending={turn.pending} />
                    {turn.pending ? (
                      <p className="status-message" role="status">
                        Buscando instrucciones y preparando la respuesta...
                      </p>
                    ) : null}
                    {turn.usage ? (
                      <dl className="usage-row" aria-label="Uso de IA">
                        <div>
                          <dt>Tokens</dt>
                          <dd>
                            Entrada {turn.usage.inputTokens} / caché {turn.usage.cachedTokens} / salida{' '}
                            {turn.usage.outputTokens}
                          </dd>
                        </div>
                        <div>
                          <dt>Costo</dt>
                          <dd>{formatUsageCost(turn.usage.costUsd)}</dd>
                        </div>
                      </dl>
                    ) : null}
                    <CitationSummaryButton citations={turn.citations} onOpen={openCitationDrawer} />
                  </article>
                </div>
              ))}
            </section>
          ) : null}

          {answer && queryAuditEventId ? (
            <form className="feedback-panel" onSubmit={handleFeedback}>
              <div className="feedback-actions" aria-label="Feedback de la respuesta">
                <button
                  className={feedbackValue === 'up' ? 'feedback-button selected' : 'feedback-button'}
                  type="button"
                  onClick={() => toggleFeedbackValue('up')}
                >
                  <ThumbsUp size={16} />
                  <span>Me sirvió</span>
                </button>
                <button
                  className={feedbackValue === 'down' ? 'feedback-button selected' : 'feedback-button'}
                  type="button"
                  onClick={() => toggleFeedbackValue('down')}
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
        </section>
      </section>
      </main>
    </div>
  )
}

interface ChatSidebarProps {
  conversations: LocalConversation[]
  user: SessionUser | null
  activeConversationId: string
  languageLabel: string
  languageValue: string
  onLanguageChange: (value: string) => void
  onNewConversation: () => void
  onSelectConversation: (conversationId: string) => void
  onLogout: () => void
}

function ChatSidebar({
  conversations,
  user,
  activeConversationId,
  languageLabel,
  languageValue,
  onLanguageChange,
  onNewConversation,
  onSelectConversation,
  onLogout,
}: ChatSidebarProps) {
  return (
    <div className="chat-sidebar" role="complementary" aria-label="Menu de chat">
      <header className="chat-sidebar-brand">
        <span className="brand-mark">AR</span>
        <div>
          <p>Advanced RAG</p>
          <span>Chat</span>
        </div>
      </header>

      <div className="chat-sidebar-actions">
        <Button type="button" className="chat-new-button" onClick={onNewConversation}>
          <Plus size={16} aria-hidden="true" />
          Nueva conversación
        </Button>
      </div>

      <nav className="chat-history-nav" aria-label="Historial de conversaciones">
        <p className="eyebrow">Conversaciones</p>
        {conversations.map((conversation) => (
          <button
            key={conversation.id}
            type="button"
            aria-current={conversation.id === activeConversationId ? 'page' : undefined}
            className="chat-history-item"
            onClick={() => onSelectConversation(conversation.id)}
          >
            <span>{conversation.title}</span>
            {conversation.isDraft ? <small>Borrador local</small> : null}
          </button>
        ))}
      </nav>

      <footer className="chat-sidebar-footer">
        <div className="chat-sidebar-controls">
          <LanguageSelect
            label={languageLabel}
            value={languageValue}
            onChange={onLanguageChange}
            options={[
              { value: 'es-AR', label: 'ES' },
              { value: 'en-US', label: 'EN' },
            ]}
          />
          <DarkModeToggle label="Cambiar tema" />
        </div>
        {user ? (
          <section className="chat-sidebar-session" aria-label="Sesión activa">
            <span>Sesión activa</span>
            <strong>{user.email}</strong>
            <span>{user.roles.join(', ')}</span>
          </section>
        ) : null}
        <Button type="button" variant="secondary" className="chat-logout-button" onClick={onLogout}>
          <LogOut size={16} aria-hidden="true" />
          Cerrar sesión
        </Button>
      </footer>
    </div>
  )
}

function ChatLoginPage({ onAuthenticated }: { onAuthenticated: (user: SessionUser) => void }) {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError(null)
    setIsSubmitting(true)
    try {
      const session = await login(email, password)
      onAuthenticated(session.user)
    } catch (caught) {
      setError(formatApiError(caught, 'No se pudo iniciar sesión.'))
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <ChatAuthFrame controls={<AuthSurfaceControls />}>
      <form className="auth-card" onSubmit={submit}>
        <AuthCardHeader eyebrow="Chat" title="Iniciar sesión" />
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
          <span>Contraseña</span>
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
    </ChatAuthFrame>
  )
}

interface LocalConversation {
  id: string
  title: string
  isDraft?: boolean
}

function toLocalConversation(session: ChatSessionSummary): LocalConversation {
  return {
    id: session.sessionId,
    title: session.title || session.lastQuestion || NewConversationTitle,
  }
}

function toConversationTurn(turn: ChatSessionTurn): ConversationTurn {
  return {
    id: turn.queryAuditEventId,
    question: turn.question,
    answer: turn.answer,
    queryAuditEventId: turn.queryAuditEventId,
    citations: turn.citations,
    cacheHit: turn.cacheHit,
    usage: null,
    feedbackValue: turn.feedbackValue,
    feedbackComment: turn.feedbackComment ?? '',
  }
}

function upsertDraftConversation(
  conversations: LocalConversation[],
  sessionId: string,
  title: string,
): LocalConversation[] {
  const nextConversation = { id: sessionId, title, isDraft: true }
  const exists = conversations.some((conversation) => conversation.id === sessionId)
  if (!exists) {
    return [nextConversation, ...conversations.filter((conversation) => !conversation.isDraft)]
  }

  return conversations.map((conversation) =>
    conversation.id === sessionId
      ? { ...conversation, title: conversation.isDraft ? title : conversation.title }
      : conversation,
  )
}

function createConversationId(): string {
  if ('randomUUID' in crypto) {
    return crypto.randomUUID()
  }

  return `session-${Date.now()}`
}

function ChatAuthFrame({
  children,
  controls,
  title,
  detail,
  tone = 'neutral',
}: {
  children?: ReactNode
  controls?: ReactNode
  title?: string
  detail?: string
  tone?: 'neutral' | 'error'
}) {
  return (
    <AuthShell>
      <section className="auth-card-stack" aria-label="Controles de acceso">
        {controls}
        {children ?? (
          <section className={`auth-card status-panel ${tone}`}>
            <h1>{title}</h1>
            <p>{detail}</p>
          </section>
        )}
      </section>
    </AuthShell>
  )
}

function AuthSurfaceControls() {
  const { t, i18n } = useTranslation()

  return (
    <div className="auth-surface-controls">
      <LanguageSelect
        label={t('common.language')}
        value={i18n.resolvedLanguage ?? i18n.language}
        onChange={(value) => void i18n.changeLanguage(value)}
        options={[
          { value: 'es-AR', label: 'ES' },
          { value: 'en-US', label: 'EN' },
        ]}
      />
      <DarkModeToggle label={t('common.toggle_theme')} />
    </div>
  )
}

function CitationSummaryButton({
  citations,
  onOpen,
}: {
  citations: ChatCitation[]
  onOpen: (citations: ChatCitation[]) => void
}) {
  const unique = uniqueCitations(citations)
  if (unique.length === 0) {
    return null
  }

  return (
    <button type="button" className="citation-summary-button" onClick={() => onOpen(unique)}>
      <FileText size={15} aria-hidden="true" />
      Ver citas ({unique.length})
    </button>
  )
}

function uniqueCitations(citations: ChatCitation[]): ChatCitation[] {
  const seen = new Set<string>()
  const unique: ChatCitation[] = []
  for (const citation of citations) {
    const key = `${citation.documentId}:${citation.documentVersionId}`
    if (seen.has(key)) {
      continue
    }

    seen.add(key)
    unique.push(citation)
  }

  return unique
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
