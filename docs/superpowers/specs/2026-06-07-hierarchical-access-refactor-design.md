# Hierarchical Access Refactor Design

Date: 2026-06-07
Status: Approved design, implementation plan written

## Context

The current implementation uses flat groups/departments for document visibility. That works for small datasets but becomes hard to administer in larger companies because managers need predictable access to their area and subareas, while cross-cutting audiences such as crisis committees or leadership groups still need to exist outside the strict tree.

External product research found similar patterns in Google Workspace organizational units plus access groups, Google Cloud IAM hierarchy, GitLab group/subgroup inheritance, Microsoft Entra administrative units, SharePoint permission inheritance, AWS Organizations OUs/SCPs, and NIST role hierarchy guidance. The approved direction is a hybrid model: organizational units for the company hierarchy plus transverse groups for exceptions and cross-cutting audiences.

## Scope

This design covers the access model, hierarchy storage direction, scoped document-management permissions, seed reset dataset, and acceptance matrix for the first implementation plan.

This design does not implement schema, migrations, API changes, RAG SQL changes, UI changes, or data deletion. The existing development/demo data may be reset later, but only after an explicit user-owned destructive-data checkpoint.

## Core Model

`Admin` remains a global unrestricted system role. It can manage users, hierarchy, groups, documents, budgets, configuration, audit, chat retrieval, and document viewing across every organizational unit and group.

Every non-admin user has exactly one primary organizational-unit assignment and zero or more group assignments.

Organizational units are stored with a closure table:

- `organizational_units` stores each node and its direct parent.
- `organizational_unit_closure` stores `(ancestor, descendant, depth)` rows.
- Branch moves are deferred out of the first slice.

The first hierarchy-management slice allows creating, renaming, and deactivating organizational-unit nodes. Moving branches requires a later workflow with impact preview and stronger audit.

## Read Authorization

Root organizational-unit rules are global. A document assigned to `Empresa` is visible to all users.

Non-root organizational-unit read access is branch-scoped:

- A document published at an ancestor unit is visible to descendant users.
- A user assigned to a parent unit can read documents assigned to descendant units.
- Sibling branches do not match.

Groups are evaluated as an additional access dimension. Document rules use AND inside one rule and OR across rules:

- A document is visible if at least one rule matches.
- Every configured dimension in a matching rule must be satisfied.
- When a rule lists multiple groups, group matching is any-of.
- A rule with no organizational unit and no group is invalid.

## Document Management Authorization

Non-admin document management is split into scoped permissions instead of using the current all-purpose `DocumentManager` role:

- `DocumentEditor`: can create, import, edit, update metadata, send to review, and return/reject draft or in-review documents within scope.
- `DocumentPublisher`: includes `DocumentEditor` and can publish, archive, and restore documents within scope.

The normal management scope is the user's assigned organizational unit plus descendants.

Groups do not grant publication authority by themselves. Each transverse group has an ownership scope:

- Global.
- Or tied to one organizational-unit node.

A non-admin publisher may attach a group to a document rule only when:

- The group owner's organizational unit is inside the publisher's management scope.
- Or an explicit publisher-to-group usage grant allows that publisher to use the group.

Global groups require `Admin` or an explicit usage grant. They are not automatically available to every non-admin publisher.

## Fresh Seed Dataset

The approved seed hierarchy is:

```text
Empresa
+- Comunicación
|  +- Marketing
|  +- Producción Audiovisual
+- Operaciones
+- Recursos Humanos
+- Sistemas
+- Finanzas
```

Approved seed transverse groups:

| Group | Ownership scope |
| --- | --- |
| `Gerentes` | Global; publishing use only through explicit grants. |
| `Comité de crisis` | Owned by `Comunicación`. |
| `Liderazgo` | Global; admin-only by default. |
| `Demo viewers` | Global technical group if still needed for tests. |

Approved seed users:

| User | Role/permission | Node | Groups |
| --- | --- | --- | --- |
| `admin@admin.com` | `Admin` | `Empresa` | `Liderazgo` |
| `manager.comunicacion@demo.com` | `DocumentPublisher` | `Comunicación` | `Gerentes`, `Comité de crisis` |
| `viewer.marketing@demo.com` | `Viewer` | `Marketing` | none |
| `viewer.audiovisual@demo.com` | `Viewer` | `Producción Audiovisual` | none |
| `viewer.sistemas@demo.com` | `Viewer` | `Sistemas` | none |
| `crisis.comunicacion@demo.com` | `Viewer` | `Marketing` | `Comité de crisis` |

