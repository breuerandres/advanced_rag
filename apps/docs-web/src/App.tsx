import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { AlertTriangle, Clock3, FileText, Search, ShieldCheck } from 'lucide-react'
import {
  AppShell,
  AuthCardHeader,
  AuthShell,
  Button,
  DarkModeToggle,
  EmptyState,
  Input,
  LanguageSelect,
} from '@helpcenter/shared-ui'
import {
  consumeSessionHandoff,
  consumeViewerHandoff,
  createViewerLink,
  getSession,
  getViewerDocument,
  listViewerDocuments,
  login,
  type SessionUser,
  type ViewerCatalogDocument,
  type ViewerDocument,
  type ViewerDocumentCatalog,
  viewerErrorKey,
} from './api/viewer'
import { ApiError } from './lib/api-error'
import './i18n'
import './App.css'

type ViewerState =
  | { status: 'loading'; message: string }
  | { status: 'error'; message: string }
  | { status: 'ready'; document: ViewerDocument }

type PortalState =
  | { status: 'loading' }
  | { status: 'login' }
  | { status: 'error'; message: string }
  | { status: 'ready'; user: SessionUser; catalog: ViewerDocumentCatalog }

export default function App() {
  const params = new URLSearchParams(window.location.search)
  const documentId = params.get('documentId')
  const handoffCode = params.get('handoff')
  return documentId ? (
    <ViewerLinkApp documentId={documentId} handoffCode={handoffCode} />
  ) : (
    <DocumentPortalApp handoffCode={handoffCode} />
  )
}

function ViewerLinkApp({ documentId, handoffCode }: { documentId: string; handoffCode: string | null }) {
  const { t, i18n } = useTranslation()
  const [state, setState] = useState<ViewerState>({
    status: 'loading',
    message: 'viewer.loading',
  })

  useEffect(() => {
    let isMounted = true

    async function loadViewer() {
      try {
        if (handoffCode) {
          await consumeViewerHandoff(handoffCode, documentId)
          removeHandoffFromUrl()
        }

        const document = await getViewerDocument(documentId)
        if (isMounted) {
          setState({ status: 'ready', document })
        }
      } catch (error) {
        if (isMounted) {
          setState({ status: 'error', message: viewerErrorKey(error) })
        }
      }
    }

    void loadViewer()
    return () => {
      isMounted = false
    }
  }, [documentId, handoffCode])

  return (
    <AppShell className="viewer-main">
      <section className="viewer-shell" id="viewer">
        <header className="viewer-header">
          <div>
            <p className="eyebrow">Advanced RAG</p>
            <h1>{t('viewer.instruction_viewer_title')}</h1>
          </div>
          <div className="viewer-toolbar">
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
          <div className="trust-strip" aria-label="Estado de acceso">
            <span>
              <ShieldCheck size={16} />
              Acceso verificado
            </span>
            <span>
              <Clock3 size={16} />
              Sesion activa
            </span>
          </div>
        </header>

        {state.status === 'loading' ? (
          <EmptyState
            className="state-panel"
            title={t(state.message)}
            aria-live="polite"
            icon={<FileText size={22} aria-hidden="true" />}
          />
        ) : null}

        {state.status === 'error' ? (
          <EmptyState
            className="state-panel error"
            title={t(state.message)}
            role="alert"
            icon={<AlertTriangle size={22} aria-hidden="true" />}
          />
        ) : null}

        {state.status === 'ready' ? <DocumentView document={state.document} /> : null}
      </section>
    </AppShell>
  )
}

function removeHandoffFromUrl() {
  const url = new URL(window.location.href)
  url.searchParams.delete('handoff')
  window.history.replaceState(null, '', `${url.pathname}${url.search}${url.hash}`)
}

function DocumentPortalApp({ handoffCode }: { handoffCode: string | null }) {
  const { t, i18n } = useTranslation()
  const [state, setState] = useState<PortalState>({ status: 'loading' })

  useEffect(() => {
    let isMounted = true

    async function loadPortal() {
      try {
        const session = handoffCode ? await consumeSessionHandoff(handoffCode) : await getSession()
        if (handoffCode) {
          removeHandoffFromUrl()
        }
        const catalog = await listViewerDocuments()
        if (isMounted) {
          setState({ status: 'ready', user: session.user, catalog })
        }
      } catch (error) {
        if (!isMounted) {
          return
        }

        if (error instanceof ApiError && error.code === 'AUTH_REQUIRED') {
          setState({ status: 'login' })
          return
        }

        setState({ status: 'error', message: viewerErrorKey(error) })
      }
    }

    void loadPortal()
    return () => {
      isMounted = false
    }
  }, [handoffCode])

  async function loadAfterLogin(user: SessionUser) {
    const catalog = await listViewerDocuments()
    setState({ status: 'ready', user, catalog })
  }

  if (state.status === 'login') {
    return <DocsLoginPage onAuthenticated={(user) => void loadAfterLogin(user)} />
  }

  return (
    <AppShell className="viewer-main">
      <section className="viewer-shell portal-shell">
        <header className="portal-header">
          <div>
            <p className="eyebrow">Advanced RAG</p>
            <h1>{t('portal.title')}</h1>
            <p className="header-copy">{t('portal.header_copy')}</p>
          </div>
          <div className="viewer-toolbar">
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
        </header>

        {state.status === 'loading' ? (
          <EmptyState
            className="state-panel"
            title="Cargando biblioteca..."
            aria-live="polite"
            icon={<FileText size={22} aria-hidden="true" />}
          />
        ) : null}

        {state.status === 'error' ? (
          <EmptyState
            className="state-panel error"
            title={t(state.message)}
            role="alert"
            icon={<AlertTriangle size={22} aria-hidden="true" />}
          />
        ) : null}

        {state.status === 'ready' ? (
          <DocumentPortal user={state.user} catalog={state.catalog} />
        ) : null}
      </section>
    </AppShell>
  )
}

