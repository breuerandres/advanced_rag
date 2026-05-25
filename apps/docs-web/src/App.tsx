import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { AlertTriangle, Clock3, FileText, Search, ShieldCheck } from 'lucide-react'
import { AppShell, DarkModeToggle, LanguageSelect } from '@helpcenter/shared-ui'
import {
  createViewerLink,
  exchangeViewerCode,
  getSession,
  getViewerDocument,
  listViewerDocuments,
  login,
  type SessionUser,
  type ViewerCatalogDocument,
  type ViewerDocument,
  type ViewerDocumentCatalog,
  viewerErrorMessage,
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
  const code = new URLSearchParams(window.location.search).get('code')
  return code ? <ViewerLinkApp /> : <DocumentPortalApp />
}

function ViewerLinkApp() {
  const { i18n } = useTranslation()
  const [state, setState] = useState<ViewerState>({
    status: 'loading',
    message: 'Validando enlace...',
  })

  useEffect(() => {
    let isMounted = true

    async function loadViewer() {
      try {
        const code = new URLSearchParams(window.location.search).get('code')
        if (code) {
          await exchangeViewerCode(code)
          clearExchangeCodeFromUrl()
        }

        const document = await getViewerDocument()
        if (isMounted) {
          setState({ status: 'ready', document })
        }
      } catch (error) {
        if (isMounted) {
          setState({ status: 'error', message: viewerErrorMessage(error) })
        }
      }
    }

    void loadViewer()
    return () => {
      isMounted = false
    }
  }, [])

  return (
    <AppShell className="viewer-main">
      <section className="viewer-shell" id="viewer">
        <header className="viewer-header">
          <div>
            <p className="eyebrow">Advanced RAG</p>
            <h1>Visor de instrucciones</h1>
          </div>
          <div className="viewer-toolbar">
            <LanguageSelect
              label="Idioma"
              value={i18n.resolvedLanguage ?? i18n.language}
              onChange={(value) => void i18n.changeLanguage(value)}
              options={[
                { value: 'es-AR', label: 'ES' },
                { value: 'en-US', label: 'EN' },
                { value: 'pt-BR', label: 'PT' },
              ]}
            />
            <DarkModeToggle label="Cambiar tema" />
          </div>
          <div className="trust-strip" aria-label="Estado de acceso">
            <span>
              <ShieldCheck size={16} />
              Acceso verificado
            </span>
            <span>
              <Clock3 size={16} />
              Token temporal
            </span>
          </div>
        </header>

        {state.status === 'loading' ? (
          <section className="state-panel" aria-live="polite">
            <FileText size={22} />
            <p>{state.message}</p>
          </section>
        ) : null}

        {state.status === 'error' ? (
          <section className="state-panel error" role="alert">
            <AlertTriangle size={22} />
            <p>{state.message}</p>
          </section>
        ) : null}

        {state.status === 'ready' ? <DocumentView document={state.document} /> : null}
      </section>
    </AppShell>
  )
}

function DocumentPortalApp() {
  const { i18n } = useTranslation()
  const [state, setState] = useState<PortalState>({ status: 'loading' })

  useEffect(() => {
    let isMounted = true

    async function loadPortal() {
      try {
        const session = await getSession()
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

        setState({ status: 'error', message: viewerErrorMessage(error) })
      }
    }

    void loadPortal()
    return () => {
      isMounted = false
    }
  }, [])

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
            <h1>Biblioteca de documentos</h1>
            <p className="header-copy">Documentos disponibles segun tus grupos y permisos.</p>
          </div>
          <div className="viewer-toolbar">
            <LanguageSelect
              label="Idioma"
              value={i18n.resolvedLanguage ?? i18n.language}
              onChange={(value) => void i18n.changeLanguage(value)}
              options={[
                { value: 'es-AR', label: 'ES' },
                { value: 'en-US', label: 'EN' },
                { value: 'pt-BR', label: 'PT' },
              ]}
            />
            <DarkModeToggle label="Cambiar tema" />
          </div>
        </header>

        {state.status === 'loading' ? (
          <section className="state-panel" aria-live="polite">
            <FileText size={22} />
            <p>Cargando biblioteca...</p>
          </section>
        ) : null}

        {state.status === 'error' ? (
          <section className="state-panel error" role="alert">
            <AlertTriangle size={22} />
            <p>{state.message}</p>
          </section>
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
          <input
            type="search"
            value={query}
            onChange={(event) => setQuery(event.target.value)}
            placeholder="Buscar por titulo, tipo o grupo"
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
        <section className="state-panel">
          <FileText size={22} />
          <p>No hay documentos para mostrar.</p>
        </section>
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
              <button className="primary-button" type="button" onClick={() => void openDocument(document)}>
                Abrir documento
              </button>
            </article>
          ))}
        </section>
      )}
    </>
  )
}

function DocsLoginPage({ onAuthenticated }: { onAuthenticated: (user: SessionUser) => void }) {
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
      setError(caught instanceof ApiError ? viewerErrorMessage(caught) : 'No se pudo iniciar sesion.')
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <main className="docs-auth-shell">
      <form className="docs-auth-card" onSubmit={submit}>
        <div className="auth-brand">
          <span className="brand-mark">AR</span>
          <span>Advanced RAG</span>
        </div>
        <header>
          <p className="eyebrow">Documentos</p>
          <h1>Iniciar sesion</h1>
        </header>
        {error ? <p className="status-message error">{error}</p> : null}
        <label className="field">
          <span>Email</span>
          <input type="email" value={email} onChange={(event) => setEmail(event.target.value)} />
        </label>
        <label className="field">
          <span>Contrasena</span>
          <input
            type="password"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
          />
        </label>
        <button className="primary-button" type="submit" disabled={isSubmitting}>
          Entrar
        </button>
      </form>
    </main>
  )
}

function clearExchangeCodeFromUrl() {
  const url = new URL(window.location.href)
  url.searchParams.delete('code')
  window.history.replaceState({}, '', `${url.pathname}${url.search}${url.hash}`)
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
              <dt>Token vigente hasta</dt>
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
