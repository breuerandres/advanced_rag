# Access-Model Management UI — Design

- Date: 2026-06-10
- Branch: `roles-refactor`
- Status: Approved (pending spec review)

## Problem

The hierarchical access-model refactor (organizational units + transverse groups,
roles `Admin` / `DocumentEditor` / `DocumentPublisher`) is implemented and verified
in the backend (`.NET` `app` schema, FastAPI retrieval) and partially wired into
`apps/manage-web`. However, several management surfaces are missing, so an operator
cannot actually *see or manage* the new model from the UI:

- There is no screen to view or edit the organizational-unit hierarchy. `orgUnits.ts`
  only exposes `listOrganizationalUnits`; the hierarchy can only be created by seed.
- Group **owner unit** and **publishing policy** are read-only table columns; the
  create/edit group dialogs only send `name`, even though the backend accepts
  `ownerOrganizationalUnitId` and `publishingPolicy`.
- The documents table renders access from `allowedGroupIds` only, so a company-wide
  (unit-only) document shows `-` in the Access column and looks unpermissioned.
- A user's organizational unit is shown read-only after creation; there is no way to
  reassign it (and no backend endpoint for it).

This design adds the missing management UI and the one backend endpoint required to
make the redesigned access model fully visible and editable.

## Existing backend surface (confirmed)

Already implemented and usable as-is:

- `GET /api/organizational-units` — roles `Admin,DocumentEditor,DocumentPublisher`.
  Returns the **active** tree (`ListActiveTreeAsync`): `{ id, name, parentId, depth, isActive }`.
- `POST /api/organizational-units` — role `Admin`. Body `{ name, parentId }`
  (`parentId` required; creating a new root is not supported).
- `PATCH /api/organizational-units/{id}` — role `Admin`. Body `{ name?, isActive? }`.
  Sending `parentId` is rejected (`VALIDATION_FAILED`): **moving branches is not exposed**.
- `POST /api/groups` and `PUT /api/groups/{id}` — roles `Admin,DocumentPublisher`.
  Both accept `{ name, ownerOrganizationalUnitId?, publishingPolicy? }`.
  `publishingPolicy` is normalized to one of `OwnerScope`, `ExplicitGrantOnly`,
  `AdminOnly` (default `OwnerScope`).

Backend gap that this design fills:

- No endpoint to change an existing user's organizational unit. `UsersController`
  exposes roles, groups, status, and ai-budget mutations only, and there is no
  `SetUserOrganizationalUnit` service method.

## Decisions

- **D1 — inactive units:** extend `GET /api/organizational-units` with an optional
  `?includeInactive=true` query parameter. The Admin management page requests the full
  tree (including inactive units, rendered dimmed and reactivatable); every other
  caller (document access-rule editor, group owner selector, create-user dialog) keeps
  the default active-only behavior.
- **D2 — visibility:** the new **Organizational Units** nav section and page are shown
  **only to `Admin`**. The `GET` endpoint stays open to `Admin,DocumentEditor,DocumentPublisher`
  because the document access-rule editor and the group owner selector need it; only the
  dedicated management page is Admin-gated.
- No new libraries. The tree is a custom recursive React component built from
  `parentId`/`depth`, styled with the existing plain CSS conventions. All API calls go
  through the per-app typed client pattern; all user-facing strings are bilingual
  es-AR/en-US via `react-i18next`.

## Scope

### P1 — Organizational Units page (tree editor)

- New `ManagementSection` value `organizational-units` in `ManagementNav.tsx`
  (lucide icon, e.g. `Network`), placed between Documents and Users. Visible to `Admin`
  only (`canAccessSection`), and routed in `App.tsx`.
- New feature page `apps/manage-web/src/features/orgUnits/OrganizationalUnitsPage.tsx`.
- `orgUnits.ts` gains:
  - `listOrganizationalUnits(includeInactive?: boolean)` (adds the query param).
  - `createOrganizationalUnit({ name, parentId })`.
  - `updateOrganizationalUnit(id, { name?, isActive? })`.
- The page loads the full tree (`includeInactive=true`), builds a parent→children map,
  and renders a recursive indented tree with expand/collapse per node.
- Per-node actions (Admin): **Add child** (dialog → `POST`), **Rename**
  (dialog → `PATCH { name }`), **Activate/Deactivate** (`PATCH { isActive }`).
  Inactive units are visually dimmed and labelled.
- The root `Empresa` is labelled "(toda la empresa)" / "(company-wide)" and cannot be
  deactivated.
- No branch moving / drag-and-drop and no physical delete (backend does not expose
  either); documented as a slice limitation.
