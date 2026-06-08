# Hierarchical Access Refactor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the current flat group-only document access model with a strict hybrid model: one primary organizational-unit node per user, transverse groups for cross-cutting access, scoped document-management permissions, and branch-aware RAG/docs visibility.

**Architecture:** `.NET` remains the owner of authentication, users, organizational units, groups, document lifecycle, document-access rules, viewer authorization, and the `app` schema. FastAPI remains the owner of chat/RAG and the `rag` schema, but reads the approved `.NET` access tables through explicit read-only grants. `Admin` is the only unrestricted role. Non-admin users receive scoped `DocumentEditor` or `DocumentPublisher` capabilities and are limited to their organizational unit plus descendants for management actions. Chat/docs retrieval uses branch-scoped rules plus group rules, with AND semantics within one rule and OR semantics between rules.

**Tech Stack:** .NET 8, EF Core migrations, PostgreSQL, FastAPI, Pydantic v2, SQLAlchemy async, React 18, TypeScript, Vite, Vitest, pytest, `uv`, `pnpm`, Docker Compose.

---

## Human-Owned Checkpoints

This refactor is intentionally schema- and data-destructive for local/demo data. Do not run destructive commands until the user confirms the checkpoint during implementation.

- User-owned destructive reset checkpoint:

```powershell
docker compose --env-file infra/compose/.env.example -f infra/compose/compose.yaml -f infra/compose/compose.override.yaml down -v
```

Expected result: the local Compose stack stops and named volumes for the local demo database/object storage are removed. The user should report whether the command completed successfully or paste the error.

- User-owned local stack startup after implementation:

```powershell
.\infra\compose\Start-Local.ps1 -ForceRecreate
```

Expected result: the stack starts, migrations run from a clean database, and the script prints the management URL.

- User-owned demo seed after implementation:

```powershell
.\infra\compose\Seed-LocalDemoData.ps1
```

Expected result: the approved hierarchy, groups, users, and seed documents are present. The script prints the seeded credentials.

- User-owned browser acceptance after agent verification:
  - Log into `https://manage.localhost` as `admin@admin.com`.
  - Confirm the organizational-unit tree, group ownership, user node/role/group assignments, and document access rules are visible.
  - Log into the seeded non-admin users and verify the acceptance matrix in Manage, Chat, and Docs.

Agent-owned work is limited to code, tests, migrations, seed scripts, context updates, and non-destructive verification commands.

## File And Responsibility Map

### .NET App Schema And Authorization

- Modify: `services/dotnet-api/src/AdvancedRag.Infrastructure/Persistence/Entities.cs`
- Modify: `services/dotnet-api/src/AdvancedRag.Infrastructure/Persistence/AppDbContext.cs`
- Add: EF migration under `services/dotnet-api/src/AdvancedRag.Infrastructure/Migrations/`
- Modify: `services/dotnet-api/src/AdvancedRag.App/Auth/AccessScopeHash.cs`
- Add: `services/dotnet-api/src/AdvancedRag.App/Auth/EffectiveAccessScope*.cs`
- Add: `services/dotnet-api/src/AdvancedRag.Infrastructure/Auth/EfEffectiveAccessScopeRepository.cs`
- Modify: `.NET` DI registration in `services/dotnet-api/src/AdvancedRag.Api/Program.cs`

### .NET Users, Groups, Organizational Units

- Modify: `services/dotnet-api/src/AdvancedRag.App/Users/UserAdministrationTypes.cs`
- Modify: `services/dotnet-api/src/AdvancedRag.App/Users/UserAdministrationService.cs`
- Modify: `services/dotnet-api/src/AdvancedRag.Infrastructure/Users/EfUserAdministrationRepository.cs`
- Modify: `services/dotnet-api/src/AdvancedRag.Api/Controllers/UsersController.cs`
- Modify: `services/dotnet-api/src/AdvancedRag.Api/Controllers/GroupsController.cs`
- Add: `services/dotnet-api/src/AdvancedRag.Api/Controllers/OrganizationalUnitsController.cs`
- Add/modify API models under `services/dotnet-api/src/AdvancedRag.Api/Models/Users`, `Groups`, and `OrganizationalUnits`

### .NET Documents And Viewer

