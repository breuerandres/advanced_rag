import { useId } from 'react';
import { cn } from '../lib/cn';

export interface RadioOption {
  value: string;
  label: string;
  disabled?: boolean;
}

export interface RadioGroupProps {
  options: RadioOption[];
  ariaLabel: string;
  value?: string;
  defaultValue?: string;
  name?: string;
  className?: string;
  onValueChange?: (value: string) => void;
}

export function RadioGroup({
  options,
  ariaLabel,
  value,
  defaultValue,
  name,
  className,
  onValueChange,
}: RadioGroupProps) {
  const generatedName = useId();
  const groupName = name ?? generatedName;

  return (
    <div role="radiogroup" aria-label={ariaLabel} className={cn('grid gap-2', className)}>
      {options.map((option) => (
        <label key={option.value} className="inline-flex items-center gap-2 text-sm text-[var(--fg)]">
          <input
            type="radio"
            name={groupName}
            value={option.value}
            checked={value === option.value ? true : undefined}
            defaultChecked={defaultValue === option.value ? true : undefined}
            disabled={option.disabled}
            className="h-4 w-4 border border-[var(--border)] accent-[var(--accent)] focus-visible:ring-2 focus-visible:ring-[var(--border-focus)]"
            onChange={(event) => {
              if (event.currentTarget.checked) {
                onValueChange?.(option.value);
              }
            }}
          />
          <span>{option.label}</span>
        </label>
      ))}
    </div>
  );
}
