import { Fragment, useEffect, useId, useMemo, useState } from "react";
import type { FormEvent } from "react";
import { useTranslation } from "react-i18next";
import {
  Archive,
  ExternalLink,
  FolderPlus,
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
  uploadDocumentImage,
} from "../../api/documents";
import type {
  DocumentAccessRuleInput,
  DocumentDetail,
  DocumentSummary,
} from "../../api/documents";
import { createGroup, listGroups, type GroupSummary } from "../../api/users";
import { listOrganizationalUnits } from "../../api/orgUnits";
import type { OrganizationalUnitSummary } from "../../api/orgUnits";
import { UnitLevelBadge } from "../orgUnits/UnitLevelBadge";
import { RichTextEditor } from "./RichTextEditor";
import { Button, Checkbox, DataTable, Dialog, Input, Select } from "@helpcenter/shared-ui";
import type { SelectOption } from "@helpcenter/shared-ui";

type DocumentStateFilter =
  | "all"
  | "Draft"
  | "In Review"
  | "Published"
  | "Archived";
type IndexingFilter = "all" | "None" | "Pending" | "Succeeded" | "Failed";
type DocumentWorkspaceTab = "list" | "editor";

// Local editing shape: the stable key keeps React state and per-rule UI
// attached to the right card when rules are removed in the middle.
interface EditableAccessRule extends DocumentAccessRuleInput {
  key: string;
}

interface EditorState {
  mode: "create" | "edit";
  document: DocumentDetail;
}

interface DocumentsPageProps {
  userRoles: string[];
}