function DocumentPortal({ user, catalog }: { user: SessionUser; catalog: ViewerDocumentCatalog }) {
  const [selectedGroupId, setSelectedGroupId] = useState<string>('all')
  const [query, setQuery] = useState('')
  const isManager = user.roles.includes('Admin') || user.roles.includes('DocumentManager')
  const filteredDocuments = useMemo(() => {
    const normalizedQuery = query.trim().toLowerCase()
    return catalog.documents.filter((document) => {
      const matchesGroup =
        selectedGroupId === 'all' || document.allowedGroups.some((group) => group.id === selectedGroupId)
      const matchesQuery =
        normalizedQuery.length === 0 ||
        [document.title, document.documentType, document.audience, ...document.allowedGroups.map((group) => group.name)]
          .join(' ')
          .toLowerCase()
          .includes(normalizedQuery)

      return matchesGroup && matchesQuery
    })
  }, [catalog.documents, query, selectedGroupId])

  async function openDocument(document: ViewerCatalogDocument) {
    const purpose = isManager ? 'management' : 'chat'
    const url = await createViewerLink(document.id, purpose)
    window.location.assign(url)
  }

  return (
    <>
      <section className="portal-controls" aria-label="Filtros de documentos">
        <label className="search-control">
          <Search size={16} aria-hidden="true" />
          <span className="sr-only">Buscar documentos</span>
          <Input
            type="search"
            value={query}
            onChange={(event) => setQuery(event.target.value)}
            placeholder="Buscar por titulo, tipo o grupo"
            className="border-0 bg-transparent p-0 shadow-none focus-visible:ring-0 focus-visible:ring-offset-0"
          />
        </label>
        <div className="category-strip" aria-label="Categorias">
          <button
            className={selectedGroupId === 'all' ? 'category-button selected' : 'category-button'}
            type="button"
            onClick={() => setSelectedGroupId('all')}
          >
            Todos
          </button>
          {catalog.groups.map((group) => (
            <button
              key={group.id}
              className={selectedGroupId === group.id ? 'category-button selected' : 'category-button'}
              type="button"
              onClick={() => setSelectedGroupId(group.id)}
            >
              {group.name}
            </button>
          ))}
        </div>
      </section>

      {filteredDocuments.length === 0 ? (
        <EmptyState
          className="state-panel"
          title="Sin documentos para mostrar"
          description="No encontramos documentos disponibles con los filtros actuales."
          icon={<FileText size={22} aria-hidden="true" />}
        />
      ) : (
        <section className="document-grid" aria-label="Documentos">
          {filteredDocuments.map((document) => (
            <article className="document-card" key={document.id}>
              <div>
                <p className="eyebrow">{document.documentType}</p>
                <h2>{document.title}</h2>
                <p>{document.audience}</p>
              </div>
              <dl className="card-meta">
                <div>
                  <dt>Estado</dt>
                  <dd>{displayState(document.state)}</dd>
                </div>
                <div>
                  <dt>Grupos</dt>
                  <dd>{document.allowedGroups.map((group) => group.name).join(', ') || '-'}</dd>
                </div>
              </dl>
              <Button className="primary-button" type="button" onClick={() => void openDocument(document)}>
                Abrir documento
              </Button>
            </article>
          ))}
        </section>
      )}
    </>
  )
}

function DocsLoginPage({ onAuthenticated }: { onAuthenticated: (user: SessionUser) => void }) {
  const { t } = useTranslation()
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
    } catch {
      setError('login.failed')
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <AuthShell>
      <form className="auth-card" onSubmit={submit}>
        <AuthCardHeader eyebrow="Documentos" title="Iniciar sesion" />
        {error ? <p className="status-message error">{t(error)}</p> : null}
        <label className="field">
          <span>Email</span>
          <Input type="email" value={email} onChange={(event) => setEmail(event.target.value)} />
        </label>
        <label className="field">
          <span>Contrasena</span>
          <Input
            type="password"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
          />
        </label>
        <Button className="primary-button" type="submit" disabled={isSubmitting}>
          Entrar
        </Button>
      </form>
    </AuthShell>
  )
}

function DocumentView({ document }: { document: ViewerDocument }) {
  return (
    <article className="document-view">
      <header className="document-header">
        <div>
          <p className="eyebrow">{document.documentType}</p>
          <h2>{document.title}</h2>
        </div>
      </header>
      <div className="document-layout">
        <section
          className="document-content"
          dangerouslySetInnerHTML={{ __html: document.contentHtml }}
        />
        <aside className="document-side-rail" aria-label="Contexto del documento">
          <h3>Contexto</h3>
          <dl className="document-meta">
            <div>
              <dt>Estado</dt>
              <dd>{displayState(document.state)}</dd>
            </div>
            <div>
              <dt>Audiencia</dt>
              <dd>{document.audience}</dd>
            </div>
            <div>
              <dt>Sesion vigente hasta</dt>
              <dd>{formatDateTime(document.tokenExpiresAt)}</dd>
            </div>
          </dl>
        </aside>
      </div>
    </article>
  )
}

function formatDateTime(value: string) {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) {
    return '-'
  }

  return new Intl.DateTimeFormat('es-AR', {
    dateStyle: 'short',
    timeStyle: 'short',
  }).format(date)
}

function displayState(state: string) {
  const labels: Record<string, string> = {
    Published: 'Publicado',
    Draft: 'Borrador',
    'In Review': 'En revision',
  }
  return labels[state] ?? state
}
