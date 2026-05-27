import { useEffect, useId, useMemo, useState } from "react";
import {
  Archive,
  ExternalLink,
  Pencil,
  Plus,
  RefreshCw,
  RotateCcw,
  Search,
  Upload,
} from "lucide-react";
import { ApiError } from "../../lib/api-error";
import {
  archiveDocument,
  createDocumentDraft,
  createManagementViewerLink,
  getDocument,
  importDocumentText,
  listDocuments,
  requestPublish,
  restoreDocument,
  saveDocumentDraft,
  sendDocumentToReview,
} from "../../api/documents";
import type { DocumentDetail, DocumentSummary } from "../../api/documents";
import { listGroups, type GroupSummary } from "../../api/users";
import { RichTextEditor } from "./RichTextEditor";
import { Button, Checkbox, DataTable, Input } from "@helpcenter/shared-ui";

type DocumentStateFilter =
  | "all"
  | "Draft"
  | "In Review"
  | "Published"
  | "Archived";
type IndexingFilter = "all" | "None" | "Pending" | "Succeeded" | "Failed";
type AccessFilter = "all" | "with-groups" | "without-groups";
type DocumentWorkspaceTab = "list" | "editor";

interface EditorState {
  mode: "create" | "edit";
  document: DocumentDetail;
}

interface DocumentsPageProps {
  userRoles: string[];
}

