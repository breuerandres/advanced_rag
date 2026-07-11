import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { AlertTriangle, ArrowLeft, FileText } from 'lucide-react'
import { AppShell, BrandMark, DarkModeToggle, EmptyState, LanguageSelect } from '@helpcenter/shared-ui'
import {
  consumeViewerHandoff,
  getViewerDocument,
  viewerErrorKey,
  type ViewerDocument,
} from '../../api/viewer'
import { removeHandoffFromUrl } from '../../lib/url'
import { DocumentView } from './DocumentView'
import { DocChatWidget } from '../docChat/DocChatWidget'

type ViewerState =
  | { status: 'loading' }
  | { status: 'error'; messageKey: string }
  | { status: 'ready'; document: ViewerDocument }

export function ViewerLinkApp({
  documentId,
  handoffCode,
}: {
  documentId: string
  handoffCode: string | null
}) {
  const { t, i18n } = useTranslation()
  const [state, setState] = useState<ViewerState>({ status: 'loading' })

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
          setState({ status: 'error', messageKey: viewerErrorKey(error) })
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
      <div className="viewer-shell">
        <nav className="viewer-topbar" aria-label={t('viewer.nav_label')}>
          <a className="back-link" href="/">
            <ArrowLeft size={16} aria-hidden="true" />
            {t('viewer.back_to_library')}
          </a>
          <span className="brand">
            <BrandMark />
            <span className="brand-name">ReferentIA</span>
          </span>
          <div className="toolbar">
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
        </nav>

        {state.status === 'loading' ? (
          <EmptyState
            className="state-panel"
            title={t('viewer.loading')}
            aria-live="polite"
            icon={<FileText size={22} aria-hidden="true" />}
          />
        ) : null}

        {state.status === 'error' ? (
          <EmptyState
            className="state-panel error"
            title={t(state.messageKey)}
            role="alert"
            icon={<AlertTriangle size={22} aria-hidden="true" />}
          />
        ) : null}

        {state.status === 'ready' ? (
          <>
            <DocumentView document={state.document} />
            {state.document.state === 'Published' ? (
              <DocChatWidget documentId={state.document.documentId} />
            ) : null}
          </>
        ) : null}
      </div>
    </AppShell>
  )
}