- Modify: `services/dotnet-api/src/AdvancedRag.App/Documents/DocumentLifecycleTypes.cs`
- Modify: `services/dotnet-api/src/AdvancedRag.App/Documents/DocumentLifecycleService.cs`
- Modify: `services/dotnet-api/src/AdvancedRag.Infrastructure/Documents/EfDocumentRepository.cs`
- Modify: `services/dotnet-api/src/AdvancedRag.Api/Models/Documents/DocumentRequests.cs`
- Modify: `services/dotnet-api/src/AdvancedRag.Api/Models/Documents/DocumentResponses.cs`
- Modify: `services/dotnet-api/src/AdvancedRag.Api/Controllers/DocumentsController.cs`
- Modify: `services/dotnet-api/src/AdvancedRag.App/Viewer/ViewerAccessService.cs`
- Modify: `services/dotnet-api/src/AdvancedRag.App/Viewer/ViewerDocumentCatalogService.cs`
- Modify: `services/dotnet-api/src/AdvancedRag.Infrastructure/Viewer/EfViewerAccessRepository.cs`

### FastAPI RAG

- Modify: `services/rag-api/src/advanced_rag/auth/session_validation.py`
- Modify: `services/rag-api/src/advanced_rag/auth/chat_tokens.py` only if legacy test helpers still need claim-shape compatibility.
- Modify: `services/rag-api/src/advanced_rag/rag/hybrid_retrieval.py`
- Modify: `services/rag-api/src/advanced_rag/rag/chat_service.py`
- Modify tests under `services/rag-api/tests/`

### Management Frontend

- Modify: `apps/manage-web/src/api/users.ts`
- Add: `apps/manage-web/src/api/orgUnits.ts`
- Modify: `apps/manage-web/src/api/documents.ts`
- Modify: `apps/manage-web/src/features/users/UsersBudgetPage.tsx`
- Modify: `apps/manage-web/src/features/documents/DocumentsPage.tsx`
- Modify: `apps/manage-web/src/App.test.tsx`
- Modify i18n strings where these surfaces define Spanish/English UI copy.

### Demo And Context

- Modify: `infra/compose/Seed-LocalDemoData.ps1`
- Modify: `context/architecture.md`
- Modify: `context/rag-spec.md`
- Modify: `context/ui-context.md`
- Modify: `context/progress-tracker.md`
- Modify: `context/design-decisions.md` only for implementation-level decisions that differ from the approved design.

## Target Data Model

The implementation should keep EF migration history append-only, but it does not need to preserve existing local/demo rows.

```sql
app.organizational_units (
  "Id" uuid primary key,
  name text not null,
  parent_id uuid null references app.organizational_units("Id"),
  is_active boolean not null default true,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
)

app.organizational_unit_closure (
  ancestor_id uuid not null references app.organizational_units("Id") on delete cascade,
  descendant_id uuid not null references app.organizational_units("Id") on delete cascade,
  depth integer not null,
  primary key (ancestor_id, descendant_id)
)

app.users.organizational_unit_id uuid not null references app.organizational_units("Id")
app.users.access_scope_version bigint not null default 1

app.groups.owner_organizational_unit_id uuid null references app.organizational_units("Id")
app.groups.publishing_policy text not null default 'OwnerScope'

app.user_group_publish_grants (
  user_id uuid not null references app.users("Id") on delete cascade,
  group_id uuid not null references app.groups("Id") on delete cascade,
  granted_by_user_id uuid not null references app.users("Id"),
  created_at timestamptz not null default now(),
  primary key (user_id, group_id)
)

app.document_permissions (
  "Id" uuid primary key,
  document_id uuid not null references app.documents("Id") on delete cascade,
  organizational_unit_id uuid null references app.organizational_units("Id"),
  attribute_key text null,
  attribute_value text null,
  created_at timestamptz not null default now()
)

app.document_permission_groups (
  document_permission_id uuid not null references app.document_permissions("Id") on delete cascade,
  group_id uuid not null references app.groups("Id") on delete cascade,
  primary key (document_permission_id, group_id)
)
```

`publishing_policy` values:

- `OwnerScope`: non-admin publishers may use the group when the group's owner organizational unit is inside the publisher's management scope.
- `ExplicitGrantOnly`: non-admin publishers may use the group only if `app.user_group_publish_grants` grants that user access.
- `AdminOnly`: only `Admin` may use the group in document access rules.

