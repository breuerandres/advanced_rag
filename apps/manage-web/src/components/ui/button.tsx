import type { ButtonHTMLAttributes } from 'react'
import { cn } from '../../lib/utils'

type ButtonProps = ButtonHTMLAttributes<HTMLButtonElement>

export function Button({ className, title, ...props }: ButtonProps) {
  const ariaLabel = typeof props['aria-label'] === 'string' ? props['aria-label'] : undefined
  const isIconButton = className?.split(/\s+/).includes('icon-button') ?? false
  const tooltip = isIconButton ? ariaLabel : undefined

  return (
    <button
      {...props}
      className={cn('ui-button', className)}
      data-tooltip={tooltip}
      title={isIconButton ? undefined : title}
    />
  )
}
