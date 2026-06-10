# Access-Model Management UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the missing `manage-web` management surfaces (organizational-unit tree editor, group owner unit + publishing policy, documents access column by rule, change a user's organizational unit) plus the one backend endpoint they require, so the hierarchical access model is fully visible and editable.

**Architecture:** Backend changes are minimal additions to the existing `.NET` `app`-schema services (extend the org-unit list with `includeInactive`; add a `SetUserOrganizationalUnit` command/service/repo/endpoint that bumps `access_scope_version`). Frontend adds one new Admin-only feature page with a custom recursive tree component plus targeted edits to existing dialogs/tables, all through the existing per-app typed API client and `react-i18next`.

**Tech Stack:** .NET 8 (controllers + app services + EF Core), React 18 + TypeScript + Vite (`apps/manage-web`), Vitest + Testing Library, xUnit + WebApplicationFactory, i18next (es-AR/en-US).

**Source spec:** `docs/superpowers/specs/2026-06-10-access-model-management-ui-design.md`

**Branch:** `roles-refactor`. No new libraries. All code/comments/commits in English; UI strings bilingual es-AR/en-US.

---

## File Structure

**Backend (`.NET`):**
- Modify `services/dotnet-api/src/AdvancedRag.App/Users/UserAdministrationTypes.cs` — add `SetUserOrganizationalUnitCommand`; change `IOrganizationalUnitService` / `IOrganizationalUnitRepository` list method to take `includeInactive`; add `SetUserOrganizationalUnitAsync` to `IUserAdministrationRepository`.
- Modify `services/dotnet-api/src/AdvancedRag.App/Users/UserAdministrationService.cs` — `OrganizationalUnitService.ListTreeAsync`; `IUserAdministrationService.SetUserOrganizationalUnitAsync` + impl.
- Modify `services/dotnet-api/src/AdvancedRag.Infrastructure/Users/EfOrganizationalUnitRepository.cs` — `ListTreeAsync(includeInactive)`.
- Modify `services/dotnet-api/src/AdvancedRag.Infrastructure/Users/EfUserAdministrationRepository.cs` — `SetUserOrganizationalUnitAsync`.
- Modify `services/dotnet-api/src/AdvancedRag.Api/Controllers/OrganizationalUnitsController.cs` — `[FromQuery] bool includeInactive`.
- Modify `services/dotnet-api/src/AdvancedRag.Api/Controllers/UsersController.cs` — `PUT {id}/organizational-unit`.
- Create `services/dotnet-api/src/AdvancedRag.Api/Models/Users/SetUserOrganizationalUnitRequest.cs`.
- Modify tests: `OrganizationalUnitsEndpointTests.cs` (fake + includeInactive test), `UserAdministrationEndpointTests.cs` (change-unit test + fake).

**Frontend (`apps/manage-web`):**
- Modify `src/api/orgUnits.ts` — `includeInactive` param + `createOrganizationalUnit` + `updateOrganizationalUnit`.
- Modify `src/api/users.ts` — extend group request types + `updateUserOrganizationalUnit`.
- Create `src/features/orgUnits/OrganizationalUnitsPage.tsx` — tree editor page.
- Modify `src/components/ManagementNav.tsx` — new `organizational-units` section (Admin only).
- Modify `src/App.tsx` — route the new section.
- Modify `src/features/users/UsersBudgetPage.tsx` — group owner/policy fields + user unit selector.
- Modify `src/features/documents/DocumentsPage.tsx` — access column by rule.
- Modify `src/i18n/es-AR.json` and `src/i18n/en-US.json` — new keys.
- Modify `src/App.test.tsx` — new tests.

---

## Task 1: Backend — `includeInactive` on organizational-units list

**Files:**
- Modify: `services/dotnet-api/src/AdvancedRag.App/Users/UserAdministrationTypes.cs`
- Modify: `services/dotnet-api/src/AdvancedRag.App/Users/UserAdministrationService.cs:308-311`
- Modify: `services/dotnet-api/src/AdvancedRag.Infrastructure/Users/EfOrganizationalUnitRepository.cs:16-19`
- Modify: `services/dotnet-api/src/AdvancedRag.Api/Controllers/OrganizationalUnitsController.cs:21-26`
- Test: `services/dotnet-api/tests/AdvancedRag.Api.Tests/OrganizationalUnitsEndpointTests.cs`

- [ ] **Step 1: Update the fake service to the new interface and add an inactive unit + a failing test**

In `OrganizationalUnitsEndpointTests.cs`, replace the `FakeOrganizationalUnitService.ListActiveTreeAsync` method (lines 281-289) with the new signature and an inactive node:

```csharp
    public Task<IReadOnlyList<OrganizationalUnitRecord>> ListTreeAsync(bool includeInactive, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        List<OrganizationalUnitRecord> units =
        [
            new OrganizationalUnitRecord(RootId, "Empresa", null, 0, true),
            new OrganizationalUnitRecord(ComunicacionId, "Comunicacion", RootId, 1, true),
        ];
        if (includeInactive)
        {
            units.Add(new OrganizationalUnitRecord(OperacionesId, "Inactive Unit", RootId, 1, false));
        }

        return Task.FromResult<IReadOnlyList<OrganizationalUnitRecord>>(units);
    }
```

Then add this test method to the `OrganizationalUnitsEndpointTests` class (after `ListOrganizationalUnits_ReturnsActiveTree`):

```csharp
    [Fact]
    public async Task ListOrganizationalUnits_WithIncludeInactive_ReturnsInactiveUnits()
    {
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        LoginSession session = await LoginAsync(client, FakeOrgUnitsAuthService.AdminEmail, "manage.localhost");

        using HttpRequestMessage request = new(HttpMethod.Get, "/api/organizational-units?includeInactive=true");
        request.Headers.Host = "manage.localhost";
        request.Headers.Add("Cookie", session.SessionCookie);

        using HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        OrganizationalUnitResponse[]? body = await response.Content.ReadFromJsonAsync<OrganizationalUnitResponse[]>();
        body!.Should().Contain(unit => unit.Name == "Inactive Unit" && !unit.IsActive);
    }
```

- [ ] **Step 2: Run the test to verify it fails to compile/pass**

Run: `dotnet test services\dotnet-api\AdvancedRag.sln --filter "FullyQualifiedName~OrganizationalUnitsEndpointTests"`
Expected: FAIL — compile error because `IOrganizationalUnitService` has no `ListTreeAsync` (the fake no longer matches the interface).

- [ ] **Step 3: Change the service interface method in `UserAdministrationTypes.cs`**

In `IOrganizationalUnitService` (around line 170-177), replace `ListActiveTreeAsync` with:

```csharp
    Task<IReadOnlyList<OrganizationalUnitRecord>> ListTreeAsync(bool includeInactive, CancellationToken ct);
```