Empty document rules must be rejected in application validation and ignored defensively in RAG SQL.

## Task 1: Schema, Roles, And Seed Baseline

**Files:**
- `services/dotnet-api/src/AdvancedRag.Infrastructure/Persistence/Entities.cs`
- `services/dotnet-api/src/AdvancedRag.Infrastructure/Persistence/AppDbContext.cs`
- EF migration files
- `services/dotnet-api/tests/AdvancedRag.Infrastructure.Tests/AppDbContextMappingTests.cs`
- `services/dotnet-api/tests/AdvancedRag.Infrastructure.Tests/AppDbContextMigrationTests.cs`
- `services/dotnet-api/tests/AdvancedRag.Api.Tests/SetupEndpointTests.cs`
- `infra/compose/Seed-LocalDemoData.ps1`

- [ ] **Step 1: Write failing schema and bootstrap tests**

Add tests proving:

```csharp
[Fact]
public async Task AppDbContext_MapsOrganizationalUnitsClosureAndAccessRules()
{
    // Assert org units, closure rows, users.organizational_unit_id,
    // groups.publishing_policy, document_permission_groups, and grants persist.
}
```

```csharp
[Fact]
public async Task CleanBootstrap_SeedsHierarchicalRolesAndRootAdmin()
{
    // Assert roles include Admin, DocumentEditor, DocumentPublisher, Viewer.
    // Assert admin@admin.com is active, assigned to Empresa, and has Admin.
}
```

Run:

```powershell
dotnet test services\dotnet-api\AdvancedRag.sln --filter "FullyQualifiedName~AppDbContextMappingTests|FullyQualifiedName~AppDbContextMigrationTests|FullyQualifiedName~SetupEndpointTests"
```

Expected: failures because the hierarchy tables, role set, and mappings do not exist.

- [ ] **Step 2: Implement EF schema**

Implement:

- `OrganizationalUnit`, `OrganizationalUnitClosure`, `DocumentPermissionGroup`, and `UserGroupPublishGrant` entities.
- `User.OrganizationalUnitId` and `User.AccessScopeVersion`.
- `Group.OwnerOrganizationalUnitId` and `Group.PublishingPolicy`.
- `DocumentPermission.OrganizationalUnitId`.
- Indexes for:
  - `organizational_units.parent_id`
  - `organizational_unit_closure.descendant_id`
  - `users.organizational_unit_id`
  - `groups.owner_organizational_unit_id`
  - `document_permissions.document_id`
  - `document_permissions.organizational_unit_id`
  - `document_permission_groups.group_id`
- A root `Empresa` row and closure self-row in migration seed SQL.
- Roles `Admin`, `DocumentEditor`, `DocumentPublisher`, and `Viewer`.
- Table-level `GRANT SELECT` from `app.document_permissions`, `app.document_permission_groups`, `app.organizational_units`, `app.organizational_unit_closure`, `app.groups`, and `app.user_ai_budget_limits` to `rag_owner`.

- [ ] **Step 3: Update local demo seed**

Update `infra/compose/Seed-LocalDemoData.ps1` to seed the approved dataset:

- Org tree: `Empresa`, `Comunicación`, `Marketing`, `Producción Audiovisual`, `Operaciones`, `Recursos Humanos`, `Sistemas`, `Finanzas`.
- Groups: `Gerentes`, `Comité de crisis`, `Liderazgo`, and optional `Demo viewers`.
- Users: `admin@admin.com`, `manager.comunicacion@demo.com`, `viewer.marketing@demo.com`, `viewer.audiovisual@demo.com`, `viewer.sistemas@demo.com`, `crisis.comunicacion@demo.com`.
- Published seed documents and access rules from the approved acceptance matrix.
- Deterministic UUIDs so tests and browser acceptance can refer to stable rows.

Keep the script idempotent on a clean database and destructive only for its own seed rows.

- [ ] **Step 4: Verify schema green**

Run:

```powershell
dotnet test services\dotnet-api\AdvancedRag.sln --filter "FullyQualifiedName~AppDbContextMappingTests|FullyQualifiedName~AppDbContextMigrationTests|FullyQualifiedName~SetupEndpointTests"
dotnet build services\dotnet-api\AdvancedRag.sln --no-restore
```

