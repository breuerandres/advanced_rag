import { useEffect, useState } from 'react'
import { AlertTriangle, Clock3, FileText, ShieldCheck } from 'lucide-react'
import {
  exchangeViewerCode,
  getViewerDocument,
  type ViewerDocument,
  viewerErrorMessage,
} from './api/viewer'
import './App.css'

type ViewerState =
  | { status: 'loading'; message: string }
  | { status: 'error'; message: string }
  | { status: 'ready'; document: ViewerDocument }

export default function App() {
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
    <main className="viewer-shell">
      <header className="viewer-header">
        <div>
          <p className="eyebrow">Advanced RAG</p>
          <h1>Visor de instrucciones</h1>
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
          <p className="eyebrow">{document.instructionType}</p>
          <h2>{document.title}</h2>
        </div>
        <dl className="document-meta">
          <div>
            <dt>Estado</dt>
            <dd>{displayState(document.state)}</dd>
          </div>
          <div>
            <dt>Audiencia</dt>
            <dd>{document.audience}</dd>
          </div>
        </dl>
      </header>
      <section
        className="document-content"
        dangerouslySetInnerHTML={{ __html: document.contentHtml }}
      />
    </article>
  )
}

function displayState(state: string) {
  const labels: Record<string, string> = {
    Published: 'Publicado',
    Draft: 'Borrador',
    'In Review': 'En revision',
  }
  return labels[state] ?? state
}
