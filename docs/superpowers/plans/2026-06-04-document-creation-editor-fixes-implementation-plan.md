# Document Creation And Editor Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement quick group creation in the document editor, richer TipTap controls/list rendering, secure DB-backed docs handoff, deferred tag documentation cleanup, and safer HTML import prefill.

**Architecture:** Keep document lifecycle and viewer session handoff in the .NET app boundary, with EF-owned `app` schema changes for one-time handoff codes. Keep React changes inside the existing manage/docs app API clients and components. Imports remain assisted drafts: backend returns sanitized `contentHtml` plus fallback `text`; frontend inserts HTML when present.

**Tech Stack:** .NET 8, EF Core 8, PostgreSQL, Mammoth 1.11.0, HtmlSanitizer, PdfPig 0.1.14, React 18, TipTap 3.23.5, Vitest, xUnit.

---

## Agent-Owned Steps

- Implement code, tests, migrations, context updates, and focused verification from the shared workspace.
- Add Mammoth through a pinned `PackageReference` in `services/dotnet-api/src/AdvancedRag.Infrastructure/AdvancedRag.Infrastructure.csproj`.
- Avoid touching unrelated dirty worktree changes except where context files already own this feature decision.

## User-Owned Steps

- After implementation, run browser/Compose acceptance on the local or demo stack:

```powershell
docker compose --env-file infra/compose/.env.example -f infra/compose/compose.yaml up -d --build
```

Expected: Compose starts without unhealthy services. Report any service that stays unhealthy and the last 50 lines of its logs.

- Then verify in the browser: log in on `manage.*`, create a document, quick-create a group from the editor, import a DOCX, save the draft, send it to review when the current role can do so, and open the docs viewer link.

Expected: the new group is selected, DOCX formatting is visible in the editor, and `docs.*` opens without a separate manual login.

---

### Task 1: Backend Import Contract And Mammoth Conversion

**Files:**
- Modify: `services/dotnet-api/src/AdvancedRag.App/Documents/DocumentImportTypes.cs`
- Modify: `services/dotnet-api/src/AdvancedRag.Api/Models/Documents/DocumentResponses.cs`
- Modify: `services/dotnet-api/src/AdvancedRag.Infrastructure/Documents/DocumentImportExtractionService.cs`
- Modify: `services/dotnet-api/src/AdvancedRag.Infrastructure/AdvancedRag.Infrastructure.csproj`
- Test: `services/dotnet-api/tests/AdvancedRag.Infrastructure.Tests/DocumentImportExtractionTests.cs`

- [ ] **Step 1: Write failing import tests**

Add assertions that DOCX extraction returns sanitized semantic `ContentHtml` and omits embedded image output. Add a PDF assertion that the result includes editable paragraph HTML.

- [ ] **Step 2: Run import tests to verify RED**

Run:

```powershell
dotnet test services\dotnet-api\tests\AdvancedRag.Infrastructure.Tests\AdvancedRag.Infrastructure.Tests.csproj --no-restore --filter "FullyQualifiedName~DocumentImportExtractionTests"
```

Expected: FAIL because `ImportExtractionResult` has no `ContentHtml` and DOCX conversion is still plain text.

- [ ] **Step 3: Implement import contract**

Change `ImportExtractionResult` to include `string? ContentHtml`. Keep `Text` for fallback compatibility. Change `ImportExtractionResponse` to return `contentHtml`.

- [ ] **Step 4: Add Mammoth and conversion**

Add `Mammoth` version `1.11.0`. Convert DOCX through `Mammoth.DocumentConverter`, sanitize converted HTML with `Ganss.Xss.HtmlSanitizer`, remove/skip image output, and use raw text extraction for fallback text. Keep `IMPORT_TEXT_NOT_EXTRACTABLE` when fallback text is empty.

- [ ] **Step 5: Improve PDF HTML prefill**

Continue using PdfPig, but build fallback text and `contentHtml` from page words/lines in reading order rather than direct `page.Text` concatenation. Generate paragraph HTML with escaped text and `<br>` for line breaks where useful.

- [ ] **Step 6: Run import tests to verify GREEN**

Run the same focused import test command. Expected: PASS.

---

### Task 2: Backend Viewer Handoff DB Contract