- Errors surfaced via the shared error envelope through `parseApiError`, shown as
  localized messages with the `requestId` reference, consistent with existing pages.

Reference layout:

```
Organizational Units                                     [+ child of root]
─────────────────────────────────────────────────────────────────────────
▾ Empresa  (company-wide)                             [+ child] [rename]
   ▾ Operations                                       [+ child] [rename] [deactivate]
       • Support L1                                    [+ child] [rename] [deactivate]
       • Support L2  (inactive)                        [+ child] [rename] [activate]
   ▸ Commercial                                        [+ child] [rename] [deactivate]
```

### P2 — Group owner unit + publishing policy

- `GroupDialog` (create) and `GroupEditDialog` (edit) in `UsersBudgetPage.tsx` add:
  - An **owner organizational unit** selector (active units, plus a "No owner" option).
  - A **publishing policy** selector with the three backend values and friendly bilingual
    labels: `OwnerScope`, `ExplicitGrantOnly`, `AdminOnly`.
- `createGroup` / `updateGroup` in `users.ts` send `ownerOrganizationalUnitId` and
  `publishingPolicy` (currently only `name`). Existing group table columns already render
  these fields.

### P3 — Documents access column

- In `DocumentsPage.tsx` the table Access column renders `accessRules` instead of
  `allowedGroupIds`: for each rule, show its unit (root → "company-wide") plus its group
  names; multiple rules are joined with a separator to convey OR. Company-wide documents
  no longer render `-`.
- The existing access filter is preserved (optionally relabelled to "with/without rules").

### P4 — Change a user's organizational unit

- **Backend (.NET):**
  - `SetUserOrganizationalUnitCommand(Guid UserId, Guid OrganizationalUnitId, Guid ActorUserId)`.
  - `IUserAdministrationService.SetUserOrganizationalUnitAsync` + repository update that sets
    the unit **and increments `users.access_scope_version`** (Task 2 rule: org-unit assignment
    changes must bump the access-scope version so cached scopes/answers invalidate).
  - `PUT /api/users/{id}/organizational-unit` — role `Admin`, body `{ organizationalUnitId }`,
    returns the updated `UserResponse`. Validates the unit exists/active.
- **UI:** `UserManagementDialog` replaces the read-only unit line with a selector (Admin only);
  `users.ts` gains `updateUserOrganizationalUnit(id, { organizationalUnitId })`. The dialog only
  issues the call when the unit changed, mirroring the existing role/group change pattern.

## Testing

- **Frontend (`apps/manage-web/src/App.test.tsx`):**
  - Org Units page: create child, rename, toggle active; inactive unit visible when
    `includeInactive` is honored.
  - Group create/edit sends owner unit + publishing policy.
  - Documents Access column shows a company-wide (unit-only) rule rather than `-`.
  - User management dialog changes the organizational unit.
- **Backend (.NET):**
  - `PUT /api/users/{id}/organizational-unit`: success path bumps `access_scope_version`;
    non-admin gets 403; unknown/invalid unit gets the proper error envelope.
  - `GET /api/organizational-units?includeInactive=true` returns inactive units; default
    returns active only.
- Verification commands (run by the user where Docker/Testcontainers are needed):
  - `pnpm.cmd --dir apps\manage-web test -- --run src/App.test.tsx`
  - `pnpm.cmd --dir apps\manage-web typecheck`
  - `pnpm.cmd --dir apps\manage-web build`
  - `dotnet test services\dotnet-api\AdvancedRag.sln --filter "FullyQualifiedName~UserAdministrationEndpointTests|FullyQualifiedName~OrganizationalUnitsEndpointTests"`
  - `dotnet build services\dotnet-api\AdvancedRag.sln --no-restore`
- Two pre-existing TipTap toolbar tests (`Estilo de bloque`) are expected to remain red and
  are out of scope.

## Out of scope

- Moving/reparenting organizational-unit branches (backend rejects it).
- Physical deletion of units (no endpoint; deactivate only).
- Any recomputation of document access beyond what the backend already performs when a
  group's owner unit or a user's unit changes.
- FastAPI/RAG changes (retrieval already honors the model).

## Postman checklist (new/changed endpoints)

- `GET /api/organizational-units?includeInactive=true` — cookie session, Admin; 200 with
  active + inactive units. Without the param: active only.
- `PUT /api/users/{id}/organizational-unit` — cookie session + CSRF double-submit, Admin;
  body `{ "organizationalUnitId": "<guid>" }`; 200 updated user; 403 for non-admin;
  error envelope for invalid unit.
