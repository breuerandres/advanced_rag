import { createElement } from 'react'
import { useTranslation } from 'react-i18next'
import type { ViewerDocument } from '../../api/viewer'
import { formatDateTime } from '../../lib/dates'
import { documentStateKey } from '../../lib/documentState'
import { typeIcon, typeTintIndex } from '../../lib/documentType'

export function DocumentView({ document }: { document: ViewerDocument }) {
  const { t, i18n } = useTranslation()
  const locale = i18n.resolvedLanguage ?? i18n.language
  const stateKey = documentStateKey(document.state)

  return (
    <article className="document-view">
      {document.state !== 'Published' ? (
        <p className="state-banner" role="status">
          {document.state === 'Draft' ? t('viewer.draft_banner') : t('viewer.review_banner')}
        </p>
      ) : null}
      <header className="document-titleblock">
        <div className="document-chips">
          <span className={`type-chip tint-${typeTintIndex(document.documentType)}`}>
            {createElement(typeIcon(document.documentType), { size: 13, 'aria-hidden': true })}
            {document.documentType}
          </span>
          {document.state !== 'Published' && stateKey ? (
            <span className="state-chip">{t(stateKey)}</span>
          ) : null}
        </div>
        <h1>{document.title}</h1>
      </header>
      <section
        className="document-content"
        dangerouslySetInnerHTML={{ __html: document.contentHtml }}
      />
      <footer className="document-footer">
        {t('viewer.session_valid_until', { time: formatDateTime(document.tokenExpiresAt, locale) })}
      </footer>
    </article>
  )
}
