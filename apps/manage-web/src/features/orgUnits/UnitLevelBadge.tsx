import { useTranslation } from 'react-i18next'

// Number of distinct color tones; levels deeper than this cycle through them.
const TONE_COUNT = 6

interface UnitLevelBadgeProps {
  level: number
}

/**
 * Compact badge that signals the hierarchical depth of an organizational unit.
 * The numbered label is the primary signal; color only reinforces it. The badge
 * is decorative for assistive tech (hierarchy is conveyed by structure/indent),
 * so it is hidden from the accessible name of rows and select options.
 */
export function UnitLevelBadge({ level }: UnitLevelBadgeProps) {
  const { t } = useTranslation()
  const tone = ((level % TONE_COUNT) + TONE_COUNT) % TONE_COUNT
  const tooltip = t('orgUnits.level_tooltip', { level })

  return (
    <span className="unit-level-badge" data-tone={tone} title={tooltip} aria-hidden="true">
      N{level}
    </span>
  )
}
