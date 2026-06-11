import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Check, ChevronDown, ChevronRight } from 'lucide-react'
import { Popover } from '@helpcenter/shared-ui'
import type { OrganizationalUnitSummary } from '../../api/orgUnits'
import { UnitLevelBadge } from './UnitLevelBadge'
import { buildUnitTree } from './unitTree'

export interface UnitTreeSelectOption {
  value: string
  label: string
}

interface UnitTreeSelectProps {
  units: OrganizationalUnitSummary[]
  /** Selected value: a unit id, a leading-option value, or '' for none. */
  value: string
  onChange: (value: string) => void
  ariaLabel: string
  placeholder: string
  /** Flat, non-hierarchical rows rendered above the tree (e.g. "no unit"). */
  leadingOptions?: UnitTreeSelectOption[]
  /** Suffix appended to top-level unit names (e.g. "toda la empresa"). */
  companyWideLabel?: string
  disabled?: boolean
}

/**
 * Single-select organizational-unit picker that renders the hierarchy as a
 * collapsible tree (chevrons + level badges), mirroring the Organizational
 * Units page. The tree opens collapsed to its roots; selecting a row closes
 * the popover.
 */
export function UnitTreeSelect({
  units,
  value,
  onChange,
  ariaLabel,
  placeholder,
  leadingOptions = [],
  companyWideLabel,
  disabled,
}: UnitTreeSelectProps) {
  const { t } = useTranslation()
  const [open, setOpen] = useState(false)
  const [expanded, setExpanded] = useState<Set<string>>(new Set())

  const { childrenByParent, roots } = buildUnitTree(units)

  const unitLabel = (unit: OrganizationalUnitSummary) =>
    unit.parentId === null && companyWideLabel
      ? `${unit.name} (${companyWideLabel})`
      : unit.name

  const selectedUnit = units.find((unit) => unit.id === value)
  const selectedLeading = leadingOptions.find((option) => option.value === value)
  const triggerText = selectedUnit
    ? unitLabel(selectedUnit)
    : (selectedLeading?.label ?? placeholder)
  const hasSelection = selectedUnit !== undefined || selectedLeading !== undefined

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

  function select(nextValue: string) {
    onChange(nextValue)
    setOpen(false)
  }

  function renderNode(unit: OrganizationalUnitSummary, level: number) {
    const children = childrenByParent.get(unit.id) ?? []
    const isExpanded = expanded.has(unit.id)
    const isSelected = unit.id === value
    return (
      <li key={unit.id} className="unit-tree-node">
        <div className="unit-tree-row" style={{ paddingLeft: `${level * 1.1}rem` }}>
          {children.length > 0 ? (
            <button
              type="button"
              className="unit-tree-toggle"
              aria-label={t(isExpanded ? 'orgUnits.collapse_branch' : 'orgUnits.expand_branch', {
                name: unit.name,
              })}
              onClick={() => toggle(unit.id)}
            >
              {isExpanded ? <ChevronDown size={16} /> : <ChevronRight size={16} />}
            </button>
          ) : (
            <span className="unit-tree-toggle-spacer" aria-hidden="true" />
          )}
          <button
            type="button"
            className="unit-tree-option"
            data-selected={isSelected || undefined}
            onClick={() => select(unit.id)}
          >
            <UnitLevelBadge level={unit.depth} />
            <span className="unit-tree-option-label">{unitLabel(unit)}</span>
            {isSelected ? <Check size={14} className="unit-tree-check" aria-hidden="true" /> : null}
          </button>
        </div>
        {children.length > 0 && isExpanded ? (
          <ul className="unit-tree-children">
            {children.map((child) => renderNode(child, level + 1))}
          </ul>
        ) : null}
      </li>
    )
  }

  return (
    <Popover
      open={open}
      onOpenChange={setOpen}
      align="start"
      contentClassName="unit-tree-panel"
      trigger={
        <button
          type="button"
          className={hasSelection ? 'unit-tree-trigger' : 'unit-tree-trigger placeholder'}
          aria-label={ariaLabel}
          aria-haspopup="tree"
          disabled={disabled}
        >
          <span className="unit-tree-trigger-value">
            {selectedUnit ? <UnitLevelBadge level={selectedUnit.depth} /> : null}
            <span className="unit-tree-trigger-text">{triggerText}</span>
          </span>
          <ChevronDown size={16} className="unit-tree-trigger-caret" aria-hidden="true" />
        </button>
      }
    >
      <ul className="unit-tree" role="tree" aria-label={ariaLabel}>
        {leadingOptions.map((option) => (
          <li key={option.value} className="unit-tree-node">
            <div className="unit-tree-row">
              <span className="unit-tree-toggle-spacer" aria-hidden="true" />
              <button
                type="button"
                className="unit-tree-option"
                data-selected={option.value === value || undefined}
                onClick={() => select(option.value)}
              >
                <span className="unit-tree-option-label">{option.label}</span>
                {option.value === value ? (
                  <Check size={14} className="unit-tree-check" aria-hidden="true" />
                ) : null}
              </button>
            </div>
          </li>
        ))}
        {roots.map((unit) => renderNode(unit, 0))}
      </ul>
    </Popover>
  )
}
