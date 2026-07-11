import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { AlertTriangle } from 'lucide-react'
import {
  AppShell,
  BrandMark,
  DarkModeToggle,
  EmptyState,
  LanguageSelect,
  Skeleton,
} from '@helpcenter/shared-ui'
import {
  consumeSessionHandoff,
  getSession,
  listViewerDocuments,
  viewerErrorKey,
  type SessionUser,
  type ViewerDocumentCatalog,
} from '../../api/viewer'
import { ApiError } from '../../lib/api-error'
import { removeHandoffFromUrl } from '../../lib/url'
import { DocsLoginPage } from '../auth/DocsLoginPage'
import { DocumentPortal } from './DocumentPortal'

type PortalState =
  | { status: 'loading' }
  | { status: 'login' }
  | { status: 'error'; messageKey: string }
  | { status: 'ready'; user: SessionUser; catalog: ViewerDocumentCatalog }

export function DocumentPortalApp({ handoffCode }: { handoffCode: string | null }) {
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

        setState({ status: 'error', messageKey: viewerErrorKey(error) })
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
      <section className="portal-shell">
        <header className="portal-topbar">
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
        </header>
        <div className="portal-intro">
          <h1>{t('portal.title')}</h1>
          <p>{t('portal.subtitle')}</p>
        </div>

        {state.status === 'loading' ? (
          <section className="document-grid" aria-hidden="true">
            {Array.from({ length: 6 }).map((_, index) => (
              <Skeleton key={index} className="card-skeleton" />
            ))}
          </section>
        ) : null}

        {state.status === 'error' ? (
          <EmptyState
            className="state-panel error"
            title={t(state.messageKey)}
            role="alert"
            icon={<AlertTriangle size={22} aria-hidden="true" />}
          />
        ) : null}

        {state.status === 'ready' ? <DocumentPortal user={state.user} catalog={state.catalog} /> : null}
      </section>
    </AppShell>
  )
}
