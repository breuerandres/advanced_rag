# Documents Access-Rules UI — UX Improvements Handoff

Date: 2026-06-10 · Branch: `roles-refactor` · Status: first pass (items 1–3) and second pass (backlog items A–D below) implemented and verified; only item E remains, blocked on backend support.

This handoff continues the UX review of the document access-rules UI in `apps/manage-web` (org-unit + groups rules on `DocumentsPage.tsx`). It is a companion to `docs/plans/2026-06-10-access-model-management-ui.md` and to the access-model semantics in `context/architecture.md` (AND within a rule / OR between rules; within a rule, multiple groups match on ANY membership; unit matching is branch-scoped ancestor/descendant; root unit = company-wide).

## Completed in this session (uncommitted working-tree changes)

All in `apps/manage-web`:

1. **Documents list Access column resolves real unit names** (was: every unit-scoped rule rendered the misleading "Toda la empresa" label because the list never loaded the unit catalog).
   - `loadDocuments()` now includes `listOrganizationalUnits()` in its `Promise.all` and stores units in page state (sorted via `sortUnitsInTreeOrder`).
   - `displayAccessRules(...)` takes the unit catalog; `unitDisplayName(...)` maps root unit → `documents.rule_company_wide_label`, non-root → unit name, unresolved id → new key `documents.rule_unit_unavailable` (non-admin catalog omits inactive units, so ids may not resolve).
   - `DocumentEditor` now receives `organizationalUnits` as a prop from the page; its own lazy fetch, `orgUnitsError` state, and the `documents.organizational_units_load_error` rendering were removed (the i18n key still exists, now unused in the documents section; the users section has its own copy that is still used).
2. **Empty new rules show a neutral hint instead of an immediate red error.** Per-rule message renders as `muted-copy` hint (`documents.access_rule_empty_hint`) by default and switches to the red `documents.access_rule_empty_invalid` only after a failed validation attempt (`hasAccessRuleValidationError` = review validation message OR backend `VALIDATION_FAILED` message).
3. **Org-unit select shows hierarchy.** `organizationalUnitOptionLabel` indents non-root options with `"   ".repeat(unit.depth)` (NBSP escapes — do not replace with plain spaces; browsers collapse them in `<option>`). `sortUnitsInTreeOrder` flattens units depth-first with alphabetical siblings; units whose parent is missing from the list (inactive ancestor under a non-admin catalog) are appended at the end unindented.
4. **New test**: `App.test.tsx` → "documents access column shows the unit name for unit-scoped rules" (non-root rule renders "Comunicación", not the company-wide label). The fetch mock already serves `GET /api/organizational-units` out of band, so the extra page-level fetch does not disturb the ordered response queue.
5. **i18n keys added** (es-AR + en-US, `documents` section): `access_rule_empty_hint`, `rule_unit_unavailable`.

### Verification (2026-06-10)

- `pnpm.cmd --dir apps\manage-web typecheck` — clean.
- `pnpm.cmd --dir apps\manage-web test -- --run` — 48 tests, 2 failed: both are the pre-existing TipTap "Estilo de bloque" toolbar tests, confirmed failing on the clean tree via `git stash` before/after comparison. Everything else, including the new test, passes.
- `pnpm.cmd --dir apps\manage-web lint` — 5 problems, all pre-existing in untouched files (`ManagementNav.tsx`, `AuditPage.tsx`, `ConfigurationPage.tsx`, `OrganizationalUnitsPage.tsx`); touched files are clean.
- `graphify update .` run after edits.

## Completed in the second pass (2026-06-10, uncommitted working-tree changes)

All in `apps/manage-web`; `DocumentsPage.tsx` now has a dedicated `AccessRuleCard` component per rule.

