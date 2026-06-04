# Document Creation And Editor Fixes Design

## Status

Approved for design by the user on 2026-06-03. The next gate is written spec review before implementation planning.

## Context

The current document creation workflow works end to end, but several usability and contract gaps are visible in the management and document viewer surfaces:

- Document editors must leave the document flow to create access groups.
- TipTap creates list HTML, but bullets and numbers are not visible because editor/viewer CSS does not restore list markers after the global reset.
- The toolbar exposes only a small part of the editor capability, despite the approved UI context requiring h1-h3, common marks, lists, code, links, images, and tables.
- Links from `manage.*` or `chat.*` to `docs.*` do not feel logged in because the product intentionally uses host-only `__Host-session` cookies.
- Document tags exist in schema/documentation history but have no API or UI behavior.
- PDF/DOCX import currently returns plain text only, so the editor loses useful structure.

## Goals

1. Keep document creation in one ergonomic workflow by adding contextual group creation inside the document editor.
2. Fix TipTap list rendering in both the editor and document viewer.
3. Expand the toolbar to the document controls users naturally expect without adding unnecessary UI dependencies.
4. Preserve host-only cookie security while making `docs.*` links behave like a shared authenticated product surface.
5. Stop documenting tags as an active MVP feature.
6. Improve import prefill quality: structured DOCX-to-HTML, conservative PDF formatting, and clear limits.

## Non-Goals

- Do not add parent-domain cookies.
- Do not put the main session token, long-lived viewer tokens, or access tokens in URLs.
- Do not implement document tags in this MVP slice.
- Do not promise pixel-perfect or high-fidelity PDF-to-HTML conversion.
- Do not retain uploaded PDF/DOCX originals.
- Do not add OCR for scanned PDFs.
- Do not make imported HTML publishable without user review and normal save/review validation.

## Agent-Owned Work

- Update context and documentation in English.
- Add focused tests before implementation changes.
- Implement frontend/backend code, DTOs, styling, and API behavior.
- Run focused typecheck/build/test verification for touched stacks.
- Keep unrelated dirty worktree changes untouched.

## User-Owned Work

- Review this spec before implementation planning.
- Approve any new import dependency after implementation validates NuGet version, license, and security implications.
- Run browser/Compose acceptance when cross-host SSO handoff needs realistic cookie behavior.

## Design

### 1. Quick Group Creation In Document Editor

The document editor's access group section should include a compact "Create group" action for `Admin` and `DocumentManager`.

Behavior:

- The action opens a small inline panel or dialog with a required group name.
- On submit, the frontend calls the existing `POST /api/groups` endpoint through the `users` API client.
- On success, the new group is appended to the local `groups` state, selected in `allowedGroupIds`, and the document becomes dirty.
- On validation or API failure, the editor shows a localized inline error with request ID when available.
- The global Users/Groups workspace remains the canonical place for group administration. The editor action is quick creation only, not full group management.

This uses existing backend authorization and does not introduce a document-specific group endpoint.

### 2. TipTap Rendering And Toolbar

Root cause for missing markers:

- Current TipTap commands generate `<ul>` and `<ol>`.
- CSS for `.tiptap-editor-surface` and `.document-content` sets margins but does not set `list-style` or padding.
- The project's reset/Tailwind styles remove default list markers.

Fix:

- Add scoped CSS for editor and viewer content:
  - `ul { list-style: disc; padding-left: ... }`
  - `ol { list-style: decimal; padding-left: ... }`
  - nested lists use stable indentation.
  - `li` spacing is defined without causing layout jumps.
- Add viewer content CSS for tables, blockquotes, code blocks, horizontal rules, and images so saved HTML renders consistently outside the editor.

Toolbar controls:

- Paragraph.
- H1, H2, H3.
- Bold, italic, underline, strike.
- Bulleted list, ordered list.
- Blockquote.
- Inline code and code block.
- Link.
- Insert table.
- Horizontal rule.
- Undo and redo.
- Insert image.

The toolbar should remain compact, use `lucide-react` icons, expose accessible names, show active state where meaningful, and avoid in-app explanatory help text.

Tiptap references checked on 2026-06-03:

- `StarterKit` includes headings, bullet/ordered lists, blockquote, code block, horizontal rule, strike, undo/redo, and list keymap.
- Heading commands support `toggleHeading({ level })`.
- List commands support toggling bullet and ordered lists.

### 3. Secure Cross-Subdomain Viewer SSO Handoff

The current `__Host-session` cookie is host-only by design. A session on `manage.*` cannot authenticate `docs.*` automatically. The fix should preserve host-only cookies by adding a handoff flow.

Proposed contract:

- Source app calls `POST /api/viewer/links` with `{ documentId, purpose }`, as today.
- `.NET` validates the source session and document link purpose.
- `.NET` persists a short-lived one-time handoff code tied to:
  - source user id,
  - document id,
  - purpose,
  - allowed state scope,
  - expiration timestamp,
  - consumed/used state,
  - request id or audit metadata.