Expected: focused schema/setup tests and .NET build pass.

## Task 2: Effective Access Scope Contract

**Files:**
- `services/dotnet-api/src/AdvancedRag.App/Auth/AccessScopeHash.cs`
- New `.NET` effective-scope app/infrastructure files
- `services/dotnet-api/src/AdvancedRag.Api/Controllers/InternalSessionController.cs`
- `services/dotnet-api/src/AdvancedRag.Api/Models/Auth/InternalSessionValidationResponse.cs`
- `services/rag-api/src/advanced_rag/auth/session_validation.py`
- `services/rag-api/src/advanced_rag/auth/chat_tokens.py`
- Tests in `.NET` auth/API and FastAPI session validation

- [x] **Step 1: Write failing scope contract tests**

Add tests proving:

```csharp
[Fact]
public void AccessScopeHash_V2_IncludesRoleAdminFlagOrgUnitGroupsAndVersion()
{
    // Fixed vector test with expected SHA-256 digest.
}
```

```csharp
[Fact]
public async Task InternalSessionValidation_ReturnsHierarchicalScopeClaims()
{
    // Assert userId, role, isGlobalAdmin, organizationalUnitId,
    // groups, accessScopeVersion, accessScopeHash, corpus.
}
```

```python
async def test_dotnet_session_validator_fetches_access_claims_every_request() -> None:
    # Same session cookie, two requests, two .NET calls.
    # This proves user node/group changes can apply on the next request.
```

Expected: failures because claim shape and cache behavior are still legacy.

- [x] **Step 2: Implement `.NET` effective scope**

Implement an app-layer contract like:

```csharp
public sealed record EffectiveAccessScope(
    Guid UserId,
    string PrimaryRole,
    bool IsGlobalAdmin,
    Guid OrganizationalUnitId,
    IReadOnlyList<Guid> GroupIds,
    long AccessScopeVersion,
    string Corpus);
```

Rules:

- `Admin` sets `IsGlobalAdmin = true`.
- `DocumentPublisher` and `DocumentEditor` are non-admin scoped management roles.
- `DocumentPublisher` implies editor capabilities.
- `Viewer` has no document-management authority.
- `AccessScopeHash.ComputeV2(...)` includes:
  - `v = 2`
  - primary role
  - `isGlobalAdmin`
  - organizational unit id
  - sorted group ids
  - access scope version
- Increment `users.access_scope_version` on role, group, organizational-unit, and active-status changes that affect authorization.

- [x] **Step 3: Implement FastAPI claim shape and freshness**

Update FastAPI to:

- Parse `isGlobalAdmin`, `organizationalUnitId`, and `accessScopeVersion`.
- Stop caching access-relevant claims across requests. The simplest compliant implementation is to fetch `.NET` validation for every chat/docs API request that uses `DotnetSessionValidator`.
- Keep raw session cookies out of logs.
- Preserve `access_scope_hash` as the semantic-cache/audit partition key.

- [x] **Step 4: Verify contract green**

Run:

```powershell
dotnet test services\dotnet-api\AdvancedRag.sln --filter "FullyQualifiedName~AuthEndpointTests|FullyQualifiedName~UserAdministrationEndpointTests"
Set-Location services\rag-api
uv run pytest tests/test_session_validation.py tests/test_auth_tokens.py -q
Set-Location ..\..
```

Expected: `.NET` and FastAPI agree on the new access-scope contract and FastAPI no longer reuses stale access claims.

## Task 3: Central Document Access Authorization

**Files:**
- `services/dotnet-api/src/AdvancedRag.App/Documents/DocumentLifecycleTypes.cs`
- `services/dotnet-api/src/AdvancedRag.App/Documents/DocumentLifecycleService.cs`
- `services/dotnet-api/src/AdvancedRag.Infrastructure/Documents/EfDocumentRepository.cs`
- New app-layer authorization service/repository types
- Tests in `DocumentLifecycleServiceTests`, `ViewerAccessServiceTests`, `ViewerDocumentCatalogServiceTests`

- [ ] **Step 1: Write failing acceptance-matrix service tests**

Add tests proving:

