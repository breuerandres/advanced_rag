import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
  type FormEvent,
  type ReactNode,
  type UIEvent,
} from 'react'
import { useTranslation } from 'react-i18next'
import {
  AlertCircle,
  ExternalLink,
  FileText,
  LogOut,
  MessageSquareText,
  Plus,
  ThumbsDown,
  ThumbsUp,
  X,
} from 'lucide-react'
import {
  AuthCardHeader,
  AuthShell,
  Button,
  ChatComposer,
  ChatMessage,
  CommandPalette,
  DarkModeToggle,
  EmptyState,
  Input,
  LanguageSelect,
  Textarea,
} from '@helpcenter/shared-ui'
import {
  consumeSessionHandoff,
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

interface FeedbackDraft {
  value: FeedbackValue | null
  comment: string
  saved: boolean
}

export default function App() {
  const { t, i18n } = useTranslation()
  const [initialConversationId] = useState(() => createConversationId())
  const [mode, setMode] = useState<AppMode>('loading')
  const [bootError, setBootError] = useState<string | null>(null)
  const [sessionUser, setSessionUser] = useState<SessionUser | null>(null)
  const [turns, setTurns] = useState<ConversationTurn[]>([])
  const [status, setStatus] = useState<ChatStatus>('idle')
  const [error, setError] = useState<ChatErrorState | null>(null)
  const [conversations, setConversations] = useState<LocalConversation[]>([
    { id: initialConversationId, title: NewConversationTitle, isDraft: true },
  ])
  const [activeConversationId, setActiveConversationId] = useState(initialConversationId)
  const [isCitationDrawerOpen, setIsCitationDrawerOpen] = useState(false)
  const [activeDrawerCitations, setActiveDrawerCitations] = useState<ChatCitation[]>([])
  const [feedbackDrafts, setFeedbackDrafts] = useState<Record<string, FeedbackDraft>>({})
  const [sendingFeedbackTurnId, setSendingFeedbackTurnId] = useState<string | null>(null)
  const transcriptRef = useRef<HTMLDivElement | null>(null)
  const pinnedToBottomRef = useRef(true)
  const pendingTurnSeqRef = useRef(0)

  const isSubmitting = status === 'submitting'
  const latestTurn = turns.length > 0 ? turns[turns.length - 1] : null
  const latestCitations = useMemo(
    () => (latestTurn && !latestTurn.pending ? uniqueCitations(latestTurn.citations) : []),
    [latestTurn],
  )

  const closeCitationsPanel = useCallback(() => {
    setIsCitationDrawerOpen(false)
    setActiveDrawerCitations([])
  }, [])

  const openCitationDrawer = useCallback((nextCitations: ChatCitation[]) => {
    const next = uniqueCitations(nextCitations)
    setActiveDrawerCitations(next)
    setIsCitationDrawerOpen(next.length > 0)
  }, [])

  const openCitation = useCallback(async (citation: ChatCitation) => {
    setError(null)
    try {
      const url = await createViewerLink(citation.documentId)
      window.open(url, '_blank', 'noopener,noreferrer')
    } catch (caught) {
      setError(toCitationError(caught))
    }
  }, [])

  const startNewConversation = useCallback(() => {
    setError(null)
    closeCitationsPanel()
    setFeedbackDrafts({})
    setTurns([])
    const nextId = createConversationId()
    setActiveConversationId(nextId)
    setConversations((current) => [
      { id: nextId, title: NewConversationTitle, isDraft: true },
      ...current.filter((conversation) => !conversation.isDraft),
    ])
  }, [closeCitationsPanel])

  const applyHistory = useCallback((serverTurns: ChatSessionTurn[]) => {
    pinnedToBottomRef.current = true
    setTurns(serverTurns.map(toConversationTurn))
    setFeedbackDrafts({})
  }, [])

  const drawerCitations = useMemo(
    () =>
      uniqueCitations(activeDrawerCitations).map((citation) => ({
        id: `${citation.documentId}-${citation.documentVersionId}`,
        title: citation.headingPath[0] ?? t('chat.cited_document'),
        headingPath: citation.headingPath.length > 1 ? citation.headingPath : undefined,
        onOpen: () => void openCitation(citation),
      })),
    [activeDrawerCitations, openCitation, t],
  )
  const commandGroups = useMemo(
    () => [
      {
        heading: 'Chat',
        items: [
          {
            id: 'new-question',
            label: 'Nueva pregunta',
            hint: 'Empieza una conversación nueva',
            icon: <Plus size={16} aria-hidden="true" />,
            onSelect: startNewConversation,
            shortcut: 'Ctrl K',
          },
          {
            id: 'show-citations',
            label: 'Ver citas',
            hint: latestCitations.length > 0 ? `${latestCitations.length} disponibles` : 'Sin citas',
            icon: <FileText size={16} aria-hidden="true" />,
            onSelect: () => openCitationDrawer(latestCitations),
          },
        ],
      },
    ],
    [latestCitations, openCitationDrawer, startNewConversation],
  )

  useEffect(() => {
    let cancelled = false

    async function boot() {
      try {
        const handoffCode = new URLSearchParams(window.location.search).get('handoff')
        const session = handoffCode ? await consumeSessionHandoff(handoffCode) : await getSession()
        if (handoffCode) {
          removeHandoffFromUrl()
        }
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
  }, [applyHistory])

  useEffect(() => {
    const transcript = transcriptRef.current
    if (transcript && pinnedToBottomRef.current) {
      transcript.scrollTop = transcript.scrollHeight
    }
  }, [turns])

  function handleTranscriptScroll(event: UIEvent<HTMLElement>) {
    const element = event.currentTarget
    pinnedToBottomRef.current =
      element.scrollHeight - element.scrollTop - element.clientHeight < 96
  }

  async function handleAsk(nextQuestion: string) {
    const sessionId = activeConversationId || createConversationId()
    pendingTurnSeqRef.current += 1
    const pendingTurnId = `pending-${pendingTurnSeqRef.current}`
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
    pinnedToBottomRef.current = true
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
      void refreshConversationList(sessionId, summarizeQuestion(nextQuestion))
    } catch (caught) {
      setTurns((current) => current.filter((turn) => turn.id !== pendingTurnId))
      setError(toChatError(caught))
    } finally {
      setStatus('idle')
    }
  }

  async function selectConversation(conversationId: string) {
    setActiveConversationId(conversationId)
    setError(null)
    closeCitationsPanel()
    setFeedbackDrafts({})
    const selected = conversations.find((conversation) => conversation.id === conversationId)
    if (selected?.isDraft) {
      setTurns([])
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

  function draftFor(turn: ConversationTurn): FeedbackDraft {
    return (
      feedbackDrafts[turn.id] ?? {
        value: turn.feedbackValue,
        comment: turn.feedbackComment,
        saved: false,
      }
    )
  }

  function toggleTurnFeedback(turn: ConversationTurn, nextValue: FeedbackValue) {
    setFeedbackDrafts((current) => {
      const draft = current[turn.id] ?? {
        value: turn.feedbackValue,
        comment: turn.feedbackComment,
        saved: false,
      }
      const value = draft.value === nextValue ? null : nextValue
      return { ...current, [turn.id]: { value, comment: value ? draft.comment : '', saved: false } }
    })
  }

  function updateTurnFeedbackComment(turn: ConversationTurn, comment: string) {
    setFeedbackDrafts((current) => {
      const draft = current[turn.id] ?? {
        value: turn.feedbackValue,
        comment: turn.feedbackComment,
        saved: false,
      }
      return { ...current, [turn.id]: { ...draft, comment, saved: false } }
    })
  }

  async function submitTurnFeedback(turn: ConversationTurn, event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const draft = draftFor(turn)
    const feedbackValue = draft.value
    if (!turn.queryAuditEventId || !feedbackValue) {
      return
    }

    setSendingFeedbackTurnId(turn.id)
    setError(null)
    try {
      await submitFeedback(turn.queryAuditEventId, feedbackValue, draft.comment)
      setTurns((current) =>
        current.map((candidate) =>
          candidate.id === turn.id
            ? { ...candidate, feedbackValue, feedbackComment: draft.comment }
            : candidate,
        ),
      )
      setFeedbackDrafts((current) => ({
        ...current,
        [turn.id]: { value: feedbackValue, comment: draft.comment, saved: true },
      }))
    } catch (caught) {
      setError(toFeedbackError(caught))
    } finally {
      setSendingFeedbackTurnId(null)
    }
  }

  async function handleLogout() {
    setError(null)
    try {
      await logout()
      setTurns([])
      setFeedbackDrafts({})
      closeCitationsPanel()
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
    <div className={isCitationDrawerOpen ? 'chat-app-layout citations-open' : 'chat-app-layout'}>
      <ChatSidebar
        conversations={conversations}
        user={sessionUser}
        activeConversationId={activeConversationId}
        languageLabel={t('common.language')}
        languageValue={i18n.resolvedLanguage ?? i18n.language}
        onLanguageChange={(value) => void i18n.changeLanguage(value)}
        onNewConversation={startNewConversation}
        onSelectConversation={(conversationId) => void selectConversation(conversationId)}
        onLogout={() => void handleLogout()}
      />
      <main className="chat-main">
        <CommandPalette groups={commandGroups} placeholder="Buscar acción..." />
        <section className="chat-panel" aria-label="Chat de instrucciones">
          <header className="chat-thread-header">
            <div>
              <h1>{t('chat.page_title')}</h1>
              <p className="header-copy">{t('chat.header_copy')}</p>
            </div>
          </header>

          {error ? <ChatErrorMessage error={error} /> : null}

          {turns.length === 0 && !error ? (
            <EmptyState
              className="empty-state"
              title={t('chat.empty_state_title')}
              description={t('chat.empty_state_subtitle')}
              icon={<MessageSquareText size={20} aria-hidden="true" />}
            />
          ) : null}

          {turns.length > 0 ? (
            <div
              className="conversation-transcript"
              aria-label="Conversación activa"
              ref={transcriptRef}
              onScroll={handleTranscriptScroll}
            >
              {turns.map((turn) => {
                const draft = draftFor(turn)
                const isSendingTurnFeedback = sendingFeedbackTurnId === turn.id
                return (
                  <div className="conversation-turn" key={turn.id}>
                    <ChatMessage
                      author="user"
                      content={turn.question}
                      showAuthor={false}
                      className="chat-bubble chat-bubble-user"
                    />
                    <article
                      className="assistant-turn"
                      aria-label={turn.pending ? 'Respuesta en curso' : undefined}
                    >
                      <ChatMessage
                        author="assistant"
                        content={turn.answer || ' '}
                        pending={turn.pending}
                        showAuthor={false}
                        className="chat-bubble chat-bubble-assistant"
                      />
                      {turn.pending ? (
                        <p className="status-message" role="status">
                          {t('chat.preparing_answer')}
                        </p>
                      ) : (
                        <footer className="turn-meta">
                          <form
                            className="turn-feedback"
                            onSubmit={(event) => void submitTurnFeedback(turn, event)}
                          >
                            <div className="turn-actions">
                              <CitationSummaryButton
                                citations={turn.citations}
                                onOpen={openCitationDrawer}
                              />
                              {turn.queryAuditEventId ? (
                                <div className="feedback-actions" aria-label={t('chat.feedback_group')}>
                                  <button
                                    className={
                                      draft.value === 'up' ? 'feedback-button selected' : 'feedback-button'
                                    }
                                    type="button"
                                    aria-label={t('chat.feedback_helpful')}
                                    title={t('chat.feedback_helpful')}
                                    aria-pressed={draft.value === 'up'}
                                    onClick={() => toggleTurnFeedback(turn, 'up')}
                                  >
                                    <ThumbsUp size={15} aria-hidden="true" />
                                  </button>
                                  <button
                                    className={
                                      draft.value === 'down' ? 'feedback-button selected' : 'feedback-button'
                                    }
                                    type="button"
                                    aria-label={t('chat.feedback_not_helpful')}
                                    title={t('chat.feedback_not_helpful')}
                                    aria-pressed={draft.value === 'down'}
                                    onClick={() => toggleTurnFeedback(turn, 'down')}
                                  >
                                    <ThumbsDown size={15} aria-hidden="true" />
                                  </button>
                                </div>
                              ) : null}
                              {turn.cacheHit ? (
                                <span className="cache-badge">{t('chat.cache_badge')}</span>
                              ) : null}
                            </div>

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

                            {turn.queryAuditEventId && draft.value ? (
                              <div className="feedback-expansion">
                                <label className="field">
                                  <span>{t('chat.feedback_comment_label')}</span>
                                  <Textarea
                                    value={draft.comment}
                                    maxLength={1000}
                                    onChange={(event) => updateTurnFeedbackComment(turn, event.target.value)}
                                    disabled={isSendingTurnFeedback}
                                  />
                                </label>
                                <div className="feedback-submit-row">
                                  <Button type="submit" disabled={isSendingTurnFeedback}>
                                    {turn.feedbackValue || draft.saved
                                      ? t('chat.feedback_update')
                                      : t('chat.feedback_submit')}
                                  </Button>
                                  {draft.saved ? (
                                    <p className="status-message success" role="status">
                                      {t('chat.feedback_saved')}
                                    </p>
                                  ) : null}
                                </div>
                              </div>
                            ) : null}
                          </form>
                        </footer>
                      )}
                    </article>
                  </div>
                )
              })}
            </div>
          ) : null}

          <section className="question-form">
            <ChatComposer
              disabled={isSubmitting}
              maxLength={MaxQuestionChars}
              placeholder={t('chat.composer_placeholder')}
              submitLabel={t('chat.send_question')}
              pendingLabel={t('chat.sending')}
              characterCountLabel={(count, maxLength) => `${count} / ${maxLength}`}
              onSubmit={(nextQuestion) => void handleAsk(nextQuestion)}
            />
          </section>
        </section>
      </main>
      <CitationsPanel
        open={isCitationDrawerOpen}
        citations={drawerCitations}
        onClose={() => setIsCitationDrawerOpen(false)}
      />
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
  const { t } = useTranslation()

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
          {t('chat.session_new')}
        </Button>
      </div>

      <nav className="chat-history-nav" aria-label="Historial de conversaciones">
        <p className="eyebrow">{t('chat.session_history')}</p>
        {conversations.map((conversation) => (
          <button
            key={conversation.id}
            type="button"
            aria-current={conversation.id === activeConversationId ? 'page' : undefined}
            className="chat-history-item"
            onClick={() => onSelectConversation(conversation.id)}
          >
            <span>{conversation.title}</span>
            {conversation.isDraft ? <small>{t('chat.session_draft')}</small> : null}
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
          <DarkModeToggle label={t('common.toggle_theme')} />
        </div>
        {user ? (
          <section className="chat-sidebar-session" aria-label="Sesión activa">
            <span>{t('chat.active_session')}</span>
            <strong>{user.email}</strong>
            <span>{user.roles.join(', ')}</span>
          </section>
        ) : null}
        <Button type="button" variant="secondary" className="chat-logout-button" onClick={onLogout}>
          <LogOut size={16} aria-hidden="true" />
          {t('auth.logout')}
        </Button>
      </footer>
    </div>
  )
}

interface CitationsPanelItem {
  id: string
  title: string
  headingPath?: string[] | undefined
  onOpen?: () => void
}

function CitationsPanel({
  open,
  citations,
  onClose,
}: {
  open: boolean
  citations: CitationsPanelItem[]
  onClose: () => void
}) {
  const { t } = useTranslation()

  useEffect(() => {
    if (!open) {
      return
    }

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        onClose()
      }
    }

    window.addEventListener('keydown', handleKeyDown)
    return () => window.removeEventListener('keydown', handleKeyDown)
  }, [open, onClose])

  if (!open) {
    return null
  }

  return (
    <aside className="citations-panel" role="dialog" aria-label={t('chat.citations_title')}>
      <header className="citations-panel-header">
        <FileText size={16} aria-hidden="true" />
        <h2>{t('chat.citations_title')}</h2>
        <span className="citations-count">{citations.length}</span>
        <button
          type="button"
          className="citations-close"
          aria-label={t('chat.citations_close')}
          title={t('chat.citations_close')}
          onClick={onClose}
        >
          <X size={16} aria-hidden="true" />
        </button>
      </header>
      <p className="citations-panel-hint">{t('chat.citations_hint')}</p>
      <div className="citations-panel-list">
        {citations.map((citation) => (
          <button
            key={citation.id}
            type="button"
            className="citation-item"
            aria-label={t('chat.open_citation', { title: citation.title })}
            onClick={citation.onOpen}
          >
            <span className="citation-item-body">
              <span className="citation-item-title">{citation.title}</span>
              {citation.headingPath ? (
                <span className="citation-item-path">{citation.headingPath.join(' / ')}</span>
              ) : null}
            </span>
            <ExternalLink size={14} aria-hidden="true" />
          </button>
        ))}
      </div>
    </aside>
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

function removeHandoffFromUrl() {
  const url = new URL(window.location.href)
  url.searchParams.delete('handoff')
  window.history.replaceState(null, '', `${url.pathname}${url.search}${url.hash}`)
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
  const { t } = useTranslation()
  const unique = uniqueCitations(citations)
  if (unique.length === 0) {
    return null
  }

  return (
    <button type="button" className="citation-summary-button" onClick={() => onOpen(unique)}>
      <FileText size={15} aria-hidden="true" />
      {t('chat.citations_view', { n: unique.length })}
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