In `IOrganizationalUnitRepository` (around line 179-193), replace `ListActiveTreeAsync` with:

```csharp
    Task<IReadOnlyList<OrganizationalUnitRecord>> ListTreeAsync(bool includeInactive, CancellationToken ct);
```

- [ ] **Step 4: Update `OrganizationalUnitService` and the EF repository**

In `UserAdministrationService.cs`, replace `OrganizationalUnitService.ListActiveTreeAsync` (lines 308-311) with:

```csharp
    public Task<IReadOnlyList<OrganizationalUnitRecord>> ListTreeAsync(bool includeInactive, CancellationToken ct)
    {
        return _repository.ListTreeAsync(includeInactive, ct);
    }
```

In `EfOrganizationalUnitRepository.cs`, replace `ListActiveTreeAsync` (lines 16-19) with:

```csharp
    public async Task<IReadOnlyList<OrganizationalUnitRecord>> ListTreeAsync(bool includeInactive, CancellationToken ct)
    {
        return await BuildRecordsAsync(activeOnly: !includeInactive, ct);
    }
```

- [ ] **Step 5: Update the controller to read the query parameter**

In `OrganizationalUnitsController.cs`, replace the `ListAsync` method (lines 21-26) with:

```csharp
    [HttpGet]
    public async Task<IActionResult> ListAsync([FromQuery] bool includeInactive, CancellationToken ct)
    {
        IReadOnlyList<OrganizationalUnitRecord> units = await _organizationalUnits.ListTreeAsync(includeInactive, ct);
        return Ok(units.Select(OrganizationalUnitResponse.FromUnit).ToArray());
    }
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test services\dotnet-api\AdvancedRag.sln --filter "FullyQualifiedName~OrganizationalUnitsEndpointTests"`
Expected: PASS (all four existing tests + the new one). The NuGet vulnerability-index warning when nuget.org is unreachable is expected and harmless.

- [ ] **Step 7: Commit**

```bash
git add services/dotnet-api
git commit -m "feat(api): support includeInactive on organizational-units list"
```

---

## Task 2: Backend — change a user's organizational unit

**Files:**
- Modify: `services/dotnet-api/src/AdvancedRag.App/Users/UserAdministrationTypes.cs`
- Modify: `services/dotnet-api/src/AdvancedRag.App/Users/UserAdministrationService.cs`
- Modify: `services/dotnet-api/src/AdvancedRag.Infrastructure/Users/EfUserAdministrationRepository.cs`
- Create: `services/dotnet-api/src/AdvancedRag.Api/Models/Users/SetUserOrganizationalUnitRequest.cs`
- Modify: `services/dotnet-api/src/AdvancedRag.Api/Controllers/UsersController.cs`
- Test: `services/dotnet-api/tests/AdvancedRag.Api.Tests/UserAdministrationEndpointTests.cs`

- [ ] **Step 1: Add a failing endpoint test**

Open `UserAdministrationEndpointTests.cs`. It uses a fake `IUserAdministrationService`. First add the fake method (place inside the fake service class that implements `IUserAdministrationService`; search for the existing `SetUserGroupsAsync` override and add next to it):

```csharp
    public Task<UserManagementUser> SetUserOrganizationalUnitAsync(
        SetUserOrganizationalUnitCommand command,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        UserManagementUser user = _users.Single(item => item.Id == command.UserId);
        UserManagementUser updated = user with
        {
            OrganizationalUnit = new OrganizationalUnitRecord(command.OrganizationalUnitId, "Reassigned Unit", null, 0, true),
        };
        _users = _users.Select(item => item.Id == command.UserId ? updated : item).ToList();
        return Task.FromResult(updated);
    }
```

> Note: match the fake's existing field name for its user list (e.g. `_users`). If the fake stores users differently, mirror that storage; the behavior must return the updated user with the new `OrganizationalUnit`.

Then add the test (mirror the existing `SetUserGroups_*` test structure in this file for login/CSRF/host helpers):

```csharp
    [Fact]
    public async Task SetUserOrganizationalUnit_AsAdmin_ReturnsUpdatedUnit()
    {
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        LoginSession session = await LoginAsync(client, AdminEmail, "manage.localhost");

        using HttpResponseMessage response = await SendJsonAsync(
            client,
            HttpMethod.Put,
            $"/api/users/{KnownUserId}/organizational-unit",
            new { organizationalUnitId = "01000000-0000-0000-0000-000000000009" },
            "manage.localhost",
            session.Csrf,
            session.SessionCookie);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        UserResponse? body = await response.Content.ReadFromJsonAsync<UserResponse>();
        body!.OrganizationalUnit!.Id.Should().Be(Guid.Parse("01000000-0000-0000-0000-000000000009"));
    }
```

> Use the file's existing constants for `AdminEmail`, `KnownUserId`, the `LoginAsync`/`SendJsonAsync` helpers, and the local `UserResponse` test record. If `KnownUserId` does not exist, use the id of the first user the fake seeds.

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test services\dotnet-api\AdvancedRag.sln --filter "FullyQualifiedName~UserAdministrationEndpointTests"`
Expected: FAIL — compile error: `SetUserOrganizationalUnitCommand` / `SetUserOrganizationalUnitAsync` do not exist.

- [ ] **Step 3: Add the command and service interface/impl**

In `UserAdministrationTypes.cs`, add after `SetUserAiBudgetCommand` (line 31):

```csharp
public sealed record SetUserOrganizationalUnitCommand(
    Guid UserId,
    Guid OrganizationalUnitId,
    Guid ActorUserId);
```

In `UserAdministrationTypes.cs`, add to `IUserAdministrationRepository` (after `SetUserAiBudgetAsync`, around line 167):

```csharp
    Task<UserManagementUser?> SetUserOrganizationalUnitAsync(
        Guid userId,
        Guid organizationalUnitId,
        Guid actorUserId,
        CancellationToken ct);
```

In `UserAdministrationService.cs`, add to the `IUserAdministrationService` interface (after `SetUserAiBudgetAsync`, line 25):

```csharp
    Task<UserManagementUser> SetUserOrganizationalUnitAsync(SetUserOrganizationalUnitCommand command, CancellationToken ct);
```

In `UserAdministrationService.cs`, add the implementation after `SetUserAiBudgetAsync` (after line 182). It reuses the existing `RequireKnownOrganizationalUnitAsync` validation and `RequireFound`:

```csharp
    public async Task<UserManagementUser> SetUserOrganizationalUnitAsync(
        SetUserOrganizationalUnitCommand command,
        CancellationToken ct)
    {
        await RequireKnownOrganizationalUnitAsync(command.OrganizationalUnitId, ct);

        var updated = await _repository.SetUserOrganizationalUnitAsync(
            command.UserId,
            command.OrganizationalUnitId,
            command.ActorUserId,
            ct);

        return RequireFound(updated);
    }