### A. AND/OR semantics visible in the rule editor — done
- Live natural-language preview under each non-empty rule card (`rulePreview` + `formatDisjunction`; keys `rule_preview_company`, `rule_preview_company_groups`, `rule_preview_unit`, `rule_preview_unit_groups`, `rule_preview_groups`, joined with `rule_preview_or_word`). List formatting is manual ("A, B o C") to avoid `Intl.ListFormat` lib-target concerns.
- "Ó"/"OR" separator pill between rule cards (`access_rule_or`, `.access-rule-separator`, `aria-hidden` since the preview text carries the semantics).
- Branch hint under the unit select for non-root units with children: `rule_unit_branch_hint_one/_other` via `countDescendants` (parent-chain BFS over the loaded catalog).
- `access_rules_help` reworded in both locales to state OR-between-rules, AND-within-rule, and ANY-of-the-groups explicitly.

### B. Unit/group list filters and search — done
- "Con grupos / Sin grupos" replaced by a unit filter (tree-ordered, indented options) and a group filter (`filter_unit` / `filter_group`); group filter also matches legacy `allowedGroupIds`. The unit filter matches rules referencing the selected unit id exactly (not branch-expanded — keep it explainable; revisit if admins ask for "what does unit X see" semantics, which needs closure data).
- Search text now includes resolved rule unit names and rule group names. `with_groups`/`without_groups` i18n keys removed.

### C. Scalable groups picker — done (minimal variant)
- Per rule: a "Filtrar grupos" search input plus a "{n} grupos seleccionados" counter (`rule_groups_filter`, `rule_groups_selected_one/_other`, `rule_groups_no_matches`). Checkbox grid stays (accessible, no new library). A combobox-with-chips upgrade remains optional future polish.

### D. Small behavior fixes — done
- "Nuevo grupo" moved into each rule card; the created group is added to that rule (`groupDialogRuleKey` replaces the open-dialog boolean).
- Unresolved unit ids (e.g. deactivated units hidden from non-admins) render as a pinned "Unidad no disponible" option so saving no longer silently drops them.
- Duplicate rules get a warning (`access_rule_duplicate_warning`, signature = unit id + sorted group ids); a company-wide root rule with no groups alongside other rules shows `access_rules_company_redundant`. New `.status-message.warning` style (light + dark).
- Rules keyed by stable `key` (`EditableAccessRule`, stripped via `toAccessRuleInput` before save); "Quitar regla" buttons have numbered `aria-label`s (`remove_access_rule_for`).
- "Sin unidad" option copy clarified to "(solo restringe por grupos)".

### Verification (second pass)
- `pnpm.cmd --dir apps\manage-web typecheck` — clean.
- `pnpm.cmd --dir apps\manage-web test -- --run` — 51 tests, 49 passed; the only 2 failures are the pre-existing TipTap "Estilo de bloque" toolbar tests (re-confirmed same failure reason). Updated/new tests: group filter replaces the old access filter in the filters test, unit-filter assertions and table-scoped queries in the access-column tests (the new filter `<option>`s would otherwise collide with `findByText`), and a preview-sentence assertion in the rule-saving test.
- `pnpm.cmd --dir apps\manage-web lint` — same 5 pre-existing findings in untouched files; touched files clean.
- `graphify update .` run.

## Pending backlog

### E. Spec-dependent (blocked on backend slices)
- Group ownership scoping for non-admin publishers (architecture.md): UI should disable out-of-scope groups with an explanatory tooltip instead of letting save fail. Blocked until the API exposes per-user usable-group information.

## Gotchas for the next session

- Read `context/README.md` first (project rule); UI conventions in `context/ui-context.md`; copy-from patterns in `context/code-patterns.md`.
- `GET /api/organizational-units` honors `includeInactive=true` only for Admin (`OrganizationalUnitsController.cs`).
- The two TipTap test failures and the 5 lint findings listed above are pre-existing; do not chase them as regressions of this work.
- The NBSP indentation literal in `organizationalUnitOptionLabel` is written as ` ` escapes on purpose; editors/formatters mangle raw NBSP characters.
- User-facing copy is bilingual es-AR/en-US; code/comments/docs in English; converse with the user in Spanish.