```csharp
[Theory]
[InlineData("viewer.marketing@demo.com", "Calendario de campañas de Marketing", true)]
[InlineData("viewer.marketing@demo.com", "Checklist de producción audiovisual", false)]
[InlineData("viewer.sistemas@demo.com", "Guía del área Comunicación", false)]
public async Task BranchScopedReadRules_MatchOnlyExpectedDocuments(...)
{
}
```

```csharp
[Fact]
public async Task Publisher_CanUseOwnedGroupButCannotUseGlobalGroupWithoutGrant()
{
    // Comunicación publisher can use Comité de crisis.
    // Same user cannot use Gerentes until an explicit grant exists.
}
```

Expected: failures because current services only know group ids and `DocumentManager`.

- [ ] **Step 2: Replace group-id-only document DTOs in the app layer**

Introduce records similar to:

```csharp
public sealed record DocumentAccessRuleDraft(
    Guid? OrganizationalUnitId,
    IReadOnlyList<Guid> GroupIds);

public sealed record DocumentAccessRuleRecord(
    Guid Id,
    Guid? OrganizationalUnitId,
    IReadOnlyList<Guid> GroupIds);
```

Replace `AllowedGroupIds` in document commands, summaries, and aggregates with access-rule records.

- [ ] **Step 3: Implement document read and management policy**

Implement one central app-layer policy service with these operations:

- `CanReadPublishedDocument(scope, rules)`
- `CanManageDraft(scope, rules)`
- `CanPublish(scope, rules)`
- `CanUseRuleForPublishing(scope, rule)`
- `CanCreateInOrganizationalUnit(scope, organizationalUnitId)`

Rules:

- `Admin` returns true for every operation.
- Non-admin read rules:
  - Root org rule matches all users.
  - Non-root org rule matches when user unit and document unit are ancestor/descendant.
  - Group condition matches if the user has any group listed by that rule.
  - A rule with org and groups requires both conditions.
  - A rule with neither org nor group is invalid and must not match.
- Non-admin management rules:
  - `DocumentEditor` can create/edit/send/return within own org unit plus descendants.
  - `DocumentPublisher` can do editor actions plus publish/archive/restore within own org unit plus descendants.
  - Group membership alone does not grant management authority.
  - Group use during publishing follows group ownership policy and explicit grants.

- [ ] **Step 4: Update document lifecycle**

Update create, update draft, send to review, return to draft, publish, archive, restore, and image upload/viewer link checks to use the central policy.

Keep indexing behavior unchanged: publication still requires successful synchronous indexing before the document becomes `Published`.

- [ ] **Step 5: Verify document authorization green**

Run:

```powershell
dotnet test services\dotnet-api\AdvancedRag.sln --filter "FullyQualifiedName~DocumentLifecycleServiceTests|FullyQualifiedName~ViewerAccessServiceTests|FullyQualifiedName~ViewerDocumentCatalogServiceTests"
```

Expected: all document and viewer policy tests pass.

## Task 4: .NET API Contracts For Org Units, Users, Groups, Documents

**Files:**
- `services/dotnet-api/src/AdvancedRag.Api/Controllers/UsersController.cs`
- `services/dotnet-api/src/AdvancedRag.Api/Controllers/GroupsController.cs`
- `services/dotnet-api/src/AdvancedRag.Api/Controllers/DocumentsController.cs`
- Add `OrganizationalUnitsController`
- API model files under `Models`
- `services/dotnet-api/tests/AdvancedRag.Api.Tests/UserAdministrationEndpointTests.cs`
- Add/modify document endpoint tests

- [x] **Step 1: Write failing API tests**

Add endpoint tests proving:

- `GET /api/organizational-units` returns the active tree.
- `POST /api/organizational-units` creates a child node when the actor is `Admin`.
- `PATCH /api/organizational-units/{id}` renames/deactivates a node.
- Branch moves are not exposed in this slice.
- `POST /api/users` requires exactly one `organizationalUnitId`.
- `PUT /api/users/{id}/roles` accepts `Viewer`, `DocumentEditor`, `DocumentPublisher`, or `Admin`.
- `PUT /api/users/{id}/groups` increments `accessScopeVersion`.
- Non-admin publishers cannot assign `Admin` or broaden themselves.
- Document create/update rejects empty access rules.
- Document create/update rejects `Gerentes` for the Comunicación publisher without an explicit grant.
- Document create/update allows `Comité de crisis` for the Comunicación publisher.
- Publish as `manager.comunicacion@demo.com` succeeds only inside Comunicación, Marketing, and Producción Audiovisual.