export function DocumentsPage({ userRoles }: DocumentsPageProps) {
  const { t, i18n } = useTranslation();
  const [documents, setDocuments] = useState<DocumentSummary[]>([]);
  const [groups, setGroups] = useState<GroupSummary[]>([]);
  const [organizationalUnits, setOrganizationalUnits] = useState<
    OrganizationalUnitSummary[]
  >([]);
  const [loadState, setLoadState] = useState<"loading" | "ready" | "error">(
    "loading",
  );
  const [searchQuery, setSearchQuery] = useState("");
  const [stateFilter, setStateFilter] = useState<DocumentStateFilter>("all");
  const [indexingFilter, setIndexingFilter] = useState<IndexingFilter>("all");
  const [typeFilter, setTypeFilter] = useState("all");
  const [audienceFilter, setAudienceFilter] = useState("all");
  const [unitFilter, setUnitFilter] = useState("all");
  const [groupFilter, setGroupFilter] = useState("all");
  const [editorState, setEditorState] = useState<EditorState | null>(null);
  const [activeTab, setActiveTab] = useState<DocumentWorkspaceTab>("list");
  const [message, setMessage] = useState<string | null>(null);

  useEffect(() => {
    void loadDocuments();
  }, []);

  async function loadDocuments() {
    setLoadState("loading");
    try {
      const [loadedDocuments, loadedGroups, loadedUnits] = await Promise.all([
        listDocuments(),
        listGroups(),
        listOrganizationalUnits(),
      ]);
      setDocuments(loadedDocuments);
      setGroups(loadedGroups);
      setOrganizationalUnits(sortUnitsInTreeOrder(loadedUnits));
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
      const accessRules = document.accessRules ?? [];
      const matchesUnit =
        unitFilter === "all" ||
        accessRules.some((rule) => rule.organizationalUnitId === unitFilter);
      const matchesGroup =
        groupFilter === "all" ||
        accessRules.some((rule) => rule.groupIds.includes(groupFilter)) ||
        document.allowedGroupIds.includes(groupFilter);

      if (
        !matchesState ||
        !matchesIndexing ||
        !matchesType ||
        !matchesAudience ||
        !matchesUnit ||
        !matchesGroup
      ) {
        return false;
      }

      if (normalizedQuery.length === 0) {
        return true;
      }

      const groupNames = document.allowedGroupIds.map((groupId) =>
        groupName(groups, groupId),
      );
      const ruleGroupNames = accessRules.flatMap((rule) =>
        rule.groupIds.map((groupId) => groupName(groups, groupId)),
      );
      const ruleUnitNames = accessRules
        .map((rule) => rule.organizationalUnitId)
        .filter((unitId): unitId is string => unitId !== null)
        .map((unitId) =>
          unitDisplayName(
            organizationalUnits,
            unitId,
            t("documents.rule_company_wide_label"),
            t("documents.rule_unit_unavailable"),
          ),
        );
      const searchableText = [
        document.title,
        document.state,
        document.indexingStatus,
        document.documentType,
        document.audience,
        document.updatedAt,
        ...groupNames,
        ...ruleGroupNames,
        ...ruleUnitNames,
      ]
        .join(" ")
        .toLowerCase();

      return searchableText.includes(normalizedQuery);
    });
  }, [
    audienceFilter,
    documents,
    groupFilter,
    groups,
    indexingFilter,
    organizationalUnits,
    searchQuery,
    stateFilter,
    t,
    typeFilter,
    unitFilter,
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

  function addGroup(group: GroupSummary) {
    setGroups((current) =>
      current.some((item) => item.id === group.id)
        ? current
        : [...current, group].sort((left, right) =>
            left.name.localeCompare(right.name),
          ),
    );
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
    setMessage(t("documents.archived_message"));
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
    setMessage(t("documents.restored_message"));
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
    setMessage(t("documents.retry_message"));
  }

  async function openViewer(document: DocumentSummary) {
    setMessage(null);
    try {
      const link = await createManagementViewerLink(document.id);
      window.open(link.url, "_blank", "noopener,noreferrer");
    } catch (error) {
      const reference = error instanceof ApiError ? error.requestId : "unknown";
      setMessage(t("documents.viewer_error", { reference }));
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
            <p className="eyebrow">{t("documents.eyebrow")}</p>
            <h1>{t("documents.title")}</h1>
          </div>
          <div className="workspace-actions">
            <Button
              className="primary-button"
              type="button"
              onClick={openCreate}
            >
              <Plus size={16} />
              {t("documents.create")}
            </Button>
            <Button
              className="icon-button"
              type="button"
              aria-label={t("documents.refresh")}
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
          aria-label={t("documents.sections_label")}
        >
          <button
            aria-selected={activeTab === "list"}
            className="workspace-tab"
            role="tab"
            type="button"
            onClick={() => setActiveTab("list")}
          >
            {t("documents.list")}
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
            {t("documents.editor")}
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
              aria-label={t("documents.filters_label")}
            >
              <label className="field filter-search">
                <span>{t("documents.search_documents")}</span>
                <span className="search-control">
                  <Search size={16} />
                  <Input
                    type="search"
                    placeholder={t("documents.search_placeholder")}
                    value={searchQuery}
                    onChange={(event) => setSearchQuery(event.target.value)}
                  />
                </span>
              </label>
              <label className="field">
                  <span>{t("documents.state_filter")}</span>
                <select
                  value={stateFilter}
                  onChange={(event) =>
                    setStateFilter(event.target.value as DocumentStateFilter)
                  }
                >
                  <option value="all">{t("documents.all")}</option>
                  <option value="Draft">{t("documents.state_draft")}</option>
                  <option value="In Review">{t("documents.state_in_review")}</option>
                  <option value="Published">{t("documents.state_published")}</option>
                  <option value="Archived">{t("documents.state_archived")}</option>
                </select>
              </label>
              <label className="field">
                <span>{t("documents.indexing")}</span>
                <select
                  value={indexingFilter}
                  onChange={(event) =>
                    setIndexingFilter(event.target.value as IndexingFilter)
                  }
                >
                  <option value="all">{t("documents.all_feminine")}</option>
                  <option value="None">{t("documents.indexing_none")}</option>
                  <option value="Pending">{t("documents.indexing_pending_short")}</option>
                  <option value="Succeeded">{t("documents.indexing_succeeded_short")}</option>
                  <option value="Failed">{t("documents.indexing_failed_short")}</option>
                </select>
              </label>
              <label className="field">
                  <span>{t("documents.type_column")}</span>
                <select
                  value={typeFilter}
                  onChange={(event) => setTypeFilter(event.target.value)}
                >
                  <option value="all">{t("documents.all")}</option>
                  {documentTypes.map((type) => (
                    <option key={type} value={type}>
                      {type}
                    </option>
                  ))}
                </select>
              </label>
              <label className="field">
                <span>{t("documents.audience")}</span>
                <select
                  value={audienceFilter}
                  onChange={(event) => setAudienceFilter(event.target.value)}
                >
                  <option value="all">{t("documents.all_feminine")}</option>
                  {audiences.map((audience) => (
                    <option key={audience} value={audience}>
                      {audience}
                    </option>
                  ))}
                </select>
              </label>
              <label className="field">
                <span>{t("documents.filter_unit")}</span>
                <select
                  value={unitFilter}
                  onChange={(event) => setUnitFilter(event.target.value)}
                >
                  <option value="all">{t("documents.all_feminine")}</option>
                  {organizationalUnits.map((unit) => (
                    <option key={unit.id} value={unit.id}>
                      {organizationalUnitOptionLabel(
                        unit,
                        t("documents.rule_company_wide"),
                      )}
                    </option>
                  ))}
                </select>
              </label>
              <label className="field">
                <span>{t("documents.filter_group")}</span>
                <select
                  value={groupFilter}
                  onChange={(event) => setGroupFilter(event.target.value)}
                >
                  <option value="all">{t("documents.all")}</option>
                  {groups.map((group) => (
                    <option key={group.id} value={group.id}>
                      {group.name}
                    </option>
                  ))}
                </select>
              </label>
            </section>

            {loadState === "loading" ? (
              <p className="status-message">{t("documents.loading")}</p>
            ) : null}
            {loadState === "error" ? (
              <p className="status-message error" role="alert">
                {t("documents.load_error")}
              </p>
            ) : null}
            {loadState === "ready" && documents.length === 0 ? (
              <p className="status-message">
                {t("documents.empty")}
              </p>
            ) : null}
            {loadState === "ready" &&
            documents.length > 0 &&
            filteredDocuments.length === 0 ? (
              <p className="status-message">
                {t("documents.empty_filtered")}
              </p>
            ) : null}

            {loadState === "ready" && filteredDocuments.length > 0 ? (
              <DataTable<DocumentSummary>
                className="table-frame documents-table"
                columns={[
                  {
                    key: "document",
                    header: t("documents.document_column"),
                    rowHeader: true,
                    render: (document) => document.title,
                  },
                  {
                    key: "state",
                    header: t("documents.state_column"),
                    render: (document) => (
                          <span className={`badge document-state ${stateClass(document.state)}`}>
                            {displayState(document.state, t)}
                          </span>
                    ),
                  },
                  {
                    key: "type",
                    header: t("documents.type_column"),
                    render: (document) => document.documentType || "-",
                  },
                  {
                    key: "audience",
                    header: t("documents.audience"),
                    render: (document) => document.audience || "-",
                  },
                  {
                    key: "access",
                    header: t("documents.access"),
                    render: (document) =>
                      displayAccessRules(
                        groups,
                        organizationalUnits,
                        document,
                        t("documents.rule_company_wide_label"),
                        t("documents.rule_unit_unavailable"),
                        t("documents.access_rule_summary_separator"),
                      ),
                  },
                  {
                    key: "indexing",
                    header: t("documents.indexing"),
                    render: (document) => displayIndexing(document.indexingStatus, t),
                  },
                  {
                    key: "versions",
                    header: t("documents.versions_column"),
                    render: (document) =>
                      t("documents.versions_value", {
                        draft: document.draftVersionNumber ?? "-",
                        published: document.publishedVersionNumber ?? "-",
                      }),
                  },
                  {
                    key: "updated",
                    header: t("documents.updated_column"),
                    render: (document) => formatDate(document.updatedAt, i18n.language),
                  },
                  {
                    key: "actions",
                    header: t("documents.actions_column"),
                    render: (document) => (
                          <div className="row-actions">
                            <Button
                              className="icon-button"
                              type="button"
                              aria-label={t("documents.edit_document_for", { title: document.title })}
                              onClick={() => void openDocument(document.id)}
                            >
                              <Pencil size={16} />
                            </Button>
                            {canOpenViewer(document) ? (
                              <Button
                                className="icon-button"
                                type="button"
                                aria-label={t("documents.open_viewer_for", { title: document.title })}
                                onClick={() => void openViewer(document)}
                              >
                                <ExternalLink size={16} />
                              </Button>
                            ) : null}
                            {canRestore(document) ? (
                              <Button
                                className="icon-button"
                                type="button"
                                aria-label={t("documents.restore_document_for", { title: document.title })}
                                onClick={() => void runRestore(document)}
                              >
                                <RotateCcw size={16} />
                              </Button>
                            ) : null}
                            {canArchive(document, userRoles) ? (
                              <Button
                                className="icon-button"
                                type="button"
                                aria-label={t("documents.archive_document_for", { title: document.title })}
                                onClick={() => void runArchive(document)}
                              >
                                <Archive size={16} />
                              </Button>
                            ) : null}
                            {canRetryIndexing(document, userRoles) ? (
                              <Button
                                className="icon-button"
                                type="button"
                                aria-label={t("documents.retry_indexing_for", { title: document.title })}
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
            organizationalUnits={organizationalUnits}
            userRoles={userRoles}
            onClose={closeEditor}
            onGroupCreated={addGroup}
            onSaved={upsertDocument}
          />
        ) : (
          <div className="empty-panel">
            <div>
              <h2>{t("documents.editor")}</h2>
              <p>{t("documents.editor_empty")}</p>
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
  organizationalUnits,
  userRoles,
  onClose,
  onGroupCreated,
  onSaved,
}: {
  mode: "create" | "edit";
  documentDetail: DocumentDetail;
  groups: GroupSummary[];
  organizationalUnits: OrganizationalUnitSummary[];
  userRoles: string[];
  onClose: () => void;
  onGroupCreated: (group: GroupSummary) => void;
  onSaved: (document: DocumentDetail, message: string) => void;
}) {
  const { t } = useTranslation();
  const editableVersion =
    documentDetail.currentDraftVersion ?? documentDetail.currentPublishedVersion;
  const [title, setTitle] = useState(editableVersion?.title ?? documentDetail.title);
  const [documentType, setDocumentType] = useState(editableVersion?.documentType ?? "");
  const [audience, setAudience] = useState(editableVersion?.audience ?? "");
  const [contentHtml, setContentHtml] = useState(editableVersion?.contentHtml ?? "");
  const [rules, setRules] = useState<EditableAccessRule[]>(() =>
    initialAccessRules(documentDetail),
  );
  const [validationError, setValidationError] = useState<string | null>(null);
  const [importMessage, setImportMessage] = useState<string | null>(null);
  const [importError, setImportError] = useState<string | null>(null);
  const [importFileName, setImportFileName] = useState<string | null>(null);
  const [groupDialogRuleKey, setGroupDialogRuleKey] = useState<string | null>(
    null,
  );
  const [groupCreateMessage, setGroupCreateMessage] = useState<string | null>(null);
  const [isDirty, setIsDirty] = useState(mode === "create");
  const [isSaving, setIsSaving] = useState(false);
  const importInputId = useId();
  const importHelpId = useId();
  const permissions = documentActionPermissions(
    mode,
    documentDetail,
    userRoles,
  );
  const reviewValidationMessage = t("documents.review_validation_error");
  const hasReviewValidationError =
    validationError === reviewValidationMessage;
  const hasAccessRuleValidationError =
    hasReviewValidationError ||
    validationError === t("documents.access_rule_validation_error");
  const rootUnitId =
    organizationalUnits.find((unit) => unit.parentId === null)?.id ?? null;
  const ruleSignatures = rules.map(
    (rule) =>
      `${rule.organizationalUnitId ?? ""}|${[...rule.groupIds].sort().join(",")}`,
  );
  const duplicateRuleKeys = new Set(
    rules
      .filter(
        (rule, index) =>
          !isRuleEmpty(rule) &&
          ruleSignatures.indexOf(ruleSignatures[index]) !== index,
      )
      .map((rule) => rule.key),
  );
  const hasCompanyWideOnlyRule = rules.some(
    (rule) =>
      rule.organizationalUnitId !== null &&
      rule.organizationalUnitId === rootUnitId &&
      rule.groupIds.length === 0,
  );

  async function saveDraft() {
    setValidationError(null);
    setIsSaving(true);
    try {
      const request = {
        title,
        documentType,
        audience,
        contentHtml: normalizeEditorHtml(contentHtml),
        accessRules: rules.map(toAccessRuleInput),
      };
      const updated =
        mode === "create"
          ? await createDocumentDraft(request)
          : await saveDocumentDraft(documentDetail.id, request);
      setIsDirty(false);
      onSaved(
        updated,
        mode === "create" ? t("documents.created_message") : t("documents.draft_saved_message"),
      );
    } catch (error) {
      if (error instanceof ApiError && error.code === "VALIDATION_FAILED") {
        setValidationError(t("documents.access_rule_validation_error"));
      } else {
        const reference = error instanceof ApiError ? error.requestId : "unknown";
        setValidationError(t("documents.save_error", { reference }));
      }
    } finally {
      setIsSaving(false);
    }
  }

  async function sendToReview() {
    const hasValidAccessRules =
      rules.length > 0 && rules.every((rule) => !isRuleEmpty(rule));
    if (
      title.trim().length === 0 ||
      documentType.trim().length === 0 ||
      audience.trim().length === 0 ||
      plainText(contentHtml).length === 0 ||
      !hasValidAccessRules
    ) {
      setValidationError(
        reviewValidationMessage,
      );
      return;
    }

    if (mode === "create") {
      setValidationError(t("documents.save_before_review_error"));
      return;
    }

    const updated = await sendDocumentToReview(documentDetail.id);
    onSaved(updated, t("documents.sent_to_review_message"));
  }

  async function publishDocument() {
    setValidationError(null);
    setIsSaving(true);
    try {
      const updated = await requestPublish(documentDetail.id);
      onSaved(updated, t("documents.published_message"));
    } catch (error) {
      const reference = error instanceof ApiError ? error.requestId : "unknown";
      setValidationError(t("documents.publish_error", { reference }));
    } finally {
      setIsSaving(false);
    }
  }

  async function runImport(file: File) {
    setImportMessage(null);
    setImportError(null);
    setImportFileName(file.name);
    if (file.size > MaxImportFileSizeBytes) {
      setImportError(t("documents.import_too_large"));
      return;
    }

    try {
      const result = await importDocumentText(file);
      const extractedHtml = result.contentHtml?.trim();
      setContentHtml(
        extractedHtml && extractedHtml.length > 0
          ? extractedHtml
          : plainTextToParagraphHtml(result.text),
      );
      setIsDirty(true);
      setImportMessage(t("documents.import_success", { filename: result.metadata.originalFilename }));
    } catch (error) {
      const reference = error instanceof ApiError ? error.requestId : "unknown";
      setImportError(t("documents.import_error", { reference }));
    }
  }

  function addRule() {
    setRules((current) => [
      ...current,
      { key: newRuleKey(), organizationalUnitId: null, groupIds: [] },
    ]);
    setIsDirty(true);
  }

  function removeRule(key: string) {
    setRules((current) => current.filter((rule) => rule.key !== key));
    setIsDirty(true);
  }

  function setRuleUnit(key: string, unitId: string) {
    setRules((current) =>
      current.map((rule) =>
        rule.key === key
          ? { ...rule, organizationalUnitId: unitId === "" ? null : unitId }
          : rule,
      ),
    );
    setIsDirty(true);
  }

  function toggleRuleGroup(key: string, groupId: string) {
    setRules((current) =>
      current.map((rule) =>
        rule.key === key
          ? {
              ...rule,
              groupIds: rule.groupIds.includes(groupId)
                ? rule.groupIds.filter((id) => id !== groupId)
                : [...rule.groupIds, groupId],
            }
          : rule,
      ),
    );
    setIsDirty(true);
  }

  function addCreatedAccessGroup(group: GroupSummary) {
    onGroupCreated(group);
    const targetKey = groupDialogRuleKey;
    setRules((current) =>
      current.map((rule) =>
        rule.key === targetKey && !rule.groupIds.includes(group.id)
          ? { ...rule, groupIds: [...rule.groupIds, group.id] }
          : rule,
      ),
    );
    setGroupDialogRuleKey(null);
    setIsDirty(true);
    setGroupCreateMessage(t("documents.group_create_success", { name: group.name }));
  }

  return (
    <section
      className="document-editor-panel"
      aria-labelledby="document-editor-title"
    >
      <header className="document-editor-header">
        <div>
          <p className="eyebrow">{t("documents.editor_eyebrow")}</p>
          <h2 id="document-editor-title">
            {mode === "create" ? t("documents.editor_create_title") : t("documents.editor_edit_title")}
          </h2>
        </div>
        <Button className="text-button" type="button" onClick={onClose}>
          {t("documents.back_to_list")}
        </Button>
      </header>

      <form className="document-form">
        <div className="dialog-grid">
          <label className="field">
            <span>{t("documents.title_field")}</span>
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
            <span>{t("documents.type_column")}</span>
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
            <span>{t("documents.audience")}</span>
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
            <span id={`${importInputId}-label`}>{t("documents.import_file")}</span>
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
                {t("documents.select_file")}
              </label>
              <span className="file-upload-filename">
                {importFileName ?? t("documents.no_file_selected")}
              </span>
            </div>
          </div>
        </div>

        <fieldset className="checkbox-list access-rules">
          <legend>{t("documents.access_rules")}</legend>
          <p className="muted-copy">{t("documents.access_rules_help")}</p>
          <div className="checkbox-list-actions">
            <Button className="text-button" type="button" onClick={addRule}>
              <Plus size={16} />
              {t("documents.add_access_rule")}
            </Button>
          </div>
          {groupCreateMessage ? (
            <p className="status-message success">{groupCreateMessage}</p>
          ) : null}
          {rules.length === 0 ? (
            <p className="muted-copy">{t("documents.access_rules_required")}</p>
          ) : null}
          {hasCompanyWideOnlyRule && rules.length > 1 ? (
            <p className="status-message warning">
              {t("documents.access_rules_company_redundant")}
            </p>
          ) : null}
          {rules.map((rule, index) => (
            <Fragment key={rule.key}>
              {index > 0 ? (
                <p aria-hidden="true" className="access-rule-separator">
                  {t("documents.access_rule_or")}
                </p>
              ) : null}
              <AccessRuleCard
                groups={groups}
                index={index}
                isDuplicate={duplicateRuleKeys.has(rule.key)}
                organizationalUnits={organizationalUnits}
                rule={rule}
                showValidationError={hasAccessRuleValidationError}
                onCreateGroup={() => setGroupDialogRuleKey(rule.key)}
                onGroupToggle={(groupId) => toggleRuleGroup(rule.key, groupId)}
                onRemove={() => removeRule(rule.key)}
                onUnitChange={(unitId) => setRuleUnit(rule.key, unitId)}
              />
            </Fragment>
          ))}
        </fieldset>

        <RichTextEditor
          documentId={mode === "edit" ? documentDetail.id : undefined}
          value={contentHtml}
          onChange={(value) => {
            setContentHtml(value);
            setIsDirty(true);
          }}
          onUploadImage={
            mode === "edit"
              ? (file, altText) => uploadDocumentImage(documentDetail.id, file, altText)
              : undefined
          }
        />

        {isDirty ? <p className="status-message">{t("documents.unsaved_changes")}</p> : null}
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
              {t("documents.send_to_review")}
            </Button>
          ) : null}
          {permissions.canPublish ? (
            <Button
              className="primary-button"
              type="button"
              disabled={isSaving}
              onClick={() => void publishDocument()}
            >
              {isSaving ? t("documents.publishing") : t("documents.publish")}
            </Button>
          ) : null}
          {permissions.canSaveDraft ? (
            <Button
              className="primary-button"
              type="button"
              disabled={isSaving}
              onClick={() => void saveDraft()}
            >
              {isSaving ? t("documents.saving") : t("documents.save_draft")}
            </Button>
          ) : null}
        </div>
      </form>

      {groupDialogRuleKey !== null ? (
        <GroupCreateDialog
          onClose={() => setGroupDialogRuleKey(null)}
          onSaved={addCreatedAccessGroup}
        />
      ) : null}
    </section>
  );
}

// Radix Select forbids an empty-string item value, so the "no unit" choice
// (which maps to a null organizational unit) uses a sentinel value.
const NO_UNIT_VALUE = "__none__";

function AccessRuleCard({
  rule,
  index,
  groups,
  organizationalUnits,
  isDuplicate,
  showValidationError,
  onCreateGroup,
  onGroupToggle,
  onRemove,
  onUnitChange,
}: {
  rule: EditableAccessRule;
  index: number;
  groups: GroupSummary[];
  organizationalUnits: OrganizationalUnitSummary[];
  isDuplicate: boolean;
  showValidationError: boolean;
  onCreateGroup: () => void;
  onGroupToggle: (groupId: string) => void;
  onRemove: () => void;
  onUnitChange: (unitId: string) => void;
}) {
  const { t } = useTranslation();
  const [groupQuery, setGroupQuery] = useState("");

  const normalizedQuery = groupQuery.trim().toLowerCase();
  const visibleGroups =
    normalizedQuery.length === 0
      ? groups
      : groups.filter((group) =>
          group.name.toLowerCase().includes(normalizedQuery),
        );
  const selectedUnit = organizationalUnits.find(
    (unit) => unit.id === rule.organizationalUnitId,
  );
  const isUnresolvedUnit =
    rule.organizationalUnitId !== null && selectedUnit === undefined;
  const descendantCount =
    selectedUnit && selectedUnit.parentId !== null
      ? countDescendants(organizationalUnits, selectedUnit.id)
      : 0;
  const preview = rulePreview(rule, selectedUnit, groups, t);

  const unitOptions: SelectOption[] = [
    { value: NO_UNIT_VALUE, label: t("documents.rule_no_unit") },
    // Preserve a unit id missing from the caller's catalog (e.g. a unit
    // deactivated and hidden from non-admins) instead of dropping it on save.
    ...(isUnresolvedUnit && rule.organizationalUnitId
      ? [
          {
            value: rule.organizationalUnitId,
            label: t("documents.rule_unit_unavailable"),
          },
        ]
      : []),
    ...organizationalUnits.map((unit) => ({
      value: unit.id,
      label:
        unit.parentId === null
          ? `${unit.name} (${t("documents.rule_company_wide")})`
          : unit.name,
      depth: unit.depth,
      badge: <UnitLevelBadge level={unit.depth} />,
    })),
  ];

  return (
    <div className="access-rule">
      <div className="access-rule-header">
        <span className="access-rule-title">
          {t("documents.access_rule_label", { number: index + 1 })}
        </span>
        <Button
          aria-label={t("documents.remove_access_rule_for", {
            number: index + 1,
          })}
          className="text-button"
          type="button"
          onClick={onRemove}
        >
          {t("documents.remove_access_rule")}
        </Button>
      </div>
      <label className="field">
        <span>{t("documents.rule_organizational_unit")}</span>
        <Select
          ariaLabel={t("documents.rule_organizational_unit")}
          placeholder={t("documents.rule_no_unit")}
          value={rule.organizationalUnitId ?? NO_UNIT_VALUE}
          onValueChange={(next) =>
            onUnitChange(next === NO_UNIT_VALUE ? "" : next)
          }
          options={unitOptions}
        />
      </label>
      {descendantCount > 0 ? (
        <p className="muted-copy">
          {t("documents.rule_unit_branch_hint", { count: descendantCount })}
        </p>
      ) : null}
      <fieldset className="checkbox-list rule-groups">
        <legend>{t("documents.rule_groups")}</legend>
        <div className="rule-groups-toolbar">
          {groups.length > 0 ? (
            <>
              <Input
                aria-label={t("documents.rule_groups_filter")}
                placeholder={t("documents.rule_groups_filter")}
                type="search"
                value={groupQuery}
                onChange={(event) => setGroupQuery(event.target.value)}
              />
              <span className="muted-copy">
                {t("documents.rule_groups_selected", {
                  count: rule.groupIds.length,
                })}
              </span>
            </>
          ) : null}
          <Button className="text-button" type="button" onClick={onCreateGroup}>
            <FolderPlus size={16} />
            {t("documents.group_new")}
          </Button>
        </div>
        {groups.length === 0 ? (
          <p className="muted-copy">{t("documents.no_groups")}</p>
        ) : visibleGroups.length === 0 ? (
          <p className="muted-copy">{t("documents.rule_groups_no_matches")}</p>
        ) : (
          <div className="checkbox-grid">
            {visibleGroups.map((group) => (
              <Checkbox
                className="checkbox-field"
                key={group.id}
                label={group.name}
                checked={rule.groupIds.includes(group.id)}
                onCheckedChange={() => onGroupToggle(group.id)}
              />
            ))}
          </div>
        )}
      </fieldset>
      {isDuplicate ? (
        <p className="status-message warning">
          {t("documents.access_rule_duplicate_warning")}
        </p>
      ) : null}
      {preview ? <p className="access-rule-preview">{preview}</p> : null}
      {isRuleEmpty(rule) ? (
        showValidationError ? (
          <p className="status-message error">
            {t("documents.access_rule_empty_invalid")}
          </p>
        ) : (
          <p className="muted-copy">{t("documents.access_rule_empty_hint")}</p>
        )
      ) : null}
    </div>
  );
}

function GroupCreateDialog({
  onClose,
  onSaved,
}: {
  onClose: () => void;
  onSaved: (group: GroupSummary) => void;
}) {
  const { t } = useTranslation();
  const [name, setName] = useState("");
  const [validationError, setValidationError] = useState<string | null>(null);
  const [apiError, setApiError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setValidationError(null);
    setApiError(null);

    const trimmedName = name.trim();
    if (trimmedName.length === 0) {
      setValidationError(t("documents.group_create_name_required"));
      return;
    }

    setIsSaving(true);
    try {
      onSaved(await createGroup({ name: trimmedName }));
    } catch (error) {
      const reference = error instanceof ApiError ? error.requestId : "unknown";
      setApiError(t("documents.group_create_error", { reference }));
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <Dialog
      open
      title={t("documents.group_create")}
      description={t("documents.group_create_description")}
      onOpenChange={(open) => {
        if (!open) {
          onClose();
        }
      }}
    >
      <form className="dialog-form" noValidate onSubmit={handleSubmit}>
        <label className="field">
          <span>{t("documents.group_create_name")}</span>
          <Input
            type="text"
            value={name}
            onChange={(event) => setName(event.target.value)}
            disabled={isSaving}
          />
        </label>

        {validationError ? (
          <p className="status-message error" role="alert">
            {validationError}
          </p>
        ) : null}
        {apiError ? (
          <p className="status-message error" role="alert">
            {apiError}
          </p>
        ) : null}

        <div className="dialog-actions">
          <Button className="text-button" type="button" onClick={onClose}>
            {t("documents.cancel")}
          </Button>
          <Button className="primary-button" type="submit" disabled={isSaving}>
            {isSaving ? t("documents.saving") : t("documents.group_save")}
          </Button>
        </div>
      </form>
    </Dialog>
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
    accessRules: document.accessRules,
    draftVersionNumber: document.currentDraftVersion?.versionNumber ?? null,
    publishedVersionNumber:
      document.currentPublishedVersion?.versionNumber ?? null,
    indexingStatus: version?.indexingStatus ?? "None",
    updatedAt: document.updatedAt,
  };
}

function initialAccessRules(detail: DocumentDetail): EditableAccessRule[] {
  const existing = (detail.accessRules ?? []).map((rule) => ({
    key: newRuleKey(),
    organizationalUnitId: rule.organizationalUnitId,
    groupIds: [...rule.groupIds],
  }));
  if (existing.length > 0) {
    return existing;
  }

  // Fall back to a single group-only rule for legacy documents that still expose
  // only `allowedGroupIds`, otherwise start with one empty rule to fill in.
  if (detail.allowedGroupIds.length > 0) {
    return [
      {
        key: newRuleKey(),
        organizationalUnitId: null,
        groupIds: detail.allowedGroupIds.map((groupId) => groupId.toString()),
      },
    ];
  }

  return [{ key: newRuleKey(), organizationalUnitId: null, groupIds: [] }];
}

function newRuleKey(): string {
  if ("randomUUID" in crypto) {
    return crypto.randomUUID();
  }

  return `rule-${Date.now()}-${Math.random().toString(36).slice(2)}`;
}

function toAccessRuleInput(rule: EditableAccessRule): DocumentAccessRuleInput {
  return {
    organizationalUnitId: rule.organizationalUnitId,
    groupIds: rule.groupIds,
  };
}

function countDescendants(
  units: OrganizationalUnitSummary[],
  unitId: string,
): number {
  let count = 0;
  let frontier = new Set([unitId]);
  while (frontier.size > 0) {
    const children = units.filter(
      (unit) => unit.parentId !== null && frontier.has(unit.parentId),
    );
    count += children.length;
    frontier = new Set(children.map((unit) => unit.id));
  }
  return count;
}

// "A, B o C" — the disjunction mirrors the backend rule semantics where the
// group dimension matches when the user belongs to ANY listed group.
function formatDisjunction(values: string[], orWord: string): string {
  if (values.length <= 1) {
    return values.join("");
  }

  return `${values.slice(0, -1).join(", ")} ${orWord} ${values[values.length - 1]}`;
}

function rulePreview(
  rule: EditableAccessRule,
  selectedUnit: OrganizationalUnitSummary | undefined,
  groups: GroupSummary[],
  t: (key: string, options?: Record<string, unknown>) => string,
): string | null {
  if (isRuleEmpty(rule)) {
    return null;
  }

  const groupsText = formatDisjunction(
    rule.groupIds.map((groupId) => groupName(groups, groupId)),
    t("documents.rule_preview_or_word"),
  );

  if (rule.organizationalUnitId === null) {
    return t("documents.rule_preview_groups", { groups: groupsText });
  }

  const unitName = selectedUnit?.name ?? t("documents.rule_unit_unavailable");
  if (selectedUnit?.parentId === null) {
    return rule.groupIds.length === 0
      ? t("documents.rule_preview_company")
      : t("documents.rule_preview_company_groups", { groups: groupsText });
  }

  return rule.groupIds.length === 0
    ? t("documents.rule_preview_unit", { unit: unitName })
    : t("documents.rule_preview_unit_groups", {
        unit: unitName,
        groups: groupsText,
      });
}

function isRuleEmpty(rule: DocumentAccessRuleInput): boolean {
  return rule.organizationalUnitId === null && rule.groupIds.length === 0;
}

function organizationalUnitOptionLabel(
  unit: OrganizationalUnitSummary,
  companyWideLabel: string,
): string {
  if (unit.parentId === null) {
    return `${unit.name} (${companyWideLabel})`;
  }

  // Non-breaking spaces survive the browser's whitespace collapsing in <option>.
  return `${"\u00A0\u00A0\u00A0".repeat(unit.depth)}${unit.name}`;
}

// Flattens the unit list into depth-first tree order (siblings alphabetical) so
// option indentation reads as a hierarchy. Units whose parent is not in the
// list (e.g. under an inactive ancestor) are appended at the end unindented.
function sortUnitsInTreeOrder(
  units: OrganizationalUnitSummary[],
): OrganizationalUnitSummary[] {
  const childrenByParent = new Map<string | null, OrganizationalUnitSummary[]>();
  const knownIds = new Set(units.map((unit) => unit.id));
  const orphans: OrganizationalUnitSummary[] = [];
  for (const unit of units) {
    if (unit.parentId !== null && !knownIds.has(unit.parentId)) {
      orphans.push(unit);
      continue;
    }
    const siblings = childrenByParent.get(unit.parentId) ?? [];
    siblings.push(unit);
    childrenByParent.set(unit.parentId, siblings);
  }
  for (const siblings of childrenByParent.values()) {
    siblings.sort((left, right) => left.name.localeCompare(right.name));
  }

  const ordered: OrganizationalUnitSummary[] = [];
  const visit = (parentId: string | null) => {
    for (const unit of childrenByParent.get(parentId) ?? []) {
      ordered.push(unit);
      visit(unit.id);
    }
  };
  visit(null);
  return [...ordered, ...orphans];
}

function documentActionPermissions(
  mode: "create" | "edit",
  document: DocumentDetail,
  userRoles: string[],
) {
  const isAdmin = hasRole(userRoles, "Admin");
  const canPublishRole = isAdmin || hasRole(userRoles, "DocumentPublisher");
  const canEditRole = canPublishRole || hasRole(userRoles, "DocumentEditor");
  const draftState = document.currentDraftVersion?.state;

  return {
    canSaveDraft:
      canEditRole &&
      (mode === "create" ||
        document.state === "Draft" ||
        document.state === "Published"),
    canSendToReview:
      canEditRole &&
      mode === "edit" &&
      document.state === "Draft" &&
      draftState === "Draft",
    canPublish:
      canPublishRole &&
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
    hasRole(userRoles, "DocumentPublisher") &&
    (document.state === "Draft" || document.state === "In Review") &&
    document.publishedVersionNumber === null
  );
}

function canRetryIndexing(document: DocumentSummary, userRoles: string[]) {
  return (
    (hasRole(userRoles, "Admin") || hasRole(userRoles, "DocumentPublisher")) &&
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

function displayState(state: string, t: (key: string) => string) {
  const labels: Record<string, string> = {
    Draft: t("documents.state_draft"),
    "In Review": t("documents.state_in_review"),
    Published: t("documents.state_published"),
    Archived: t("documents.state_archived"),
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

function displayIndexing(status: string, t: (key: string) => string) {
  const labels: Record<string, string> = {
    None: "-",
    Pending: t("documents.indexing_pending"),
    Succeeded: t("documents.indexing_succeeded"),
    Failed: t("documents.indexing_failed"),
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

function displayAccessRules(
  groups: GroupSummary[],
  units: OrganizationalUnitSummary[],
  document: DocumentSummary,
  companyWideLabel: string,
  unitUnavailableLabel: string,
  separator: string,
): string {
  const rules = document.accessRules ?? [];
  if (rules.length === 0) {
    return displayGroups(groups, document.allowedGroupIds);
  }

  return rules
    .map((rule) => {
      const unitLabel =
        rule.organizationalUnitId === null
          ? null
          : unitDisplayName(
              units,
              rule.organizationalUnitId,
              companyWideLabel,
              unitUnavailableLabel,
            );
      const groupLabels = rule.groupIds.map((groupId) =>
        groupName(groups, groupId),
      );
      return [unitLabel, ...groupLabels].filter(Boolean).join(" + ");
    })
    .filter((text) => text.length > 0)
    .join(separator);
}

function unitDisplayName(
  units: OrganizationalUnitSummary[],
  unitId: string,
  companyWideLabel: string,
  unitUnavailableLabel: string,
): string {
  const unit = units.find((item) => item.id === unitId);
  if (!unit) {
    // The non-admin catalog omits inactive units, so the id may not resolve.
    return unitUnavailableLabel;
  }

  return unit.parentId === null ? companyWideLabel : unit.name;
}

function uniqueValues(values: string[]) {
  return [...new Set(values.map((value) => value.trim()).filter(Boolean))].sort(
    (a, b) => a.localeCompare(b),
  );
}

function formatDate(value: string, locale: string) {
  return new Intl.DateTimeFormat(locale, {
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
