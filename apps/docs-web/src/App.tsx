import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { AlertTriangle, Clock3, FileText, ShieldCheck } from 'lucide-react'
import {
  AppShell,
  DarkModeToggle,
  EmptyState,
  LanguageSelect,
} from '@helpcenter/shared-ui'
import {
  consumeViewerHandoff,
  getViewerDocument,
  viewerErrorKey,
  type ViewerDocument,
} from './api/viewer'
import { removeHandoffFromUrl } from './lib/url'
import { DocumentPortalApp } from './features/portal/DocumentPortalApp'
import './i18n'
import './App.css'

type ViewerState =
  | { status: 'loading'; message: string }
  | { status: 'error'; message: string }
  | { status: 'ready'; document: ViewerDocument }

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