**Files:**
- Modify: `services/dotnet-api/src/AdvancedRag.App/Viewer/ViewerAccessTypes.cs`
- Modify: `services/dotnet-api/src/AdvancedRag.App/Viewer/ViewerAccessService.cs`
- Modify: `services/dotnet-api/src/AdvancedRag.Api/Controllers/ViewerController.cs`
- Modify: `services/dotnet-api/src/AdvancedRag.Api/Models/Viewer/ViewerRequests.cs`
- Modify: `services/dotnet-api/src/AdvancedRag.Api/Models/Viewer/ViewerResponses.cs`
- Modify: `services/dotnet-api/src/AdvancedRag.Infrastructure/Persistence/Entities.cs`
- Modify: `services/dotnet-api/src/AdvancedRag.Infrastructure/Persistence/AppDbContext.cs`
- Create: `services/dotnet-api/src/AdvancedRag.Infrastructure/Migrations/20260604090000_AddViewerSessionHandoffCodes.cs`
- Create: `services/dotnet-api/src/AdvancedRag.Infrastructure/Migrations/20260604090000_AddViewerSessionHandoffCodes.Designer.cs`
- Modify: `services/dotnet-api/src/AdvancedRag.Infrastructure/Migrations/AppDbContextModelSnapshot.cs`
- Modify: `services/dotnet-api/src/AdvancedRag.Infrastructure/Viewer/EfViewerAccessRepository.cs`
- Modify: `services/dotnet-api/src/AdvancedRag.Api/Program.cs`
- Test: `services/dotnet-api/tests/AdvancedRag.App.Tests/ViewerAccessServiceTests.cs`
- Test: `services/dotnet-api/tests/AdvancedRag.Api.Tests/ViewerEndpointTests.cs`
- Test: `services/dotnet-api/tests/AdvancedRag.Infrastructure.Tests/AppDbContextMappingTests.cs`
- Test: `services/dotnet-api/tests/AdvancedRag.Infrastructure.Tests/AppDbContextMigrationTests.cs`

- [ ] **Step 1: Write failing service tests**

Add tests that `CreateLinkAsync` includes `handoff=<code>`, sets a finite `ExpiresAt`, persists a one-time code, and `ConsumeHandoffAsync` rejects expired or already-used codes.

- [ ] **Step 2: Run viewer service tests to verify RED**

Run:

```powershell
dotnet test services\dotnet-api\tests\AdvancedRag.App.Tests\AdvancedRag.App.Tests.csproj --no-restore --filter "FullyQualifiedName~ViewerAccessServiceTests"
```

Expected: FAIL because no handoff repository or consume method exists.

- [ ] **Step 3: Implement app-layer handoff types**

Add `IViewerSessionHandoffRepository`, `ViewerSessionHandoffRecord`, `ConsumeViewerSessionHandoffCommand`, and `ViewerSessionHandoffResult`. Extend `IViewerAccessService` with `ConsumeHandoffAsync`.

- [ ] **Step 4: Implement service behavior**

Generate 32 random bytes as a URL-safe base64url code, store only its SHA-256 hash, persist allowed status scope, user id, document id, purpose, expiry, and used state. TTL is 60 seconds. Consuming validates hash, expiry, used state, document id, marks the record used, and returns the user id plus allowed scope.

- [ ] **Step 5: Add API handoff endpoint**

Add `POST /api/viewer/session-handoff` accepting `{ handoffCode, documentId }`. On success, sign in the returned user id with the existing cookie authentication scheme and return `{ user }`. On invalid/expired/used code, return stable viewer handoff errors.

- [ ] **Step 6: Add EF entity, mapping, and migration**

Create `app.viewer_session_handoff_codes` with id, code hash, user id, document id, purpose, allowed state scope, expires at, consumed at, created at, request id. Add indexes on code hash and expiry. Update mapping and migration tests.

- [ ] **Step 7: Run viewer backend tests to verify GREEN**

Run service, API, mapping, and migration focused tests. Expected: PASS.

---

### Task 3: Manage Frontend Document Editor

**Files:**
- Modify: `apps/manage-web/src/api/documents.ts`
- Modify: `apps/manage-web/src/features/documents/DocumentsPage.tsx`
- Modify: `apps/manage-web/src/features/documents/RichTextEditor.tsx`
- Modify: `apps/manage-web/src/i18n/es-AR.json`
- Modify: `apps/manage-web/src/i18n/en-US.json`
- Modify: `apps/manage-web/src/App.css`
- Test: `apps/manage-web/src/App.test.tsx`

- [ ] **Step 1: Write failing manage-web tests**

Add tests that import uses `contentHtml` when present, quick group creation calls `/api/groups` and selects the created group, and the toolbar exposes paragraph, H1, H2, H3, strike, blockquote, inline code, code block, horizontal rule, undo, and redo.

- [ ] **Step 2: Run manage-web tests to verify RED**

Run:

```powershell
pnpm.cmd --dir apps\manage-web test -- --run src/App.test.tsx -t "imports HTML content|quick-creates a group|exposes the richer TipTap toolbar"
```

Expected: FAIL because the UI lacks these controls and still inserts plain text only.

- [ ] **Step 3: Implement import HTML support**

Extend `ImportExtractionResult` with `contentHtml?: string | null`. In `runImport`, set editor content to `result.contentHtml` when non-empty; otherwise use `plainTextToParagraphHtml(result.text)`.

