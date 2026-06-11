import type { OrganizationalUnitSummary } from '../../api/orgUnits'

export interface FlatOrganizationalUnit {
  unit: OrganizationalUnitSummary
  /** Depth within the visible tree (0 = top level). */
  level: number
}

/**
 * Flattens organizational units into depth-first tree order (parents immediately
 * followed by their children), with each entry tagged by its visible level.
 *
 * The API returns units ordered by depth, not tree order, so callers that render
 * a flat list (e.g. a select) need this to show the hierarchy. Units whose parent
 * is absent from the list (e.g. an inactive parent that was filtered out) are
 * promoted to the top level so they are never dropped.
 */
export function flattenUnitsInTreeOrder(
  units: OrganizationalUnitSummary[],
): FlatOrganizationalUnit[] {
  const presentIds = new Set(units.map((unit) => unit.id))
  const childrenByParent = new Map<string | null, OrganizationalUnitSummary[]>()

  for (const unit of units) {
    const effectiveParent =
      unit.parentId !== null && presentIds.has(unit.parentId) ? unit.parentId : null
    const siblings = childrenByParent.get(effectiveParent) ?? []
    siblings.push(unit)
    childrenByParent.set(effectiveParent, siblings)
  }

  for (const siblings of childrenByParent.values()) {
    siblings.sort((left, right) => left.name.localeCompare(right.name))
  }

  const flattened: FlatOrganizationalUnit[] = []
  const visit = (parentId: string | null, level: number) => {
    for (const unit of childrenByParent.get(parentId) ?? []) {
      flattened.push({ unit, level })
      visit(unit.id, level + 1)
    }
  }
  visit(null, 0)

  return flattened
}