Approved seed documents:

| Document | Access rule |
| --- | --- |
| `Manual general de comunicación interna` | `Empresa` |
| `Guía del área Comunicación` | `Comunicación` |
| `Calendario de campañas de Marketing` | `Marketing` |
| `Checklist de producción audiovisual` | `Producción Audiovisual` |
| `Procedimiento de guardias de Sistemas` | `Sistemas` |
| `Protocolo de comunicación en crisis` | `Comunicación` + `Comité de crisis` |

## Acceptance Matrix

| User | Expected chat/docs visibility | Expected document-management authority |
| --- | --- | --- |
| `admin@admin.com` | All seed documents. | Full unrestricted management and publishing authority across every organizational unit and group. |
| `manager.comunicacion@demo.com` | Company-wide, `Comunicación`, `Marketing`, `Producción Audiovisual`, and crisis documents. Does not see unrelated `Sistemas` documents. | Can create, edit, review, publish, archive, and restore documents in `Comunicación`, `Marketing`, and `Producción Audiovisual`. Can use `Comité de crisis`. Cannot publish `Empresa`, `Sistemas`, `Finanzas`, or `Liderazgo` documents. Cannot use `Gerentes` for publishing unless an explicit grant is added. |
| `viewer.marketing@demo.com` | Company-wide, `Comunicación`, and `Marketing` documents. Does not see `Producción Audiovisual`, `Sistemas`, or crisis documents. | No document-management authority. |
| `viewer.audiovisual@demo.com` | Company-wide, `Comunicación`, and `Producción Audiovisual` documents. Does not see `Marketing`, `Sistemas`, or crisis documents. | No document-management authority. |
| `viewer.sistemas@demo.com` | Company-wide and `Sistemas` documents only. Does not see `Comunicación`, `Marketing`, `Producción Audiovisual`, or crisis documents. | No document-management authority. |
| `crisis.comunicacion@demo.com` | Company-wide, `Comunicación`, `Marketing`, and crisis documents. Does not see `Producción Audiovisual` or `Sistemas` documents. | No document-management authority. |

## Negative Cases

- Group membership alone does not grant publication authority.
- `Gerentes` cannot be used for publishing without an explicit usage grant.
- `Liderazgo` is admin-only by default.
- `viewer.marketing@demo.com` cannot see `Producción Audiovisual`, `Sistemas`, or crisis documents.
- `viewer.audiovisual@demo.com` cannot see `Marketing`, `Sistemas`, or crisis documents.
- `viewer.sistemas@demo.com` cannot see `Comunicación`, `Marketing`, `Producción Audiovisual`, or crisis documents.
- Empty document-access rules are invalid and must not grant all-user access.
- User organizational-unit moves and group changes must affect chat/docs authorization on the next request.

## RAG And Cache Implications

FastAPI must stop treating access-relevant session claims as safe for a stale 60-second cache window after `.NET` changes a user's effective scope. The refactor needs an access-scope version or equivalent freshness mechanism.

The `access_scope_hash` schema version must be bumped. `Admin` global access must be explicit in the scope/hash inputs so semantic cache partitioning remains auditable.

RAG permission SQL must use `app.organizational_unit_closure` through read-only grants. For non-admin, non-root rules, the document unit and user unit must be in the same ancestor/descendant branch. Sibling branches must not match.

## First Implementation Boundaries

Agent-owned implementation work after spec review:

- Add the app-schema hierarchy and permission model.
- Reset and reseed the development/demo dataset after the user-owned destructive-data checkpoint.
- Update .NET authorization, session validation, and management APIs.
- Update FastAPI RAG filtering and scope/hash contract.
- Update management UI for user node assignment, scoped document permissions, and document rule editing.
- Add focused backend, frontend, and RAG tests for the acceptance matrix.

User-owned checkpoint before destructive work:

- Confirm the local development/demo database can be reset.
- Run or approve the explicit reset command selected during implementation planning.

## Out Of Scope

- Migrating existing flat group/document data.
- Moving organizational-unit branches in the first slice.
- Multiple primary organizational units per user.
- Per-user document ACL exceptions.
- A separate `Reviewer` role.
- Broad checkbox-style permission editing for every possible action.