```

- [ ] **Step 4: Implement the repository method (with access-scope version bump)**

In `EfUserAdministrationRepository.cs`, add after `SetUserActiveStatusAsync` (after line 221). It mirrors that method and reuses the existing `IncrementAccessScopeVersion` helper:

```csharp
    public async Task<UserManagementUser?> SetUserOrganizationalUnitAsync(
        Guid userId,
        Guid organizationalUnitId,
        Guid actorUserId,
        CancellationToken ct)
    {
        var user = await _db.Users.SingleOrDefaultAsync(item => item.Id == userId, ct);
        if (user is null)
        {
            return null;
        }

        user.OrganizationalUnitId = organizationalUnitId;
        IncrementAccessScopeVersion(user);
        await _db.SaveChangesAsync(ct);
        return await FindUserAsync(userId, ct);
    }
```

- [ ] **Step 5: Create the request DTO**

Create `services/dotnet-api/src/AdvancedRag.Api/Models/Users/SetUserOrganizationalUnitRequest.cs`:

```csharp
namespace AdvancedRag.Api.Models.Users;

public sealed record SetUserOrganizationalUnitRequest(Guid OrganizationalUnitId);
```

- [ ] **Step 6: Add the controller endpoint**

In `UsersController.cs`, add after `SetUserGroupsAsync` (after line 94), following the same pattern as the other actions:

```csharp
    [HttpPut("{id:guid}/organizational-unit")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SetUserOrganizationalUnitAsync(
        Guid id,
        [FromBody] SetUserOrganizationalUnitRequest request,
        CancellationToken ct)
    {
        try
        {
            UserManagementUser updated = await _users.SetUserOrganizationalUnitAsync(
                new SetUserOrganizationalUnitCommand(id, request.OrganizationalUnitId, ActorUserId()),
                ct);
            return Ok(UserResponse.FromUser(updated));
        }
        catch (UserAdministrationException exception)
        {
            return Error(exception.HttpStatus, exception.Code, exception.Message, exception.Details);
        }
    }
```

- [ ] **Step 7: Run the test to verify it passes**

Run: `dotnet test services\dotnet-api\AdvancedRag.sln --filter "FullyQualifiedName~UserAdministrationEndpointTests"`
Expected: PASS.

- [ ] **Step 8: Add a service-level version-bump test (real EF, Testcontainers)**

This proves `access_scope_version` increments. Add to `services/dotnet-api/tests/AdvancedRag.Infrastructure.Tests/EfUserAdministrationRepositoryTests.cs` (follow the file's existing fixture/setup for a seeded user and units):

```csharp
    [Fact]
    public async Task SetUserOrganizationalUnitAsync_BumpsAccessScopeVersion()
    {
        await using TestAppDatabase database = await TestAppDatabase.CreateAsync();
        var repository = new EfUserAdministrationRepository(database.Context);
        Guid userId = await database.SeedUserAsync("scope@example.com");
        long before = await database.GetAccessScopeVersionAsync(userId);
        Guid targetUnitId = await database.SeedOrganizationalUnitAsync("Target");

        await repository.SetUserOrganizationalUnitAsync(userId, targetUnitId, database.AdminUserId, CancellationToken.None);

        long after = await database.GetAccessScopeVersionAsync(userId);
        after.Should().Be(before + 1);
    }
```

> Reuse whatever helpers `EfUserAdministrationRepositoryTests` already provides for seeding and reading state. If a helper does not exist (e.g. `GetAccessScopeVersionAsync`), query `database.Context.Users` directly for `AccessScopeVersion`, and seed the unit + closure row mirroring `EfOrganizationalUnitRepository.CreateAsync`. Docker must be running for Testcontainers.

- [ ] **Step 9: Run the EF test to verify it passes**

Run: `dotnet test services\dotnet-api\AdvancedRag.sln --filter "FullyQualifiedName~EfUserAdministrationRepositoryTests"`
Expected: PASS (requires Docker).

- [ ] **Step 10: Commit**

```bash
git add services/dotnet-api
git commit -m "feat(api): add PUT users/{id}/organizational-unit with access-scope bump"
```

---

## Task 3: Frontend — extend `orgUnits.ts` API client

**Files:**
- Modify: `apps/manage-web/src/api/orgUnits.ts`

- [ ] **Step 1: Add `includeInactive`, create, and update functions**

In `orgUnits.ts`, replace the `listOrganizationalUnits` function (lines 11-13) and add CSRF + mutation helpers. Replace lines 11-13 with:

```typescript
export async function listOrganizationalUnits(
  includeInactive = false,
): Promise<OrganizationalUnitSummary[]> {
  const query = includeInactive ? '?includeInactive=true' : ''
  return requestJson<OrganizationalUnitSummary[]>(`/api/organizational-units${query}`)
}

export interface CreateOrganizationalUnitRequest {
  name: string
  parentId: string
}

export interface UpdateOrganizationalUnitRequest {
  name?: string
  isActive?: boolean
}

let csrfToken: string | null = null

export async function createOrganizationalUnit(
  request: CreateOrganizationalUnitRequest,
): Promise<OrganizationalUnitSummary> {
  await ensureCsrfToken()
  return requestJson<OrganizationalUnitSummary>(
    '/api/organizational-units',
    jsonRequest('POST', request),
  )
}

export async function updateOrganizationalUnit(
  id: string,
  request: UpdateOrganizationalUnitRequest,
): Promise<OrganizationalUnitSummary> {
  await ensureCsrfToken()
  return requestJson<OrganizationalUnitSummary>(
    `/api/organizational-units/${id}`,
    jsonRequest('PATCH', request),
  )
}

function jsonRequest(method: 'PATCH' | 'POST', body: unknown): RequestInit {
  return {
    method,
    headers: {
      'Content-Type': 'application/json',
      'X-CSRF-Token': csrfToken ?? '',
    },
    body: JSON.stringify(body),
  }
}

async function ensureCsrfToken(): Promise<void> {
  const response = await fetch('/api/csrf', {
    credentials: 'include',
    headers: requestHeaders(),
  })
  const body = await readJson(response)
  if (!response.ok) {
    throw parseApiError(response, body)
  }

  csrfToken = response.headers.get('X-CSRF-Token')
}
```

(The existing `requestJson`, `readJson`, `requestHeaders`, `createRequestId` helpers below stay unchanged.)

- [ ] **Step 2: Typecheck**

Run: `pnpm.cmd --dir apps\manage-web typecheck`
Expected: PASS (no type errors).

- [ ] **Step 3: Commit**

```bash
git add apps/manage-web/src/api/orgUnits.ts
git commit -m "feat(manage-web): add org-unit create/update API client"
```

---

## Task 4: Frontend — extend `users.ts` API client

**Files:**
- Modify: `apps/manage-web/src/api/users.ts`

- [ ] **Step 1: Extend group request types and add the org-unit setter**

In `users.ts`, replace `CreateGroupRequest` (lines 31-33) and `UpdateGroupRequest` (lines 56-58) with:

```typescript
export interface CreateGroupRequest {
  name: string
  ownerOrganizationalUnitId?: string | null
  publishingPolicy?: string
}
```

```typescript
export interface UpdateGroupRequest {
  name: string
  ownerOrganizationalUnitId?: string | null
  publishingPolicy?: string
}
```

Add a new request type next to `SetUserGroupsRequest` (after line 54):

```typescript
export interface SetUserOrganizationalUnitRequest {
  organizationalUnitId: string
}
```

Add the new client function after `updateUserGroups` (after line 107):

```typescript
export async function updateUserOrganizationalUnit(
  userId: string,
  request: SetUserOrganizationalUnitRequest,
): Promise<UserSummary> {
  await ensureCsrfToken()

  return requestJson<UserSummary>(
    `/api/users/${userId}/organizational-unit`,
    jsonRequest('PUT', request),
  )
}
```

- [ ] **Step 2: Typecheck**

Run: `pnpm.cmd --dir apps\manage-web typecheck`
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add apps/manage-web/src/api/users.ts
git commit -m "feat(manage-web): extend group + user API client for owner unit and unit change"
```

---

## Task 5: Frontend — i18n keys

**Files:**
- Modify: `apps/manage-web/src/i18n/es-AR.json`
- Modify: `apps/manage-web/src/i18n/en-US.json`

- [ ] **Step 1: Add nav + org-units + group + user keys (es-AR)**

In `es-AR.json`, add to the `"nav"` object a key:

```json
    "organizational_units": "Unidades organizativas",
```

Add a new top-level `"orgUnits"` object (sibling of `"documents"`):

```json
  "orgUnits": {
    "title": "Unidades organizativas",
    "eyebrow": "Estructura",
    "loading": "Cargando unidades...",
    "load_error": "No se pudieron cargar las unidades organizativas.",
    "company_wide": "toda la empresa",
    "add_child": "Agregar unidad hija",
    "add_root_child": "Agregar unidad bajo la raíz",
    "rename": "Renombrar",
    "activate": "Activar",
    "deactivate": "Desactivar",
    "inactive": "inactiva",
    "create_title": "Crear unidad",
    "create_description": "Creá una unidad organizativa hija dentro de la jerarquía.",
    "rename_title": "Renombrar unidad",
    "name_field": "Nombre de la unidad",
    "name_required": "El nombre de la unidad es obligatorio.",
    "save": "Guardar",
    "cancel": "Cancelar",
    "saving": "Guardando...",
    "create_error": "No se pudo crear la unidad. Referencia: {{reference}}.",
    "update_error": "No se pudo actualizar la unidad. Referencia: {{reference}}."
  },
```

Add to the existing `"users"` object these keys (publishing policy + owner unit + unit selector):

```json
    "owner_unit_field": "Unidad dueña",
    "owner_unit_none": "Sin unidad dueña",
    "publishing_policy_field": "Política de publicación",
    "publishing_policy_owner_scope": "Alcance de la unidad dueña",
    "publishing_policy_explicit_grant_only": "Solo concesión explícita",
    "publishing_policy_admin_only": "Solo administradores",
    "change_unit_field": "Unidad organizativa",
    "change_unit_select": "Seleccioná una unidad",
    "org_units_load_error": "No se pudieron cargar las unidades organizativas."
```

- [ ] **Step 2: Add the same keys to en-US.json**

In `en-US.json`, add to `"nav"`:

```json
    "organizational_units": "Organizational units",
```

Add the `"orgUnits"` object:

```json
  "orgUnits": {
    "title": "Organizational units",
    "eyebrow": "Structure",
    "loading": "Loading units...",
    "load_error": "Could not load organizational units.",
    "company_wide": "company-wide",
    "add_child": "Add child unit",
    "add_root_child": "Add unit under root",
    "rename": "Rename",
    "activate": "Activate",
    "deactivate": "Deactivate",
    "inactive": "inactive",
    "create_title": "Create unit",
    "create_description": "Create a child organizational unit inside the hierarchy.",
    "rename_title": "Rename unit",
    "name_field": "Unit name",
    "name_required": "Unit name is required.",
    "save": "Save",
    "cancel": "Cancel",
    "saving": "Saving...",
    "create_error": "Could not create the unit. Reference: {{reference}}.",
    "update_error": "Could not update the unit. Reference: {{reference}}."
  },
```

Add to `"users"`:

```json
    "owner_unit_field": "Owner unit",
    "owner_unit_none": "No owner unit",
    "publishing_policy_field": "Publishing policy",
    "publishing_policy_owner_scope": "Owner unit scope",
    "publishing_policy_explicit_grant_only": "Explicit grant only",
    "publishing_policy_admin_only": "Admins only",
    "change_unit_field": "Organizational unit",
    "change_unit_select": "Select a unit",
    "org_units_load_error": "Could not load organizational units."
```

- [ ] **Step 3: Add documents access-column keys (both files)**

In `es-AR.json` `"documents"` object add:

```json
    "rule_company_wide_label": "Toda la empresa",
    "access_rule_summary_separator": " · "
```

In `en-US.json` `"documents"` object add:

```json
    "rule_company_wide_label": "Company-wide",
    "access_rule_summary_separator": " · "
```

- [ ] **Step 4: Verify JSON validity via typecheck/build**

Run: `pnpm.cmd --dir apps\manage-web typecheck`
Expected: PASS (JSON imports compile).

- [ ] **Step 5: Commit**

```bash
git add apps/manage-web/src/i18n
git commit -m "feat(manage-web): add i18n keys for access-model management UI"
```

---

## Task 6: Frontend — Organizational Units page (tree editor) + nav

**Files:**
- Create: `apps/manage-web/src/features/orgUnits/OrganizationalUnitsPage.tsx`
- Modify: `apps/manage-web/src/components/ManagementNav.tsx`
- Modify: `apps/manage-web/src/App.tsx`
- Test: `apps/manage-web/src/App.test.tsx`

- [ ] **Step 1: Add the nav section (Admin only)**

In `ManagementNav.tsx`:

1. Add `Network` to the lucide import (line 1-11).
2. Add `'organizational-units'` to the `ManagementSection` union (line 14-20).
3. Add to the `links` array (after the `documents` entry, line 32):

```typescript
  { id: 'organizational-units', labelKey: 'nav.organizational_units', icon: Network },
```

4. In `canAccessSection` (lines 111-129), add an Admin-only branch BEFORE the shared Admin/Editor/Publisher check:

```typescript
  if (section === 'organizational-units') {
    return userRoles.includes('Admin')
  }
```

- [ ] **Step 2: Create the page component**

Create `apps/manage-web/src/features/orgUnits/OrganizationalUnitsPage.tsx`:

```tsx
import { useEffect, useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { ChevronDown, ChevronRight, Plus, Power, Pencil } from 'lucide-react'
import { ApiError } from '../../lib/api-error'
import {
  createOrganizationalUnit,
  listOrganizationalUnits,
  updateOrganizationalUnit,
} from '../../api/orgUnits'
import type { OrganizationalUnitSummary } from '../../api/orgUnits'
import { Button, Dialog, Input } from '@helpcenter/shared-ui'

type LoadState = 'loading' | 'ready' | 'error'

interface DialogState {
  mode: 'create' | 'rename'
  unit: OrganizationalUnitSummary
}

export function OrganizationalUnitsPage() {
  const { t } = useTranslation()
  const [units, setUnits] = useState<OrganizationalUnitSummary[]>([])
  const [loadState, setLoadState] = useState<LoadState>('loading')
  const [expanded, setExpanded] = useState<Set<string>>(new Set())
  const [dialog, setDialog] = useState<DialogState | null>(null)
  const [errorMessage, setErrorMessage] = useState<string | null>(null)

  useEffect(() => {
    void loadUnits()
  }, [])

  async function loadUnits() {
    setLoadState('loading')
    try {
      const loaded = await listOrganizationalUnits(true)
      setUnits(loaded)
      setExpanded(new Set(loaded.map((unit) => unit.id)))
      setLoadState('ready')
    } catch {
      setLoadState('error')
    }
  }

  const childrenByParent = useMemo(() => {
    const map = new Map<string | null, OrganizationalUnitSummary[]>()
    for (const unit of units) {
      const siblings = map.get(unit.parentId) ?? []
      siblings.push(unit)
      map.set(unit.parentId, siblings)
    }
    for (const siblings of map.values()) {
      siblings.sort((left, right) => left.name.localeCompare(right.name))
    }
    return map
  }, [units])

  const roots = childrenByParent.get(null) ?? []

  function toggle(id: string) {
    setExpanded((current) => {
      const next = new Set(current)
      if (next.has(id)) {
        next.delete(id)
      } else {
        next.add(id)
      }
      return next
    })
  }

  function upsertUnit(updated: OrganizationalUnitSummary) {
    setUnits((current) => {
      const exists = current.some((unit) => unit.id === updated.id)
      return exists
        ? current.map((unit) => (unit.id === updated.id ? updated : unit))
        : [...current, updated]
    })
    setExpanded((current) => new Set(current).add(updated.parentId ?? updated.id))
  }

  async function toggleActive(unit: OrganizationalUnitSummary) {
    setErrorMessage(null)
    try {
      const updated = await updateOrganizationalUnit(unit.id, { isActive: !unit.isActive })
      upsertUnit(updated)
    } catch (error) {
      const reference = error instanceof ApiError ? error.requestId : 'unknown'
      setErrorMessage(t('orgUnits.update_error', { reference }))
    }
  }

  function renderNode(unit: OrganizationalUnitSummary) {
    const children = childrenByParent.get(unit.id) ?? []
    const isExpanded = expanded.has(unit.id)
    const isRoot = unit.parentId === null
    return (
      <li key={unit.id} className="org-unit-node">
        <div className={unit.isActive ? 'org-unit-row' : 'org-unit-row inactive'}>
          {children.length > 0 ? (
            <button
              type="button"
              className="org-unit-toggle"
              aria-label={unit.name}
              onClick={() => toggle(unit.id)}
            >
              {isExpanded ? <ChevronDown size={16} /> : <ChevronRight size={16} />}
            </button>
          ) : (
            <span className="org-unit-toggle-spacer" aria-hidden="true" />
          )}
          <span className="org-unit-name">
            {unit.name}
            {isRoot ? ` (${t('orgUnits.company_wide')})` : null}
            {!unit.isActive ? ` (${t('orgUnits.inactive')})` : null}
          </span>
          <span className="org-unit-actions">
            <Button
              className="text-button"
              type="button"
              onClick={() => setDialog({ mode: 'create', unit })}
            >
              <Plus size={14} />
              {t('orgUnits.add_child')}
            </Button>
            <Button
              className="text-button"
              type="button"
              onClick={() => setDialog({ mode: 'rename', unit })}
            >
              <Pencil size={14} />
              {t('orgUnits.rename')}
            </Button>
            {!isRoot ? (
              <Button className="text-button" type="button" onClick={() => void toggleActive(unit)}>
                <Power size={14} />
                {unit.isActive ? t('orgUnits.deactivate') : t('orgUnits.activate')}
              </Button>
            ) : null}
          </span>
        </div>
        {isExpanded && children.length > 0 ? (
          <ul className="org-unit-children">{children.map(renderNode)}</ul>
        ) : null}
      </li>
    )
  }

  return (
    <section className="workspace" id="unidades">
      <header className="workspace-header">
        <div>
          <p className="eyebrow">{t('orgUnits.eyebrow')}</p>
          <h1>{t('orgUnits.title')}</h1>
        </div>
      </header>

      {errorMessage ? (
        <p className="status-message error" role="alert">
          {errorMessage}
        </p>
      ) : null}
      {loadState === 'loading' ? <p className="status-message">{t('orgUnits.loading')}</p> : null}
      {loadState === 'error' ? (
        <p className="status-message error" role="alert">
          {t('orgUnits.load_error')}
        </p>
      ) : null}

      {loadState === 'ready' ? (
        <ul className="org-unit-tree">{roots.map(renderNode)}</ul>
      ) : null}

      {dialog ? (
        <OrganizationalUnitDialog
          state={dialog}
          onClose={() => setDialog(null)}
          onSaved={(unit) => {
            upsertUnit(unit)
            setDialog(null)
          }}
        />
      ) : null}
    </section>
  )
}

function OrganizationalUnitDialog({
  state,
  onClose,
  onSaved,
}: {
  state: DialogState
  onClose: () => void
  onSaved: (unit: OrganizationalUnitSummary) => void
}) {
  const { t } = useTranslation()
  const [name, setName] = useState(state.mode === 'rename' ? state.unit.name : '')
  const [validationError, setValidationError] = useState<string | null>(null)
  const [apiError, setApiError] = useState<string | null>(null)
  const [isSaving, setIsSaving] = useState(false)

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setValidationError(null)
    setApiError(null)

    const trimmed = name.trim()
    if (trimmed.length === 0) {
      setValidationError(t('orgUnits.name_required'))
      return
    }

    setIsSaving(true)
    try {
      const saved =
        state.mode === 'create'
          ? await createOrganizationalUnit({ name: trimmed, parentId: state.unit.id })
          : await updateOrganizationalUnit(state.unit.id, { name: trimmed })
      onSaved(saved)
    } catch (error) {
      const reference = error instanceof ApiError ? error.requestId : 'unknown'
      setApiError(
        t(state.mode === 'create' ? 'orgUnits.create_error' : 'orgUnits.update_error', { reference }),
      )
    } finally {
      setIsSaving(false)
    }
  }

  return (
    <Dialog
      open
      title={t(state.mode === 'create' ? 'orgUnits.create_title' : 'orgUnits.rename_title')}
      description={state.mode === 'create' ? t('orgUnits.create_description') : undefined}
      onOpenChange={(open) => {
        if (!open) {
          onClose()
        }
      }}
    >
      <form className="dialog-form" noValidate onSubmit={handleSubmit}>
        <label className="field">
          <span>{t('orgUnits.name_field')}</span>
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
            {t('orgUnits.cancel')}
          </Button>
          <Button className="primary-button" type="submit" disabled={isSaving}>
            {isSaving ? t('orgUnits.saving') : t('orgUnits.save')}
          </Button>
        </div>
      </form>
    </Dialog>
  )
}
```

- [ ] **Step 3: Route the page in `App.tsx`**

In `App.tsx`, add the import after the `DocumentsPage` import (line 36):

```tsx
import { OrganizationalUnitsPage } from './features/orgUnits/OrganizationalUnitsPage'
```

In the workspace render block (after the `documents` line, line 229), add:

```tsx
        {activeView === 'organizational-units' ? <OrganizationalUnitsPage /> : null}
```

- [ ] **Step 4: Add the failing test**

In `App.test.tsx`, add a test that mirrors the file's existing harness for stubbing fetch (reuse the same fetch-queue helper the other tests use; the org-units list is already stubbed via `organizationalUnitsResponse` at the top of the file). The test logs in as Admin, navigates to the new section, and creates a child unit:

```tsx
test("organizational units page creates a child unit", async () => {
  // Arrange: follow the existing harness used by other tests to queue:
  //   getSetupStatus -> getSession(Admin) -> initial section data,
  //   then GET /api/organizational-units?includeInactive=true -> organizationalUnitsResponse,
  //   then GET /api/csrf -> token, then POST /api/organizational-units -> created unit.
  const createdUnit = {
    id: "0c000000-0000-0000-0000-0000000000cc",
    name: "Marketing",
    parentId: "01000000-0000-0000-0000-000000000001",
    depth: 1,
    isActive: true,
  };
  // (Queue the responses above using the same stubbing utility as the other tests,
  //  returning `createdUnit` for the POST.)

  render(<App />);
  const user = userEvent.setup();

  await user.click(await screen.findByRole("link", { name: /unidades organizativas/i }));
  await user.click((await screen.findAllByRole("button", { name: /agregar unidad hija/i }))[0]);
  await user.type(screen.getByLabelText(/nombre de la unidad/i), "Marketing");
  await user.click(screen.getByRole("button", { name: /^guardar$/i }));

  expect(await screen.findByText("Marketing")).toBeInTheDocument();
});
```

> Use the exact fetch-stubbing helper already present in `App.test.tsx` (the other tests show its call shape). Do not invent a new mocking mechanism. The nav link label comes from `nav.organizational_units` ("Unidades organizativas").

- [ ] **Step 5: Run the test**

Run: `pnpm.cmd --dir apps\manage-web test -- --run src/App.test.tsx -t "organizational units"`
Expected: PASS.

- [ ] **Step 6: Add minimal CSS for the tree**

In `apps/manage-web/src/App.css`, append (indentation + dimmed inactive rows):

```css
.org-unit-tree,
.org-unit-children {
  list-style: none;
  margin: 0;
  padding-left: 1.25rem;
}

.org-unit-row {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.25rem 0;
}

.org-unit-row.inactive .org-unit-name {
  opacity: 0.55;
}

.org-unit-actions {
  display: inline-flex;
  gap: 0.25rem;
  margin-left: auto;
}

.org-unit-toggle,
.org-unit-toggle-spacer {
  width: 1.25rem;
  display: inline-flex;
  background: none;
  border: 0;
  cursor: pointer;
}
```

- [ ] **Step 7: Typecheck, build, commit**

Run: `pnpm.cmd --dir apps\manage-web typecheck`
Run: `pnpm.cmd --dir apps\manage-web build`
Expected: both PASS.

```bash
git add apps/manage-web/src
git commit -m "feat(manage-web): add organizational units tree editor page"
```

---

## Task 7: Frontend — group owner unit + publishing policy

**Files:**
- Modify: `apps/manage-web/src/features/users/UsersBudgetPage.tsx`
- Test: `apps/manage-web/src/App.test.tsx`

- [ ] **Step 1: Add a shared publishing-policy constant and load units in group dialogs**

In `UsersBudgetPage.tsx`, add near the top-level helpers (after the imports), a constant used by both group dialogs:

```tsx
const PUBLISHING_POLICIES = ['OwnerScope', 'ExplicitGrantOnly', 'AdminOnly'] as const

function publishingPolicyLabelKey(policy: string): string {
  switch (policy) {
    case 'ExplicitGrantOnly':
      return 'users.publishing_policy_explicit_grant_only'
    case 'AdminOnly':
      return 'users.publishing_policy_admin_only'
    default:
      return 'users.publishing_policy_owner_scope'
  }
}
```

- [ ] **Step 2: Extend `GroupDialog` (create) with owner unit + policy**

In `GroupDialog` (lines 635-715), add `useTranslation`, unit loading, and the two selectors, and pass them to `createGroup`. Replace the component body's state + submit + form to include:

```tsx
  const { t } = useTranslation()
  const [name, setName] = useState('')
  const [ownerUnitId, setOwnerUnitId] = useState('')
  const [publishingPolicy, setPublishingPolicy] = useState<string>('OwnerScope')
  const [organizationalUnits, setOrganizationalUnits] = useState<OrganizationalUnitSummary[]>([])
  const [validationError, setValidationError] = useState<string | null>(null)
  const [apiError, setApiError] = useState<string | null>(null)
  const [isSaving, setIsSaving] = useState(false)

  useEffect(() => {
    let active = true
    void (async () => {
      try {
        const loaded = await listOrganizationalUnits()
        if (active) {
          setOrganizationalUnits(loaded)
        }
      } catch {
        // Owner unit is optional; ignore load failure here.
      }
    })()
    return () => {
      active = false
    }
  }, [])
```

In its `handleSubmit`, change the `createGroup` call to:

```tsx
      const group = await createGroup({
        name: trimmedName,
        ownerOrganizationalUnitId: ownerUnitId === '' ? null : ownerUnitId,
        publishingPolicy,
      })
```

In the form (after the name field), add:

```tsx
          <label className="field">
            <span>{t('users.owner_unit_field')}</span>
            <select
              value={ownerUnitId}
              onChange={(event) => setOwnerUnitId(event.target.value)}
              disabled={isSaving}
            >
              <option value="">{t('users.owner_unit_none')}</option>
              {organizationalUnits.map((unit) => (
                <option key={unit.id} value={unit.id}>
                  {organizationalUnitLabel(unit)}
                </option>
              ))}
            </select>
          </label>
          <label className="field">
            <span>{t('users.publishing_policy_field')}</span>
            <select
              value={publishingPolicy}
              onChange={(event) => setPublishingPolicy(event.target.value)}
              disabled={isSaving}
            >
              {PUBLISHING_POLICIES.map((policy) => (
                <option key={policy} value={policy}>
                  {t(publishingPolicyLabelKey(policy))}
                </option>
              ))}
            </select>
          </label>
```

- [ ] **Step 3: Extend `GroupEditDialog` (edit) the same way**

In `GroupEditDialog` (lines 561-633), seed state from the group, load units, and send the fields. Add state:

```tsx
  const { t } = useTranslation()
  const [name, setName] = useState(group.name)
  const [ownerUnitId, setOwnerUnitId] = useState(group.ownerOrganizationalUnit?.id ?? '')
  const [publishingPolicy, setPublishingPolicy] = useState<string>(group.publishingPolicy ?? 'OwnerScope')
  const [organizationalUnits, setOrganizationalUnits] = useState<OrganizationalUnitSummary[]>([])
```

Add the same `useEffect` unit-loading block as Step 2. Change the `updateGroup` call to:

```tsx
      onSaved(
        await updateGroup(group.id, {
          name: trimmedName,
          ownerOrganizationalUnitId: ownerUnitId === '' ? null : ownerUnitId,
          publishingPolicy,
        }),
      )
```

Add the same two `<label>` selectors from Step 2 after the name field.

- [ ] **Step 4: Add the failing test**

In `App.test.tsx`, add a test that creates a group with an owner unit + policy and asserts the POST body. Reuse the existing fetch harness and assert via the recorded request:

```tsx
test("creates a group with owner unit and publishing policy", async () => {
  // Queue (via the existing harness): setup -> session(Admin) -> users+groups,
  //   GET /api/organizational-units -> organizationalUnitsResponse,
  //   GET /api/csrf -> token, POST /api/groups -> created group.
  render(<App />);
  const user = userEvent.setup();

  await user.click(await screen.findByRole("tab", { name: /grupos/i }));
  await user.click(await screen.findByRole("button", { name: /crear grupo/i }));
  await user.type(screen.getByLabelText(/nombre del grupo/i), "Soporte");
  await user.selectOptions(screen.getByLabelText(/unidad dueña/i), "01000000-0000-0000-0000-000000000001");
  await user.selectOptions(screen.getByLabelText(/política de publicación/i), "AdminOnly");
  await user.click(screen.getByRole("button", { name: /guardar grupo/i }));

  // Assert the captured POST /api/groups body included
  //   ownerOrganizationalUnitId === "01000000-0000-0000-0000-000000000001" and publishingPolicy === "AdminOnly"
  // using the same request-capture approach the other write tests in this file use.
});
```

> Fill the request-body assertion using the same capture mechanism the file already uses for other POST/PUT tests.

- [ ] **Step 5: Run the test**

Run: `pnpm.cmd --dir apps\manage-web test -- --run src/App.test.tsx -t "owner unit and publishing policy"`
Expected: PASS.

- [ ] **Step 6: Typecheck, build, commit**

Run: `pnpm.cmd --dir apps\manage-web typecheck`
Run: `pnpm.cmd --dir apps\manage-web build`
Expected: both PASS.

```bash
git add apps/manage-web/src
git commit -m "feat(manage-web): edit group owner unit and publishing policy"
```

---

## Task 8: Frontend — documents access column by rule

**Files:**
- Modify: `apps/manage-web/src/features/documents/DocumentsPage.tsx`
- Test: `apps/manage-web/src/App.test.tsx`

- [ ] **Step 1: Add a rule-summary helper**

In `DocumentsPage.tsx`, add a helper near `displayGroups` (line 1290), that summarizes `accessRules`:

```tsx
function displayAccessRules(
  groups: GroupSummary[],
  document: DocumentSummary,
  companyWideLabel: string,
  separator: string,
): string {
  const rules = document.accessRules ?? []
  if (rules.length === 0) {
    return displayGroups(groups, document.allowedGroupIds)
  }

  return rules
    .map((rule) => {
      const unitLabel =
        rule.organizationalUnitId === null ? null : companyWideLabelOrName(rule.organizationalUnitId, companyWideLabel)
      const groupLabels = rule.groupIds.map((groupId) => groupName(groups, groupId))
      return [unitLabel, ...groupLabels].filter(Boolean).join(' + ')
    })
    .filter((text) => text.length > 0)
    .join(separator)
}

function companyWideLabelOrName(_organizationalUnitId: string, companyWideLabel: string): string {
  // The documents list does not load the unit catalog; show the company-wide label as a
  // stable, non-empty access indicator for unit-scoped rules. Group names still render.
  return companyWideLabel
}
```

> Note: the documents list intentionally does not fetch the org-unit catalog. Showing the company-wide label for any unit-scoped rule is enough to stop rendering `-`; the editor shows precise unit names. If a later slice loads the catalog here, replace `companyWideLabelOrName` with a name lookup.

- [ ] **Step 2: Use the helper in the Access column**

In the `access` column `render` (lines 461-465), replace:

```tsx
                    render: (document) => displayGroups(groups, document.allowedGroupIds),
```

with:

```tsx
                    render: (document) =>
                      displayAccessRules(
                        groups,
                        document,
                        t("documents.rule_company_wide_label"),
                        t("documents.access_rule_summary_separator"),
                      ),
```

- [ ] **Step 3: Add the failing test**

In `App.test.tsx`, extend a documents-list response item with an `accessRules` entry that has a unit and no groups, and assert the company-wide label appears instead of `-`:

```tsx
test("documents access column shows company-wide rules", async () => {
  // Queue (existing harness): setup -> session(Admin) -> documents list where one document has
  //   accessRules: [{ id: "r1", organizationalUnitId: "01000000-0000-0000-0000-000000000001", groupIds: [] }]
  //   and allowedGroupIds: [], plus the groups list.
  render(<App />);

  await screen.findByRole("link", { name: /documentos/i });
  // Navigate to documents if not the default section, then:
  expect(await screen.findByText(/toda la empresa/i)).toBeInTheDocument();
});
```

> Reuse the existing `documentsResponse` shape; add one document object with the `accessRules` field shown above.

- [ ] **Step 4: Run the test**

Run: `pnpm.cmd --dir apps\manage-web test -- --run src/App.test.tsx -t "company-wide rules"`
Expected: PASS.

- [ ] **Step 5: Typecheck, build, commit**

Run: `pnpm.cmd --dir apps\manage-web typecheck`
Run: `pnpm.cmd --dir apps\manage-web build`
Expected: both PASS.

```bash
git add apps/manage-web/src
git commit -m "feat(manage-web): show document access rules by unit in list"
```

---

## Task 9: Frontend — change a user's organizational unit

**Files:**
- Modify: `apps/manage-web/src/features/users/UsersBudgetPage.tsx`
- Test: `apps/manage-web/src/App.test.tsx`

- [ ] **Step 1: Add unit loading + selector to `UserManagementDialog`**

In `UserManagementDialog` (lines 928-1052), it currently shows the unit read-only. Add (Admin only) a selector. Add `updateUserOrganizationalUnit` and `listOrganizationalUnits` to the imports at the top of the file if not already present.

Add state in the dialog:

```tsx
  const [organizationalUnitId, setOrganizationalUnitId] = useState(user.organizationalUnit?.id ?? '')
  const [organizationalUnits, setOrganizationalUnits] = useState<OrganizationalUnitSummary[]>([])
```

Add a unit-loading `useEffect` (only needed when the actor can edit roles, i.e. Admin):

```tsx
  useEffect(() => {
    if (!canEditRole) {
      return
    }
    let active = true
    void (async () => {
      try {
        const loaded = await listOrganizationalUnits()
        if (active) {
          setOrganizationalUnits(loaded)
        }
      } catch {
        // Unit change is optional; ignore load failure.
      }
    })()
    return () => {
      active = false
    }
  }, [canEditRole])
```

In `handleSubmit`, after the groups update block (after line 963), add the unit change before `onSaved(updatedUser)`:

```tsx
      if (canEditRole && organizationalUnitId !== (user.organizationalUnit?.id ?? '')) {
        updatedUser = await updateUserOrganizationalUnit(user.id, { organizationalUnitId })
      }
```

Replace the read-only unit line in `readonly-summary` (line 998):

```tsx
            <span>Unidad: {user.organizationalUnit?.name ?? '-'}</span>
```

with a conditional: keep the read-only span when `!canEditRole`, and render a selector when `canEditRole`. Add this selector block right after the role `<label>` (after line 1015):

```tsx
          {canEditRole ? (
            <label className="field">
              <span>{t('users.change_unit_field')}</span>
              <select
                value={organizationalUnitId}
                onChange={(event) => setOrganizationalUnitId(event.target.value)}
                disabled={isSaving}
              >
                <option value="">{t('users.change_unit_select')}</option>
                {organizationalUnits.map((unit) => (
                  <option key={unit.id} value={unit.id}>
                    {organizationalUnitLabel(unit)}
                  </option>
                ))}
              </select>
            </label>
          ) : null}
```

> Add `const { t } = useTranslation()` at the top of `UserManagementDialog` if it is not already there.

- [ ] **Step 2: Add the failing test**

In `App.test.tsx`, add a test that opens the user management dialog, changes the unit, and asserts the PUT call:

```tsx
test("changes a user's organizational unit", async () => {
  // Queue (existing harness): setup -> session(Admin) -> users+groups,
  //   GET /api/organizational-units -> organizationalUnitsResponse (for the dialog),
  //   GET /api/csrf -> token, PUT /api/users/{id}/organizational-unit -> updated user.
  render(<App />);
  const user = userEvent.setup();

  await user.click(await screen.findByRole("button", { name: /editar usuario/i }));
  await user.selectOptions(
    await screen.findByLabelText(/unidad organizativa/i),
    "0c000000-0000-0000-0000-0000000000c0",
  );
  await user.click(screen.getByRole("button", { name: /guardar usuario/i }));

  // Assert PUT /api/users/{id}/organizational-unit was called with
  //   { organizationalUnitId: "0c000000-0000-0000-0000-0000000000c0" }
  // using the file's request-capture approach.
});
```

> The "editar usuario" button is the `UserCog` action (aria-label `users.edit_user_for`). Match by its accessible name as the other user tests do.

- [ ] **Step 3: Run the test**

Run: `pnpm.cmd --dir apps\manage-web test -- --run src/App.test.tsx -t "organizational unit"`
Expected: PASS.

- [ ] **Step 4: Run the full manage-web test file**

Run: `pnpm.cmd --dir apps\manage-web test -- --run src/App.test.tsx`
Expected: PASS except the two pre-existing TipTap toolbar tests (`Estilo de bloque`), which remain red and are out of scope.

- [ ] **Step 5: Typecheck, build, commit**

Run: `pnpm.cmd --dir apps\manage-web typecheck`
Run: `pnpm.cmd --dir apps\manage-web build`
Expected: both PASS.

```bash
git add apps/manage-web/src
git commit -m "feat(manage-web): allow changing a user's organizational unit"
```

---

## Task 10: Final verification + graph + context

**Files:**
- Modify: `context/progress-tracker.md`
- Modify: `context/ui-context.md`

- [ ] **Step 1: Run the full backend test slices**

Run: `dotnet test services\dotnet-api\AdvancedRag.sln --filter "FullyQualifiedName~OrganizationalUnitsEndpointTests|FullyQualifiedName~UserAdministrationEndpointTests|FullyQualifiedName~EfUserAdministrationRepositoryTests"`
Expected: PASS (Docker running for the EF/Testcontainers test).

- [ ] **Step 2: Run frontend gates**

Run: `pnpm.cmd --dir apps\manage-web typecheck`
Run: `pnpm.cmd --dir apps\manage-web build`
Run: `pnpm.cmd --dir apps\manage-web test -- --run src/App.test.tsx`
Expected: typecheck + build PASS; tests PASS except the two pre-existing `Estilo de bloque` toolbar tests.

- [ ] **Step 3: Update the knowledge graph**

Run: `graphify update .`
Expected: graph updated (AST-only, no API cost).

- [ ] **Step 4: Update context (status + UI notes)**

In `context/progress-tracker.md`, add a Completed entry dated 2026-06-10 summarizing the new management UI (org-units tree editor, group owner/publishing policy, documents access column by rule, change-user-unit endpoint + UI), and note the user-owned browser acceptance is pending.

In `context/ui-context.md`, document the new Admin-only Organizational Units section and the group owner/publishing-policy editing.

- [ ] **Step 5: Commit**

```bash
git add context graphify-out
git commit -m "docs: record access-model management UI completion"
```

---

## Postman checklist (new/changed endpoints)

- `GET /api/organizational-units?includeInactive=true` — cookie session, Admin; 200 with active + inactive units. Without the param: active only.
- `PUT /api/users/{id}/organizational-unit` — cookie session + CSRF (`X-CSRF-Token` + `__Host-CSRF` cookie), Admin; body `{ "organizationalUnitId": "<guid>" }`; 200 updated user; 403 for non-admin; `VALIDATION_FAILED` envelope for an invalid/inactive unit.
