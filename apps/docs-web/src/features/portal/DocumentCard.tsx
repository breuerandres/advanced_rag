import { createElement } from 'react'
import { useTranslation } from 'react-i18next'
import type { ViewerCatalogDocument } from '../../api/viewer'
import { formatRelativeDate } from '../../lib/dates'
import { documentStateKey } from '../../lib/documentState'
import { typeIcon, typeTintIndex } from '../../lib/documentType'

export function DocumentCard({
  document,
  onOpen,
}: {
  document: ViewerCatalogDocument
  onOpen: () => void
}) {
  const { t, i18n } = useTranslation()
  const locale = i18n.resolvedLanguage ?? i18n.language
  const stateKey = documentStateKey(document.state)
  const updated = formatRelativeDate(document.updatedAt, locale)

  return (
    <button
      type="button"
      className="document-card"
      onClick={onOpen}
      aria-label={t('portal.open_document', { title: document.title })}
    >
      <span className={`type-chip tint-${typeTintIndex(document.documentType)}`}>
        {createElement(typeIcon(document.documentType), { size: 13, 'aria-hidden': true })}
        {document.documentType}
      </span>
      <h2>{document.title}</h2>
      <span className="card-footer">
        {updated ? (
          <span className="card-updated">{t('portal.updated_ago', { when: updated })}</span>
        ) : null}
        {document.state !== 'Published' ? (
          <span className="state-chip">{stateKey ? t(stateKey) : document.state}</span>
        ) : null}
      </span>
    </button>
  )
}
