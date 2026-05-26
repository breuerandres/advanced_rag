import { type InputHTMLAttributes, useId } from 'react';
import { cn } from '../lib/cn';

export interface CheckboxProps
  extends Omit<InputHTMLAttributes<HTMLInputElement>, 'type' | 'onChange'> {
  label: string;
  onCheckedChange?: (checked: boolean) => void;
}

export function Checkbox({ id, label, className, onCheckedChange, ...props }: CheckboxProps) {
  const generatedId = useId();
  const inputId = id ?? generatedId;

  return (
    <label className={cn('inline-flex items-center gap-2 text-sm text-[var(--fg)]', className)} htmlFor={inputId}>
      <input
        id={inputId}
        type="checkbox"
        className="h-4 w-4 rounded-[var(--radius-xs)] border border-[var(--border)] accent-[var(--accent)] focus-visible:ring-2 focus-visible:ring-[var(--border-focus)]"
        onChange={(event) => onCheckedChange?.(event.currentTarget.checked)}
        {...props}
      />
      <span>{label}</span>
    </label>
  );
}