- [x] **Step 2: Implement DTOs**

Representative request/response shape:

```json
{
  "title": "Protocolo de comunicación en crisis",
  "documentType": "Procedimiento",
  "audience": "Comunicación",
  "contentHtml": "<p>...</p>",
  "accessRules": [
    {
      "organizationalUnitId": "comunicacion-id",
      "groupIds": ["comite-crisis-id"]
    }
  ]
}
```

```json
{
  "id": "user-id",
  "email": "manager.comunicacion@demo.com",
  "roles": ["DocumentPublisher"],
  "organizationalUnit": { "id": "comunicacion-id", "name": "Comunicación" },
  "groups": [
    { "id": "gerentes-id", "name": "Gerentes", "publishingPolicy": "ExplicitGrantOnly" }
  ],
  "accessScopeHash": "..."
}
```

- [x] **Step 3: Implement controllers and services**

Authorization:

- `Admin` can manage org units, roles, users, groups, grants, and all documents.
- `DocumentPublisher` and `DocumentEditor` can list enough users/groups/org units to operate within their scope, but cannot modify Admin-only fields.
- Existing `DocumentManager` API permissions should be removed or treated as legacy-denied. Do not keep it as an active product role.

- [x] **Step 4: Verify API green**

Run:

```powershell
dotnet test services\dotnet-api\AdvancedRag.sln --filter "FullyQualifiedName~UserAdministrationEndpointTests|FullyQualifiedName~Document"
dotnet build services\dotnet-api\AdvancedRag.sln --no-restore
```

Expected: endpoint tests and .NET build pass.

## Task 5: FastAPI RAG Branch-Aware Filtering

**Files:**
- `services/rag-api/src/advanced_rag/rag/hybrid_retrieval.py`
- `services/rag-api/src/advanced_rag/rag/chat_service.py`
- `services/rag-api/tests/test_chat_rag.py`
- `services/rag-api/tests/test_migrations.py`

- [ ] **Step 1: Write failing RAG access tests**

Update the PostgreSQL fixture in `test_chat_rag.py` to create:

- `app.organizational_units`
- `app.organizational_unit_closure`
- `app.groups`
- `app.document_permissions`
- `app.document_permission_groups`

Add tests proving:

```python
async def test_hierarchical_retrieval_allows_ancestor_descendant_but_not_sibling_documents() -> None:
    # Marketing user can retrieve Empresa, Comunicación, and Marketing.
    # Marketing user cannot retrieve Producción Audiovisual or Sistemas.
```

```python
async def test_group_rule_requires_membership_and_empty_rule_does_not_match() -> None:
    # Crisis group user retrieves Comunicación + Comité de crisis.
    # Same org without group cannot retrieve it.
    # Empty document rule does not grant access.
```

```python
async def test_admin_global_scope_bypasses_rule_filter() -> None:
    # Admin claim retrieves all published chunks.
```

- [ ] **Step 2: Implement branch-aware SQL**

Replace group-only EXISTS clauses with rule matching equivalent to:

```sql
AND (
  CAST(:is_global_admin AS boolean)
  OR EXISTS (
    SELECT 1
    FROM app.document_permissions p
    WHERE p.document_id = chunk.document_id
      AND (
        p.organizational_unit_id IS NOT NULL
        OR EXISTS (
          SELECT 1
          FROM app.document_permission_groups pg
          WHERE pg.document_permission_id = p."Id"
        )
      )
      AND (
        p.organizational_unit_id IS NULL
        OR p.organizational_unit_id = :root_organizational_unit_id
        OR EXISTS (
          SELECT 1
          FROM app.organizational_unit_closure c
          WHERE
            (c.ancestor_id = p.organizational_unit_id AND c.descendant_id = :user_organizational_unit_id)
            OR
            (c.ancestor_id = :user_organizational_unit_id AND c.descendant_id = p.organizational_unit_id)
        )
      )
      AND (
        NOT EXISTS (
          SELECT 1
          FROM app.document_permission_groups pg
          WHERE pg.document_permission_id = p."Id"
        )
        OR EXISTS (
          SELECT 1
          FROM app.document_permission_groups pg
          WHERE pg.document_permission_id = p."Id"
            AND pg.group_id = ANY(CAST(:user_groups AS uuid[]))
        )
      )
  )
)
```

