import { useEffect, useMemo, useState } from 'react'
import { Archive, ExternalLink, Pencil, RefreshCw, RotateCcw } from 'lucide-react'
import { ApiError } from '../../lib/api-error'
import {
  archiveDocument,
  createManagementViewerLink,
  getDocument,
  importDocumentText,
  listDocuments,
  requestPublish,
  restoreDocument,
  saveDocumentDraft,
  sendDocumentToReview,
} from '../../api/documents'
import type { DocumentDetail, DocumentSummary } from '../../api/documents'
import { Button } from '../../components/ui/button'

type DocumentStateFilter = 'all' | 'Draft' | 'In Review' | 'Published' | 'Archived'

export function DocumentsPage() {
  const [documents, setDocuments] = useState<DocumentSummary[]>([])
  const [stateFilter, setStateFilter] = useState<DocumentStateFilter>('all')
  const [selectedDocument, setSelectedDocument] = useState<DocumentDetail | null>(null)
  const [message, setMessage] = useState<string | null>(null)

  useEffect(() => {
    void loadDocuments()
  }, [])

  async function loadDocuments() {
    setDocuments(await listDocuments())
  }

  const filteredDocuments = useMemo(
    () =>
      documents.filter((document) =>
        stateFilter === 'all' ? true : document.state === stateFilter,
      ),
    [documents, stateFilter],
  )

  async function openDocument(id: string) {
    setMessage(null)
    setSelectedDocument(await getDocument(id))
  }

  async function runArchive(document: DocumentSummary) {
    const updated = await archiveDocument(document.id)
    setDocuments((current) =>
      current.map((item) =>
        item.id === document.id ? { ...item, state: updated.state, indexingStatus: 'None' } : item,
      ),
    )
    setMessage('Documento archivado.')
  }

  async function runRestore(document: DocumentSummary) {
    const updated = await restoreDocument(document.id)
    setDocuments((current) =>
      current.map((item) =>
        item.id === document.id ? { ...item, state: updated.state, indexingStatus: 'None' } : item,
      ),
    )
    setMessage('Documento restaurado.')
  }

  async function retryIndexing(document: DocumentSummary) {
    const updated = await requestPublish(document.id)
    setDocuments((current) =>
      current.map((item) =>
        item.id === document.id
          ? {
              ...item,
              state: updated.state,
              indexingStatus: updated.currentDraftVersion?.indexingStatus ?? 'Succeeded',
            }
          : item,
      ),
    )
    setMessage('Indexacion reintentada.')
  }

  async function openViewer(document: DocumentSummary) {
    setMessage(null)
    try {
      const link = await createManagementViewerLink(document.id)
      window.location.assign(link.url)
    } catch (error) {
      const reference = error instanceof ApiError ? error.requestId : 'unknown'
      setMessage(`No se pudo abrir el visor. Referencia: ${reference}.`)
    }
  }

  return (
    <>
      <section className="workspace" id="documentos">
        <header className="workspace-header">
          <div>
            <p className="eyebrow">Instrucciones</p>
            <h1>Documentos</h1>
          </div>
        </header>

        <section className="filter-bar" aria-label="Filtros de documentos">
          <label className="field filter-status">
            <span>Estado del documento</span>
            <select
              value={stateFilter}
              onChange={(event) => setStateFilter(event.target.value as DocumentStateFilter)}
            >
              <option value="all">Todos</option>
              <option value="Draft">Borrador</option>
              <option value="In Review">En revision</option>
              <option value="Published">Publicado</option>
              <option value="Archived">Archivado</option>
            </select>
          </label>
        </section>

        {message ? (
          <p className="status-message success" role="status">
            {message}
          </p>
        ) : null}

        <div className="table-frame">
          <table>
            <thead>
              <tr>
                <th scope="col">Documento</th>
                <th scope="col">Estado</th>
                <th scope="col">Indexacion</th>
                <th scope="col">Versiones</th>
                <th scope="col">Acciones</th>
              </tr>
            </thead>
            <tbody>
              {filteredDocuments.map((document) => (
                <tr key={document.id}>
                  <th scope="row">{document.title}</th>
                  <td>{displayState(document.state)}</td>
                  <td>{displayIndexing(document.indexingStatus)}</td>
                  <td>
                    Borrador {document.draftVersionNumber ?? '-'} / Publicada{' '}
                    {document.publishedVersionNumber ?? '-'}
                  </td>
                  <td>
                    <div className="row-actions">
                      <Button
                        className="icon-button"
                        type="button"
                        aria-label={`Editar ${document.title}`}
                        onClick={() => void openDocument(document.id)}
                      >
                        <Pencil size={16} />
                      </Button>
                      <Button
                        className="icon-button"
                        type="button"
                        aria-label={`Abrir visor de ${document.title}`}
                        onClick={() => void openViewer(document)}
                      >
                        <ExternalLink size={16} />
                      </Button>
                      {document.state === 'Archived' ? (
                        <Button
                          className="icon-button"
                          type="button"
                          aria-label={`Restaurar ${document.title}`}
                          onClick={() => void runRestore(document)}
                        >
                          <RotateCcw size={16} />
                        </Button>
                      ) : (
                        <Button
                          className="icon-button"
                          type="button"
                          aria-label={`Archivar ${document.title}`}
                          onClick={() => void runArchive(document)}
                        >
                          <Archive size={16} />
                        </Button>
                      )}
                      {document.indexingStatus === 'Failed' ? (
                        <Button
                          className="icon-button"
                          type="button"
                          aria-label={`Reintentar indexacion de ${document.title}`}
                          onClick={() => void retryIndexing(document)}
                        >
                          <RefreshCw size={16} />
                        </Button>
                      ) : null}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>

      {selectedDocument ? (
        <DocumentEditor
          document={selectedDocument}
          onClose={() => setSelectedDocument(null)}
          onSaved={(updated, savedMessage) => {
            setSelectedDocument(updated)
            setMessage(savedMessage)
            setDocuments((current) =>
              current.map((item) =>
                item.id === updated.id
                  ? {
                      ...item,
                      title: updated.title,
                      state: updated.state,
                      indexingStatus: updated.currentDraftVersion?.indexingStatus ?? 'None',
                    }
                  : item,
              ),
            )
          }}
        />
      ) : null}
    </>
  )
}

function DocumentEditor({
  document,
  onClose,
  onSaved,
}: {
  document: DocumentDetail
  onClose: () => void
  onSaved: (document: DocumentDetail, message: string) => void
}) {
  const draft = document.currentDraftVersion
  const [title, setTitle] = useState(draft?.title ?? document.title)
  const [instructionType, setInstructionType] = useState(draft?.instructionType ?? '')
  const [audience, setAudience] = useState(draft?.audience ?? '')
  const [contentHtml, setContentHtml] = useState(stripHtml(draft?.contentHtml ?? ''))
  const [allowedGroupIds] = useState(document.allowedGroupIds)
  const [validationError, setValidationError] = useState<string | null>(null)
  const [importMessage, setImportMessage] = useState<string | null>(null)
  const [importError, setImportError] = useState<string | null>(null)
  const [isDirty, setIsDirty] = useState(false)

  async function saveDraft() {
    setValidationError(null)
    const updated = await saveDocumentDraft(document.id, {
      title,
      instructionType,
      audience,
      contentHtml,
      allowedGroupIds,
    })
    setIsDirty(false)
    onSaved(updated, 'Borrador guardado.')
  }

  async function sendToReview() {
    if (
      title.trim().length === 0 ||
      instructionType.trim().length === 0 ||
      audience.trim().length === 0 ||
      contentHtml.trim().length === 0 ||
      allowedGroupIds.length === 0
    ) {
      setValidationError(
        'Completa titulo, tipo, audiencia, grupos y contenido antes de enviar a revision.',
      )
      return
    }

    const updated = await sendDocumentToReview(document.id)
    onSaved(updated, 'Documento enviado a revision.')
  }

  async function runImport(file: File) {
    setImportMessage(null)
    setImportError(null)
    try {
      const result = await importDocumentText(file)
      setContentHtml(result.text)
      setIsDirty(true)
      setImportMessage(`Texto importado desde ${result.metadata.originalFilename}.`)
    } catch (error) {
      const reference = error instanceof ApiError ? error.requestId : 'unknown'
      setImportError(`No se pudo extraer texto del archivo. Referencia: ${reference}.`)
    }
  }

  return (
    <div className="dialog-backdrop">
      <section className="dialog document-dialog" aria-modal="true" role="dialog">
        <header className="dialog-header">
          <div>
            <p className="eyebrow">Editor</p>
            <h2>Editar documento</h2>
          </div>
          <Button className="text-button" type="button" onClick={onClose}>
            Cerrar
          </Button>
        </header>

        <form className="document-form">
          <label className="field">
            <span>Titulo</span>
            <input
              type="text"
              value={title}
              onChange={(event) => {
                setTitle(event.target.value)
                setIsDirty(true)
              }}
            />
          </label>
          <label className="field">
            <span>Tipo</span>
            <input
              type="text"
              value={instructionType}
              onChange={(event) => {
                setInstructionType(event.target.value)
                setIsDirty(true)
              }}
            />
          </label>
          <label className="field">
            <span>Audiencia</span>
            <input
              type="text"
              value={audience}
              onChange={(event) => {
                setAudience(event.target.value)
                setIsDirty(true)
              }}
            />
          </label>
          <label className="field">
            <span>Contenido</span>
            <textarea
              value={contentHtml}
              onChange={(event) => {
                setContentHtml(event.target.value)
                setIsDirty(true)
              }}
            />
          </label>
          <label className="field">
            <span>Importar PDF o DOCX</span>
            <input
              type="file"
              accept=".pdf,.docx,application/pdf,application/vnd.openxmlformats-officedocument.wordprocessingml.document"
              onChange={(event) => {
                const file = event.target.files?.[0]
                if (file) {
                  void runImport(file)
                }
              }}
            />
          </label>

          {isDirty ? <p className="status-message">Cambios sin guardar</p> : null}
          {validationError ? (
            <p className="status-message error" role="alert">
              {validationError}
            </p>
          ) : null}
          {importMessage ? <p className="status-message success">{importMessage}</p> : null}
          {importError ? (
            <p className="status-message error" role="alert">
              {importError}
            </p>
          ) : null}

          <div className="dialog-actions">
            <Button className="text-button" type="button" onClick={() => void sendToReview()}>
              Enviar a revision
            </Button>
            <Button className="primary-button" type="button" onClick={() => void saveDraft()}>
              Guardar borrador
            </Button>
          </div>
        </form>
      </section>
    </div>
  )
}

function displayState(state: string) {
  const labels: Record<string, string> = {
    Draft: 'Borrador',
    'In Review': 'En revision',
    Published: 'Publicado',
    Archived: 'Archivado',
  }
  return labels[state] ?? state
}

function displayIndexing(status: string) {
  const labels: Record<string, string> = {
    None: '-',
    Pending: 'Indexacion pendiente',
    Succeeded: 'Indexacion correcta',
    Failed: 'Indexacion fallida',
  }
  return labels[status] ?? status
}

function stripHtml(html: string) {
  return html.replace(/<[^>]+>/g, '').trim()
}