- The response URL points to `docs.*`, for example `/open?documentId=...&handoff=...`.
- `docs-web` bootstraps by detecting `handoff`.
- `docs-web` calls same-origin `.NET` endpoint such as `POST /api/viewer/session-handoff` with the handoff code.
- `.NET` validates the code, checks expiration/replay state, resolves the user, issues a `__Host-session` cookie for `docs.*`, and returns a session summary.
- `docs-web` removes `handoff` from the URL with `history.replaceState` before loading the document.
- `docs-web` calls `/api/viewer/document?documentId=...`; `.NET` revalidates session, role, groups, document state, and permissions before returning content.

Security rules:

- The handoff code is not a document access token and is not sufficient by itself to load content.
- The handoff code must be short-lived.
- The handoff code must be one-time-use and persisted in the database with consumed/used state.
- The main browser session value must never be exposed to JavaScript or URLs.
- Parent-domain cookies remain forbidden.
- Failed handoff states show safe localized errors and may offer normal docs login as recovery.

### 4. Tags Deferred

Document tags are deferred for the MVP.

Required documentation changes:

- Remove or reword active product claims that tags are available to users.
- Keep schema history factual: `app.document_tags` exists but is dormant.
- Do not add tag UI, DTOs, lifecycle service fields, filters, or RAG behavior in this slice.

Rationale:

- Groups are authorization boundaries.
- Document type and audience cover current visible classification.
- Adding tag UI/API now increases scope without improving the current document creation bugs.

### 5. Assisted Import HTML

The import endpoint should evolve from plain text to safe draft HTML plus fallback text.

Response shape:

```json
{
  "text": "fallback plain text",
  "contentHtml": "<h1>...</h1><p>...</p>",
  "metadata": {
    "originalFilename": "manual.docx",
    "mimeType": "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
    "sizeBytes": 2048,
    "sha256Hash": "...",
    "extractionStatus": "Extracted"
  }
}
```

DOCX behavior:

- Use Mammoth for DOCX-to-HTML conversion, pending final NuGet version/license validation during implementation.
- Preserve common semantic structure where available: headings, paragraphs, lists, tables, emphasis, links, and line breaks.
- Sanitize converted HTML before returning it to the browser.
- Do not import embedded DOCX images in this slice. Do not return inline base64 images or external file references as canonical HTML.

PDF behavior:

- Continue using PdfPig, but replace direct `page.Text` output with conservative reading-order/layout extraction.
- Prefer text extractors/layout analysis recommended by PdfPig, such as content-order text extraction, nearest-neighbour word extraction, and simple block grouping where reliable.
- Generate paragraphs and line breaks that are easier to edit.
- Avoid pretending to detect headings/tables when evidence is weak.
- Preserve `IMPORT_TEXT_NOT_EXTRACTABLE` for scanned/no-text PDFs.

Frontend behavior:

- If `contentHtml` is present and non-empty, insert it into TipTap.
- Otherwise fall back to current plain-text-to-paragraph conversion.
- Imported content remains dirty unsaved editor state.
- Import error behavior remains unchanged: keep existing editor content and show a safe error.

## Error Handling

- Group quick-create empty name: field-level validation.
- Group quick-create API failure: localized error with request ID.
- Handoff missing/expired/invalid/used: safe error envelope and localized docs-web state.
- Import unsupported file type: `VALIDATION_FAILED`.
- Import too large: `IMPORT_FILE_TOO_LARGE`.
- Import no extractable text: `IMPORT_TEXT_NOT_EXTRACTABLE`.
- Import converter failure: safe import error, technical details in logs only.

New stable error codes may be added for handoff if current codes are insufficient, for example `VIEWER_HANDOFF_EXPIRED`, `VIEWER_HANDOFF_INVALID`, and `VIEWER_HANDOFF_USED`.

## Testing Strategy

Frontend management tests:

- Quick group creation creates a group and selects it in the document editor.
- List HTML renders with visible list-marker CSS in editor/viewer styles.
- Toolbar exposes the approved controls and calls TipTap commands.
- Import uses `contentHtml` when returned and falls back to `text`.

Docs frontend tests:

- `handoff` in URL triggers session handoff call.
- Successful handoff removes `handoff` from the URL and loads the document.
- Expired/invalid handoff shows a safe error.
- Normal docs login still works without handoff.

.NET tests:

- Viewer link creation returns a handoff URL for authenticated source sessions.
- Handoff validation issues a host-scoped session response without exposing raw session values.
- Expired/invalid/replayed handoff codes fail safely.
- DOCX import returns sanitized `contentHtml`.
- PDF import avoids direct `page.Text` behavior and returns editable paragraphs.
- Import errors preserve stable codes.

Verification commands will be finalized in the implementation plan, but should include focused `.NET` tests, `pnpm --dir apps/manage-web test -- --run`, `pnpm --dir apps/docs-web test -- --run`, typecheck/build for touched frontends, and `git diff --check`.

## Implementation Plan Inputs

1. Which exact Mammoth package version should be approved after checking NuGet metadata and license?

These are implementation-plan questions, not product-scope blockers.
