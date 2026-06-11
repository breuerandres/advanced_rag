import type { OrganizationalUnitSummary } from '../../api/orgUnits'

export interface UnitTree {
  /** Children keyed by parent id; the `null` key holds the top-level units. */
  childrenByParent: Map<string | null, OrganizationalUnitSummary[]>
  roots: OrganizationalUnitSummary[]
}

/**
 * Groups organizational units into a parent→children map with alphabetically
 * sorted siblings, used to render a collapsible tree. Units whose parent is
 * absent from the list (e.g. an inactive parent filtered out for non-admins)
 * are promoted to the top level so they are never hidden.
 */
export function buildUnitTree(units: OrganizationalUnitSummary[]): UnitTree {
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

  return { childrenByParent, roots: childrenByParent.get(null) ?? [] }
}