Pass `is_global_admin`, `user_organizational_unit_id`, `root_organizational_unit_id`, and `user_groups` from `ChatTokenClaims`.

- [ ] **Step 3: Keep semantic cache partitioning strict**

Ensure:

- `access_scope_hash` v2 is used in cache lookup/write and audit.
- Existing v1 cache entries cannot match v2 scopes because hashes differ.
- Cache source invalidation remains document-id based.

- [ ] **Step 4: Verify FastAPI green**

Run:

```powershell
Set-Location services\rag-api
uv run pytest tests/test_chat_rag.py tests/test_session_validation.py tests/test_migrations.py -q
uv run ruff check .
Set-Location ..\..
```

Expected: RAG access tests pass and lint is clean.

## Task 6: Management UI For Hierarchy, Groups, Users, And Document Rules

**Files:**
- `apps/manage-web/src/api/users.ts`
- `apps/manage-web/src/api/orgUnits.ts`
- `apps/manage-web/src/api/documents.ts`
- `apps/manage-web/src/features/users/UsersBudgetPage.tsx`
- `apps/manage-web/src/features/documents/DocumentsPage.tsx`
- `apps/manage-web/src/App.test.tsx`
- i18n string files if present in the app

- [ ] **Step 1: Write failing frontend tests**

Add tests proving:

```ts
test('creates a user with one organizational unit and multiple groups', async () => {
  // Assert POST /api/users body has organizationalUnitId and groupIds.
})
```

```ts
test('document editor saves organizational-unit and group access rules', async () => {
  // Assert body.accessRules contains Comunicación + Comité de crisis.
})
```

```ts
test('document editor surfaces invalid empty access rule errors', async () => {
  // Backend returns VALIDATION_FAILED for accessRules.
  // UI shows Spanish field-level/server error.
})
```

Expected: failures because the UI still uses `allowedGroupIds`.

- [ ] **Step 2: Update API clients**

Replace document `allowedGroupIds` types with `accessRules`.

Add:

```ts
export interface OrganizationalUnitSummary {
  id: string
  name: string
  parentId: string | null
  depth: number
  isActive: boolean
}
```

Extend users/groups:

- Users expose `organizationalUnit`.
- Create/update user accepts `organizationalUnitId`.
- Groups expose `ownerOrganizationalUnit`, `publishingPolicy`, and optional grant metadata.

- [ ] **Step 3: Update users/groups workspace**

UI requirements:

- Add an organizational-unit selector to create/edit user forms.
- Replace the single `DocumentManager` option with `DocumentEditor` and `DocumentPublisher`.
- Keep `Admin` visibly global/unrestricted.
- Show group ownership and publishing policy in the groups tab.
- Keep UI strings Spanish by default.
- Keep the screen dense and operational, not a landing page or explanatory page.

- [ ] **Step 4: Update documents workspace**

UI requirements:

- Replace "access groups" with access rules.
- Each rule allows one organizational unit and zero/more groups, or no org unit and one/more groups.
- Empty rule is invalid before submit and must also surface backend validation.
- Use the org tree and group list to build rules.
- Make root `Empresa` visually clear as company-wide.
- Hide or disable publishing actions when the user lacks scoped publisher authority.

- [ ] **Step 5: Verify frontend green**

Run:

```powershell
pnpm.cmd --dir apps\manage-web test -- --run src/App.test.tsx
pnpm.cmd --dir apps\manage-web typecheck
pnpm.cmd --dir apps\manage-web build
```

Expected: focused manage-web tests, typecheck, and build pass.

## Task 7: Cross-Surface Acceptance And Data Reset

This task starts only after the user-owned destructive reset checkpoint is approved and run.

- [ ] **Step 1: User runs destructive reset**

User command:

```powershell
docker compose --env-file infra/compose/.env.example -f infra/compose/compose.yaml -f infra/compose/compose.override.yaml down -v
```