export function DocumentsPage({ userRoles }: DocumentsPageProps) {
  const [documents, setDocuments] = useState<DocumentSummary[]>([]);
  const [groups, setGroups] = useState<GroupSummary[]>([]);
  const [loadState, setLoadState] = useState<"loading" | "ready" | "error">(
    "loading",
  );
  const [searchQuery, setSearchQuery] = useState("");
  const [stateFilter, setStateFilter] = useState<DocumentStateFilter>("all");
  const [indexingFilter, setIndexingFilter] = useState<IndexingFilter>("all");
  const [typeFilter, setTypeFilter] = useState("all");
  const [audienceFilter, setAudienceFilter] = useState("all");
  const [accessFilter, setAccessFilter] = useState<AccessFilter>("all");
  const [editorState, setEditorState] = useState<EditorState | null>(null);
  const [activeTab, setActiveTab] = useState<DocumentWorkspaceTab>("list");
  const [message, setMessage] = useState<string | null>(null);

  useEffect(() => {
    void loadDocuments();
  }, []);

  async function loadDocuments() {
    setLoadState("loading");
    try {
      const [loadedDocuments, loadedGroups] = await Promise.all([
        listDocuments(),
        listGroups(),
      ]);
      setDocuments(loadedDocuments);
      setGroups(loadedGroups);
      setLoadState("ready");
    } catch {
      setLoadState("error");
    }
  }

  const documentTypes = useMemo(
    () => uniqueValues(documents.map((document) => document.documentType)),
    [documents],
  );
  const audiences = useMemo(
    () => uniqueValues(documents.map((document) => document.audience)),
    [documents],
  );

  const filteredDocuments = useMemo(() => {
    const normalizedQuery = searchQuery.trim().toLowerCase();

    return documents.filter((document) => {
      const matchesState =
        stateFilter === "all" || document.state === stateFilter;
      const matchesIndexing =
        indexingFilter === "all" || document.indexingStatus === indexingFilter;
      const matchesType =
        typeFilter === "all" || document.documentType === typeFilter;
      const matchesAudience =
        audienceFilter === "all" || document.audience === audienceFilter;
      const matchesAccess =
        accessFilter === "all" ||
        (accessFilter === "with-groups"
          ? document.allowedGroupIds.length > 0
          : document.allowedGroupIds.length === 0);

      if (
        !matchesState ||
        !matchesIndexing ||
        !matchesType ||
        !matchesAudience ||
        !matchesAccess
      ) {
        return false;
      }

      if (normalizedQuery.length === 0) {
        return true;
      }

      const groupNames = document.allowedGroupIds.map((groupId) =>
        groupName(groups, groupId),
      );
      const searchableText = [
        document.title,
        document.state,
        document.indexingStatus,
        document.documentType,
        document.audience,
        document.updatedAt,
        ...groupNames,
      ]
        .join(" ")
        .toLowerCase();

      return searchableText.includes(normalizedQuery);
    });
  }, [
    accessFilter,
    audienceFilter,
    documents,
    groups,
    indexingFilter,
    searchQuery,
    stateFilter,
    typeFilter,
  ]);

  async function openDocument(id: string) {
    setMessage(null);
    setEditorState({ mode: "edit", document: await getDocument(id) });
    setActiveTab("editor");
  }

  function openCreate() {
    setMessage(null);
    setEditorState({ mode: "create", document: emptyDocument() });
    setActiveTab("editor");
  }

  function closeEditor() {
    setEditorState(null);
    setActiveTab("list");
  }

  async function runArchive(document: DocumentSummary) {
    const updated = await archiveDocument(document.id);
    setDocuments((current) =>
      current.map((item) =>
        item.id === document.id
          ? { ...toSummary(updated), indexingStatus: "None" }
          : item,
      ),
    );
    setMessage("Documento archivado.");
  }

  async function runRestore(document: DocumentSummary) {
    const updated = await restoreDocument(document.id);
    setDocuments((current) =>
      current.map((item) =>
        item.id === document.id
          ? { ...toSummary(updated), indexingStatus: "None" }
          : item,
      ),
    );
    setMessage("Documento restaurado.");
  }

  async function retryIndexing(document: DocumentSummary) {
    const updated = await requestPublish(document.id);
    setDocuments((current) =>
      current.map((item) =>
        item.id === document.id
          ? {
              ...toSummary(updated),
              indexingStatus:
                updated.currentDraftVersion?.indexingStatus ?? "Succeeded",
            }
          : item,
      ),
    );
    setMessage("Indexacion reintentada.");
  }

  async function openViewer(document: DocumentSummary) {
    setMessage(null);
    try {
      const link = await createManagementViewerLink(document.id);
      window.location.assign(link.url);
    } catch (error) {
      const reference = error instanceof ApiError ? error.requestId : "unknown";
      setMessage(`No se pudo abrir el visor. Referencia: ${reference}.`);
    }
  }

  function upsertDocument(updated: DocumentDetail, savedMessage: string) {
    const summary = toSummary(updated);
    setEditorState({ mode: "edit", document: updated });
    setActiveTab("editor");
    setMessage(savedMessage);
    setDocuments((current) => {
      const existing = current.some((item) => item.id === updated.id);
      return existing
        ? current.map((item) => (item.id === updated.id ? summary : item))
        : [summary, ...current];
    });
  }

  return (
    <>
      <section className="workspace" id="documentos">
        <header className="workspace-header">
          <div>
            <p className="eyebrow">Instrucciones</p>
            <h1>Documentos</h1>
          </div>
          <div className="workspace-actions">
            <Button
              className="primary-button"
              type="button"
              onClick={openCreate}
            >
              <Plus size={16} />
              Crear documento
            </Button>
            <Button
              className="icon-button"
              type="button"
              aria-label="Actualizar documentos"
              disabled={loadState === "loading"}
              onClick={() => void loadDocuments()}
            >
              <RefreshCw size={18} />
            </Button>
          </div>
        </header>

        <div
          className="workspace-tabs"
          role="tablist"
          aria-label="Secciones de documentos"
        >
          <button
            aria-selected={activeTab === "list"}
            className="workspace-tab"
            role="tab"
            type="button"
            onClick={() => setActiveTab("list")}
          >
            Listado
          </button>
          <button
            aria-selected={activeTab === "editor"}
            className="workspace-tab"
            disabled={!editorState}
            role="tab"
            type="button"
            onClick={() => {
              if (editorState) {
                setActiveTab("editor");
              }
            }}
          >
            Editor
          </button>
        </div>

        {message ? (
          <p className="status-message success" role="status">
            {message}
          </p>
        ) : null}

        {activeTab === "list" ? (
          <>
            <section
              className="document-filter-grid"
              aria-label="Filtros de documentos"
            >
              <label className="field filter-search">
                <span>Buscar documentos</span>
                <span className="search-control">
                  <Search size={16} />
                  <Input
                    type="search"
                    placeholder="Titulo, tipo, audiencia, grupo o fecha"
                    value={searchQuery}
                    onChange={(event) => setSearchQuery(event.target.value)}
                  />
                </span>
              </label>
              <label className="field">
                <span>Estado del documento</span>
                <select
                  value={stateFilter}
                  onChange={(event) =>
                    setStateFilter(event.target.value as DocumentStateFilter)
                  }
                >
                  <option value="all">Todos</option>
                  <option value="Draft">Borrador</option>
                  <option value="In Review">En revision</option>
                  <option value="Published">Publicado</option>
                  <option value="Archived">Archivado</option>
                </select>
              </label>
              <label className="field">
                <span>Indexacion</span>
                <select
                  value={indexingFilter}
                  onChange={(event) =>
                    setIndexingFilter(event.target.value as IndexingFilter)
                  }
                >
                  <option value="all">Todas</option>
                  <option value="None">Sin indexacion</option>
                  <option value="Pending">Pendiente</option>
                  <option value="Succeeded">Correcta</option>
                  <option value="Failed">Fallida</option>
                </select>
              </label>
              <label className="field">
                <span>Tipo</span>
                <select
                  value={typeFilter}
                  onChange={(event) => setTypeFilter(event.target.value)}
                >
                  <option value="all">Todos</option>
                  {documentTypes.map((type) => (
                    <option key={type} value={type}>
                      {type}
                    </option>
                  ))}
                </select>
              </label>
              <label className="field">
                <span>Audiencia</span>
                <select
                  value={audienceFilter}
                  onChange={(event) => setAudienceFilter(event.target.value)}
                >
                  <option value="all">Todas</option>
                  {audiences.map((audience) => (
                    <option key={audience} value={audience}>
                      {audience}
                    </option>
                  ))}
                </select>
              </label>
              <label className="field">
                <span>Acceso</span>
                <select
                  value={accessFilter}
                  onChange={(event) =>
                    setAccessFilter(event.target.value as AccessFilter)
                  }
                >
                  <option value="all">Todos</option>
                  <option value="with-groups">Con grupos</option>
                  <option value="without-groups">Sin grupos</option>
                </select>
              </label>
            </section>

            {loadState === "loading" ? (
              <p className="status-message">Cargando documentos...</p>
            ) : null}
            {loadState === "error" ? (
              <p className="status-message error" role="alert">
                No se pudo cargar la lista de documentos.
              </p>
            ) : null}
            {loadState === "ready" && documents.length === 0 ? (
              <p className="status-message">
                Todavia no hay documentos creados.
              </p>
            ) : null}
            {loadState === "ready" &&
            documents.length > 0 &&
            filteredDocuments.length === 0 ? (
              <p className="status-message">
                No hay documentos que coincidan con los filtros.
              </p>
            ) : null}

            {loadState === "ready" && filteredDocuments.length > 0 ? (
              <DataTable<DocumentSummary>
                className="table-frame documents-table"
                columns={[
                  {
                    key: "document",
                    header: "Documento",
                    rowHeader: true,
                    render: (document) => document.title,
                  },
                  {
                    key: "state",
                    header: "Estado",
                    render: (document) => (
                          <span className={`badge document-state ${stateClass(document.state)}`}>
                            {displayState(document.state)}
                          </span>
                    ),
                  },
                  {
                    key: "type",
                    header: "Tipo",
                    render: (document) => document.documentType || "-",
                  },
                  {
                    key: "audience",
                    header: "Audiencia",
                    render: (document) => document.audience || "-",
                  },
                  {
                    key: "access",
                    header: "Acceso",
                    render: (document) => displayGroups(groups, document.allowedGroupIds),
                  },
                  {
                    key: "indexing",
                    header: "Indexacion",
                    render: (document) => displayIndexing(document.indexingStatus),
                  },
                  {
                    key: "versions",
                    header: "Versiones",
                    render: (document) =>
                      `Borrador ${document.draftVersionNumber ?? "-"} / Publicada ${
                        document.publishedVersionNumber ?? "-"
                      }`,
                  },
                  {
                    key: "updated",
                    header: "Actualizado",
                    render: (document) => formatDate(document.updatedAt),
                  },
                  {
                    key: "actions",
                    header: "Acciones",
                    render: (document) => (
                          <div className="row-actions">
                            <Button
                              className="icon-button"
                              type="button"
                              aria-label={`Editar ${document.title}`}
                              onClick={() => void openDocument(document.id)}
                            >
                              <Pencil size={16} />
                            </Button>
                            {canOpenViewer(document) ? (
                              <Button
                                className="icon-button"
                                type="button"
                                aria-label={`Abrir visor de ${document.title}`}
                                onClick={() => void openViewer(document)}
                              >
                                <ExternalLink size={16} />
                              </Button>
                            ) : null}
                            {canRestore(document) ? (
                              <Button
                                className="icon-button"
                                type="button"
                                aria-label={`Restaurar ${document.title}`}
                                onClick={() => void runRestore(document)}
                              >
                                <RotateCcw size={16} />
                              </Button>
                            ) : null}
                            {canArchive(document, userRoles) ? (
                              <Button
                                className="icon-button"
                                type="button"
                                aria-label={`Archivar ${document.title}`}
                                onClick={() => void runArchive(document)}
                              >
                                <Archive size={16} />
                              </Button>
                            ) : null}
                            {canRetryIndexing(document, userRoles) ? (
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
                    ),
                  },
                ]}
                data={filteredDocuments}
                getRowId={(document) => document.id}
              />
            ) : null}
          </>
        ) : editorState ? (
          <DocumentEditor
            mode={editorState.mode}
            documentDetail={editorState.document}
            groups={groups}
            userRoles={userRoles}
            onClose={closeEditor}
            onSaved={upsertDocument}
          />
        ) : (
          <div className="empty-panel">
            <div>
              <h2>Editor</h2>
              <p>Abrí un documento o creá uno nuevo para editarlo.</p>
            </div>
          </div>
        )}
      </section>
    </>
  );
}

function DocumentEditor({
  mode,
  documentDetail,
  groups,
  userRoles,
  onClose,
  onSaved,
}: {
  mode: "create" | "edit";
  documentDetail: DocumentDetail;
  groups: GroupSummary[];
  userRoles: string[];
  onClose: () => void;
  onSaved: (document: DocumentDetail, message: string) => void;
}) {
  const draft = documentDetail.currentDraftVersion;
  const [title, setTitle] = useState(draft?.title ?? documentDetail.title);
  const [documentType, setDocumentType] = useState(draft?.documentType ?? "");
  const [audience, setAudience] = useState(draft?.audience ?? "");
  const [contentHtml, setContentHtml] = useState(draft?.contentHtml ?? "");
  const [allowedGroupIds, setAllowedGroupIds] = useState<string[]>(
    documentDetail.allowedGroupIds.map((groupId) => groupId.toString()),
  );
  const [validationError, setValidationError] = useState<string | null>(null);
  const [importMessage, setImportMessage] = useState<string | null>(null);
  const [importError, setImportError] = useState<string | null>(null);
  const [importFileName, setImportFileName] = useState<string | null>(null);
  const [isDirty, setIsDirty] = useState(mode === "create");
  const [isSaving, setIsSaving] = useState(false);
  const importInputId = useId();
  const importHelpId = useId();
  const permissions = documentActionPermissions(
    mode,
    documentDetail,
    userRoles,
  );
  const hasReviewValidationError =
    validationError ===
    "Completa titulo, tipo, audiencia, grupos y contenido antes de enviar a revision.";

  async function saveDraft() {
    setValidationError(null);
    setIsSaving(true);
    try {
      const request = {
        title,
        documentType,
        audience,
        contentHtml: normalizeEditorHtml(contentHtml),
        allowedGroupIds,
      };
      const updated =
        mode === "create"
          ? await createDocumentDraft(request)
          : await saveDocumentDraft(documentDetail.id, request);
      setIsDirty(false);
      onSaved(
        updated,
        mode === "create" ? "Documento creado." : "Borrador guardado.",
      );
    } finally {
      setIsSaving(false);
    }
  }

  async function sendToReview() {
    if (
      title.trim().length === 0 ||
      documentType.trim().length === 0 ||
      audience.trim().length === 0 ||
      plainText(contentHtml).length === 0 ||
      allowedGroupIds.length === 0
    ) {
      setValidationError(
        "Completa titulo, tipo, audiencia, grupos y contenido antes de enviar a revision.",
      );
      return;
    }

    if (mode === "create") {
      setValidationError("Guarda el borrador antes de enviarlo a revision.");
      return;
    }

    const updated = await sendDocumentToReview(documentDetail.id);
    onSaved(updated, "Documento enviado a revision.");
  }

  async function publishDocument() {
    setValidationError(null);
    setIsSaving(true);
    try {
      const updated = await requestPublish(documentDetail.id);
      onSaved(updated, "Documento publicado.");
    } catch (error) {
      const reference = error instanceof ApiError ? error.requestId : "unknown";
      setValidationError(`No se pudo publicar. Referencia: ${reference}.`);
    } finally {
      setIsSaving(false);
    }
  }

  async function runImport(file: File) {
    setImportMessage(null);
    setImportError(null);
    setImportFileName(file.name);
    if (file.size > MaxImportFileSizeBytes) {
      setImportError("El archivo supera el maximo de 10 MB.");
      return;
    }

    try {
      const result = await importDocumentText(file);
      setContentHtml(plainTextToParagraphHtml(result.text));
      setIsDirty(true);
      setImportMessage(
        `Texto importado desde ${result.metadata.originalFilename}.`,
      );
    } catch (error) {
      const reference = error instanceof ApiError ? error.requestId : "unknown";
      setImportError(
        `No se pudo extraer texto del archivo. Referencia: ${reference}.`,
      );
    }
  }

  function toggleGroup(groupId: string) {
    setAllowedGroupIds((current) =>
      current.includes(groupId)
        ? current.filter((selectedId) => selectedId !== groupId)
        : [...current, groupId],
    );
    setIsDirty(true);
  }

  return (
    <section
      className="document-editor-panel"
      aria-labelledby="document-editor-title"
    >
      <header className="document-editor-header">
        <div>
          <p className="eyebrow">Editor HTML</p>
          <h2 id="document-editor-title">
            {mode === "create" ? "Crear documento" : "Editar documento"}
          </h2>
        </div>
        <Button className="text-button" type="button" onClick={onClose}>
          Volver al listado
        </Button>
      </header>

      <form className="document-form">
        <div className="dialog-grid">
          <label className="field">
            <span>Titulo</span>
            <Input
              invalid={hasReviewValidationError && title.trim().length === 0}
              type="text"
              value={title}
              onChange={(event) => {
                setTitle(event.target.value);
                setIsDirty(true);
              }}
            />
          </label>
          <label className="field">
            <span>Tipo</span>
            <Input
              invalid={hasReviewValidationError && documentType.trim().length === 0}
              type="text"
              value={documentType}
              onChange={(event) => {
                setDocumentType(event.target.value);
                setIsDirty(true);
              }}
            />
          </label>
          <label className="field">
            <span>Audiencia</span>
            <Input
              invalid={hasReviewValidationError && audience.trim().length === 0}
              type="text"
              value={audience}
              onChange={(event) => {
                setAudience(event.target.value);
                setIsDirty(true);
              }}
            />
          </label>
          <div className="field import-field">
            <span id={`${importInputId}-label`}>Importar PDF o DOCX</span>
            <div className="file-upload-control">
              <input
                aria-describedby={importHelpId}
                aria-labelledby={`${importInputId}-label`}
                className="file-input-native"
                id={importInputId}
                type="file"
                accept=".pdf,.docx,application/pdf,application/vnd.openxmlformats-officedocument.wordprocessingml.document"
                onChange={(event) => {
                  const file = event.target.files?.[0];
                  if (file) {
                    void runImport(file);
                  }
                }}
              />
              <label className="file-upload-button" htmlFor={importInputId}>
                <Upload size={16} aria-hidden="true" />
                Seleccionar archivo
              </label>
              <span className="file-upload-filename">
                {importFileName ?? "Ningun archivo seleccionado"}
              </span>
            </div>
          </div>
        </div>

        <fieldset className="checkbox-list">
          <legend>Grupos con acceso</legend>
          {groups.length > 0 ? (
            groups.map((group) => (
              <Checkbox
                className="checkbox-field"
                key={group.id}
                label={group.name}
                checked={allowedGroupIds.includes(group.id)}
                onCheckedChange={() => toggleGroup(group.id)}
              />
            ))
          ) : (
            <p className="muted-copy">No hay grupos disponibles.</p>
          )}
        </fieldset>

        <RichTextEditor
          value={contentHtml}
          onChange={(value) => {
            setContentHtml(value);
            setIsDirty(true);
          }}
        />

        {isDirty ? <p className="status-message">Cambios sin guardar</p> : null}
        {validationError ? (
          <p className="status-message error" role="alert">
            {validationError}
          </p>
        ) : null}
        {importMessage ? (
          <p className="status-message success">{importMessage}</p>
        ) : null}
        {importError ? (
          <p className="status-message error" role="alert">
            {importError}
          </p>
        ) : null}

        <div className="dialog-actions">
          {permissions.canSendToReview ? (
            <Button
              className="text-button"
              type="button"
              disabled={isSaving}
              onClick={() => void sendToReview()}
            >
              Enviar a revision
            </Button>
          ) : null}
          {permissions.canPublish ? (
            <Button
              className="primary-button"
              type="button"
              disabled={isSaving}
              onClick={() => void publishDocument()}
            >
              {isSaving ? "Publicando..." : "Publicar"}
            </Button>
          ) : null}
          {permissions.canSaveDraft ? (
            <Button
              className="primary-button"
              type="button"
              disabled={isSaving}
              onClick={() => void saveDraft()}
            >
              {isSaving ? "Guardando..." : "Guardar borrador"}
            </Button>
          ) : null}
        </div>
      </form>
    </section>
  );
}

const MaxImportFileSizeBytes = 10 * 1024 * 1024;

function toSummary(document: DocumentDetail): DocumentSummary {
  const version =
    document.currentDraftVersion ?? document.currentPublishedVersion;
  return {
    id: document.id,
    title: document.title,
    state: document.state,
    documentType: version?.documentType ?? "",
    audience: version?.audience ?? "",
    allowedGroupIds: document.allowedGroupIds.map((groupId) =>
      groupId.toString(),
    ),
    draftVersionNumber: document.currentDraftVersion?.versionNumber ?? null,
    publishedVersionNumber:
      document.currentPublishedVersion?.versionNumber ?? null,
    indexingStatus: version?.indexingStatus ?? "None",
    updatedAt: document.updatedAt,
  };
}

function documentActionPermissions(
  mode: "create" | "edit",
  document: DocumentDetail,
  userRoles: string[],
) {
  const isAdmin = hasRole(userRoles, "Admin");
  const canManageDocuments = isAdmin || hasRole(userRoles, "DocumentManager");
  const draftState = document.currentDraftVersion?.state;

  return {
    canSaveDraft:
      canManageDocuments &&
      (mode === "create" ||
        document.state === "Draft" ||
        document.state === "Published"),
    canSendToReview:
      canManageDocuments &&
      mode === "edit" &&
      document.state === "Draft" &&
      draftState === "Draft",
    canPublish:
      isAdmin &&
      mode === "edit" &&
      document.state === "In Review" &&
      draftState === "In Review",
  };
}

function canOpenViewer(document: DocumentSummary) {
  return document.state !== "Archived";
}

function canRestore(document: DocumentSummary) {
  return document.state === "Archived";
}

function canArchive(document: DocumentSummary, userRoles: string[]) {
  if (document.state === "Archived") {
    return false;
  }

  if (hasRole(userRoles, "Admin")) {
    return true;
  }

  return (
    hasRole(userRoles, "DocumentManager") &&
    (document.state === "Draft" || document.state === "In Review") &&
    document.publishedVersionNumber === null
  );
}

function canRetryIndexing(document: DocumentSummary, userRoles: string[]) {
  return (
    hasRole(userRoles, "Admin") &&
    document.state === "In Review" &&
    document.indexingStatus === "Failed"
  );
}

function hasRole(userRoles: string[], role: string) {
  return userRoles.includes(role);
}

function emptyDocument(): DocumentDetail {
  return {
    id: "",
    title: "",
    state: "Draft",
    currentDraftVersion: {
      id: "",
      versionNumber: 1,
      state: "Draft",
      title: "",
      documentType: "",
      audience: "",
      contentHtml: "",
      indexingStatus: "None",
    },
    currentPublishedVersion: null,
    allowedGroupIds: [],
    updatedAt: new Date().toISOString(),
  };
}

function displayState(state: string) {
  const labels: Record<string, string> = {
    Draft: "Borrador",
    "In Review": "En revision",
    Published: "Publicado",
    Archived: "Archivado",
  };
  return labels[state] ?? state;
}

function stateClass(state: string) {
  const classes: Record<string, string> = {
    Draft: "draft",
    "In Review": "review",
    Published: "published",
    Archived: "archived",
  };

  return classes[state] ?? "inactive";
}

function displayIndexing(status: string) {
  const labels: Record<string, string> = {
    None: "-",
    Pending: "Indexacion pendiente",
    Succeeded: "Indexacion correcta",
    Failed: "Indexacion fallida",
  };
  return labels[status] ?? status;
}

function displayGroups(groups: GroupSummary[], allowedGroupIds: string[]) {
  if (allowedGroupIds.length === 0) {
    return "-";
  }

  return allowedGroupIds
    .map((groupId) => groupName(groups, groupId))
    .join(", ");
}

function groupName(groups: GroupSummary[], groupId: string) {
  return groups.find((group) => group.id === groupId)?.name ?? groupId;
}

function uniqueValues(values: string[]) {
  return [...new Set(values.map((value) => value.trim()).filter(Boolean))].sort(
    (a, b) => a.localeCompare(b),
  );
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat("es-AR", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
  }).format(new Date(value));
}

function normalizeEditorHtml(value: string) {
  const trimmed = value.trim();
  if (trimmed.length === 0) {
    return "";
  }

  return /<\/?[a-z][\s\S]*>/i.test(trimmed)
    ? trimmed
    : `<p>${escapeHtml(trimmed)}</p>`;
}

function plainTextToParagraphHtml(value: string) {
  const trimmed = value.trim();
  if (trimmed.length === 0) {
    return "";
  }

  return trimmed
    .split(/\r?\n{2,}/)
    .map((paragraph) => paragraph.trim())
    .filter(Boolean)
    .map(
      (paragraph) =>
        `<p>${escapeHtml(paragraph).replace(/\r?\n/g, "<br>")}</p>`,
    )
    .join("");
}

function plainText(html: string) {
  return html.replace(/<[^>]+>/g, "").trim();
}

function escapeHtml(value: string) {
  return value
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;");
}
