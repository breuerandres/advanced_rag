import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { FileText, Search } from 'lucide-react'
import { Button, EmptyState, Input } from '@helpcenter/shared-ui'
import {
  createViewerLink,
  type SessionUser,
  type ViewerCatalogDocument,
  type ViewerDocumentCatalog,
} from '../../api/viewer'
import { DocumentCard } from './DocumentCard'

const MANAGEMENT_ROLES: readonly string[] = ['Admin', 'DocumentEditor', 'DocumentPublisher']

export function DocumentPortal({
  user,
  catalog,
}: {
  user: SessionUser
  catalog: ViewerDocumentCatalog
}) {
  const { t } = useTranslation()
  const [selectedType, setSelectedType] = useState<string>('all')
  const [query, setQuery] = useState('')
  const isManagementUser = user.roles.some((role) => MANAGEMENT_ROLES.includes(role))

  const documentTypes = useMemo(
    () =>
      [...new Set(catalog.documents.map((document) => document.documentType))].sort((a, b) =>
        a.localeCompare(b),
      ),
    [catalog.documents],
  )

  const filteredDocuments = useMemo(() => {
    const normalizedQuery = query.trim().toLowerCase()
    return catalog.documents.filter((document) => {
      const matchesType = selectedType === 'all' || document.documentType === selectedType
      const matchesQuery =
        normalizedQuery.length === 0 ||
        [document.title, document.documentType, document.audience]
          .join(' ')
          .toLowerCase()
          .includes(normalizedQuery)
      return matchesType && matchesQuery
    })
  }, [catalog.documents, query, selectedType])

  async function openDocument(document: ViewerCatalogDocument) {
    const purpose = isManagementUser ? 'management' : 'chat'
    const url = await createViewerLink(document.id, purpose)
    window.location.assign(url)
  }

  const hasDocuments = catalog.documents.length > 0
  const hasMatches = filteredDocuments.length > 0

  return (
    <>
      <section className="portal-controls" aria-label={t('portal.filters_label')}>
        <label className="search-control">
          <Search size={16} aria-hidden="true" />
          <span className="sr-only">{t('portal.search_placeholder')}</span>
          <Input
            type="search"
            value={query}
            onChange={(event) => setQuery(event.target.value)}
            placeholder={t('portal.search_placeholder')}
            className="border-0 bg-transparent p-0 shadow-none focus-visible:ring-0 focus-visible:ring-offset-0"
          />
        </label>
        <div className="category-strip">
          <button
            type="button"
            className={selectedType === 'all' ? 'category-button selected' : 'category-button'}
            onClick={() => setSelectedType('all')}
          >
            {t('portal.filter_all')}
          </button>
          {documentTypes.map((type) => (
            <button
              key={type}
              type="button"
              className={selectedType === type ? 'category-button selected' : 'category-button'}
              onClick={() => setSelectedType(type)}
            >
              {type}
            </button>
          ))}
        </div>
      </section>

      {!hasMatches ? (
        <>
          <EmptyState
            className="state-panel"
            title={t('portal.empty_title')}
            description={hasDocuments ? t('portal.empty_filtered') : t('portal.empty_no_access')}
            icon={<FileText size={22} aria-hidden="true" />}
          />
          {hasDocuments ? (
            <div className="empty-actions">
              <Button
                type="button"
                className="ghost-button"
                onClick={() => {
                  setSelectedType('all')
                  setQuery('')
                }}
              >
                {t('portal.clear_filters')}
              </Button>
            </div>
          ) : null}
        </>
      ) : (
        <section className="document-grid" aria-label={t('portal.title')}>
          {filteredDocuments.map((document) => (
            <DocumentCard
              key={document.id}
              document={document}
              onOpen={() => void openDocument(document)}
            />
          ))}
        </section>
      )}
    </>
  )
}