- [ ] **Step 4: Implement quick group creation**

Import `createGroup`. Add compact name input/action inside the access group fieldset. On success append the group locally, select it, set dirty state, and show localized success. On API failure show localized error with request id.

- [ ] **Step 5: Expand TipTap toolbar**

Add paragraph, H1, H2, H3, strike, blockquote, inline code, code block, horizontal rule, undo, and redo toolbar buttons with lucide icons and active/disabled states. Keep existing link, table, list, underline, bold, italic, and image behavior.

- [ ] **Step 6: Fix editor list/content CSS**

Add scoped list marker, nested list, heading, blockquote, code, horizontal rule, image, and table styles for `.tiptap-editor-surface`.

- [ ] **Step 7: Run manage-web tests to verify GREEN**

Run the same focused manage-web command. Expected: PASS.

---

### Task 4: Docs Frontend Handoff And Viewer CSS

**Files:**
- Modify: `apps/docs-web/src/api/viewer.ts`
- Modify: `apps/docs-web/src/App.tsx`
- Modify: `apps/docs-web/src/App.css`
- Modify: `apps/docs-web/src/i18n/es-AR.json`
- Modify: `apps/docs-web/src/i18n/en-US.json`
- Test: `apps/docs-web/src/App.test.tsx`

- [ ] **Step 1: Write failing docs-web tests**

Add tests that a URL with `handoff` posts to `/api/viewer/session-handoff`, removes `handoff` from the visible URL, then loads the document. Add CSS regression expectations by checking the viewer renders list HTML in `.document-content`.

- [ ] **Step 2: Run docs-web tests to verify RED**

Run:

```powershell
pnpm.cmd --dir apps\docs-web test -- --run src/App.test.tsx -t "handoff|list content"
```

Expected: FAIL because handoff is not consumed and viewer list CSS is incomplete.

- [ ] **Step 3: Implement handoff client**

Add `consumeViewerHandoff(handoffCode, documentId)` that gets CSRF, posts to `/api/viewer/session-handoff`, and returns the session user.

- [ ] **Step 4: Implement handoff bootstrap**

In `ViewerLinkApp`, read `handoff` from the URL. If present, consume it before loading the document and call `history.replaceState` to remove the handoff query parameter while keeping `documentId`.

- [ ] **Step 5: Fix viewer content CSS**

Add scoped list marker, nested list, table, blockquote, code block, horizontal rule, and image styles for `.document-content`.

- [ ] **Step 6: Run docs-web tests to verify GREEN**

Run the same focused docs-web command. Expected: PASS.

---

### Task 5: Context Sync And Final Verification

**Files:**
- Modify: `context/project-overview.md`
- Modify: `context/code-standards.md`
- Modify: `context/progress-tracker.md`
- Modify: `context/design-decisions.md`

- [ ] **Step 1: Fix stale context wording**

Move viewer handoff from out-of-scope wording into in-scope viewer behavior. Pin Mammoth `1.11.0` in `code-standards.md`. Record implementation completion and any verification gaps in `progress-tracker.md`.

- [ ] **Step 2: Run backend verification**

Run:

```powershell
dotnet test services\dotnet-api\tests\AdvancedRag.App.Tests\AdvancedRag.App.Tests.csproj --no-restore --filter "FullyQualifiedName~ViewerAccessServiceTests"
dotnet test services\dotnet-api\tests\AdvancedRag.Infrastructure.Tests\AdvancedRag.Infrastructure.Tests.csproj --no-restore --filter "FullyQualifiedName~DocumentImportExtractionTests|FullyQualifiedName~AppDbContextMappingTests|FullyQualifiedName~AppDbContextMigrationTests"
dotnet test services\dotnet-api\tests\AdvancedRag.Api.Tests\AdvancedRag.Api.Tests.csproj --no-restore --filter "FullyQualifiedName~ViewerEndpointTests"
dotnet build services\dotnet-api\AdvancedRag.sln --no-restore
```

Expected: focused tests and build pass. If Testcontainers is unavailable, document the exact failure.

- [ ] **Step 3: Run frontend verification**

Run:

```powershell
pnpm.cmd --dir apps\manage-web test -- --run src/App.test.tsx
pnpm.cmd --dir apps\manage-web typecheck
pnpm.cmd --dir apps\manage-web build
pnpm.cmd --dir apps\docs-web test -- --run src/App.test.tsx
pnpm.cmd --dir apps\docs-web typecheck
pnpm.cmd --dir apps\docs-web build
```

Expected: focused app tests, typecheck, and builds pass. Known unrelated full manage-web language-switch failures are not part of this focused gate unless they appear in the touched test run.

- [ ] **Step 4: Run repository diff verification**

Run:

```powershell
git diff --check
git status --short
```

Expected: no whitespace errors. Git status may still show pre-existing dirty `.gitignore` and context/spec files.