Expected: local volumes are removed. Stop if this fails.

- [ ] **Step 2: Agent verifies clean Compose config**

Run:

```powershell
docker compose --env-file infra/compose/.env.example -f infra/compose/compose.yaml -f infra/compose/compose.override.yaml config
```

Expected: Compose config renders successfully.

- [ ] **Step 3: User starts stack and seeds demo**

User commands:

```powershell
.\infra\compose\Start-Local.ps1 -ForceRecreate
.\infra\compose\Seed-LocalDemoData.ps1
```

Expected: clean migrations run, demo hierarchy is seeded, and credentials are printed.

- [ ] **Step 4: Agent runs database smoke checks**

Run non-destructive selects through Compose after user confirms stack is up:

```powershell
docker compose --env-file infra/compose/.env.example -f infra/compose/compose.yaml -f infra/compose/compose.override.yaml exec -T postgres psql -U postgres -d advanced_rag -c "select name, parent_id from app.organizational_units order by name;"
```

Expected: approved org units are present.

- [ ] **Step 5: User runs browser acceptance**

Acceptance matrix:

- `admin@admin.com`: sees and manages everything.
- `manager.comunicacion@demo.com`: sees company-wide, Comunicación, Marketing, Producción Audiovisual, and crisis; cannot see Sistemas; can manage/publish in Comunicación branch; cannot publish Empresa/Sistemas/Finanzas; cannot use Gerentes without explicit grant.
- `viewer.marketing@demo.com`: sees company-wide, Comunicación, Marketing only.
- `viewer.audiovisual@demo.com`: sees company-wide, Comunicación, Producción Audiovisual only.
- `viewer.sistemas@demo.com`: sees company-wide and Sistemas only.
- `crisis.comunicacion@demo.com`: sees company-wide, Comunicación, Marketing, and crisis only.
- User node/group changes affect Chat/Docs on the next request.

## Task 8: Full Verification And Context Updates

**Files:**
- `context/architecture.md`
- `context/rag-spec.md`
- `context/ui-context.md`
- `context/progress-tracker.md`
- `context/design-decisions.md`

- [ ] **Step 1: Run focused full-stack verification**

Run:

```powershell
dotnet test services\dotnet-api\AdvancedRag.sln
Set-Location services\rag-api
uv run pytest -q
uv run ruff check .
Set-Location ..\..
pnpm.cmd --dir apps\manage-web typecheck
pnpm.cmd --dir apps\manage-web test -- --run src/App.test.tsx
pnpm.cmd --dir apps\manage-web build
pnpm.cmd --dir apps\chat-web typecheck
pnpm.cmd --dir apps\docs-web typecheck
git diff --check
```

Expected: all targeted suites pass. `git diff --check` may show Windows line-ending warnings only; any real whitespace errors must be fixed.

- [ ] **Step 2: Update context**

Record:

- Hierarchical access is implemented, not merely planned.
- Active role set is `Admin`, `DocumentEditor`, `DocumentPublisher`, `Viewer`.
- FastAPI session claim validation is access-fresh on every request.
- RAG retrieval reads organizational-unit closure and document permission group joins.
- Browser/Compose acceptance status, including any gaps.

- [ ] **Step 3: Commit checkpoint**

After all agent-owned verification passes and the user-owned reset/browser checkpoint status is documented, create a scoped commit if the user wants the branch committed:

```powershell
git status --short
git add services/dotnet-api services/rag-api apps/manage-web infra/compose context docs/superpowers/plans
git commit -m "Implement hierarchical document access"
```

Expected: one coherent refactor commit, with unrelated user changes left untouched.

## Implementation Notes

- Prefer one central `.NET` access policy service over scattered controller checks.
- Keep FastAPI read-only against the `app` schema.
- Do not implement branch moves in this slice.
- Do not add per-user document ACL exceptions.
- Do not preserve `DocumentManager` as an active role unless a compatibility decision is explicitly made later.
- Do not make empty access rules mean "all users"; root `Empresa` is the explicit all-company rule.
- Keep all end-user UI strings in Spanish (`es-AR`) with English translation parity where the app already supports it.
- Keep the management UI dense and operational. Avoid explanatory cards and marketing-style redesign.
