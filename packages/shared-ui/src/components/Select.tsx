import type { ReactNode } from 'react';
import * as SelectPrimitive from '@radix-ui/react-select';
import { Check, ChevronDown } from 'lucide-react';
import { cn } from '../lib/cn';

export interface SelectOption {
  value: string;
  label: string;
  disabled?: boolean;
  /** Indentation depth (0 = top level). Adds left padding to the option. */
  depth?: number;
  /** Optional leading adornment (e.g. a level badge) shown before the label. */
  badge?: ReactNode;
}

export interface SelectProps {
  options: SelectOption[];
  value?: string;
  defaultValue?: string;
  placeholder?: string;
  ariaLabel: string;
  disabled?: boolean;
  className?: string;
  onValueChange?: (value: string) => void;
}

export function Select({
  options,
  value,
  defaultValue,
  placeholder = 'Select',
  ariaLabel,
  disabled,
  className,
  onValueChange,
}: SelectProps) {
  const rootProps = {
    ...(value !== undefined ? { value } : {}),
    ...(defaultValue !== undefined ? { defaultValue } : {}),
    ...(disabled !== undefined ? { disabled } : {}),
    ...(onValueChange ? { onValueChange } : {}),
  };

  const selectedOption = options.find((option) => option.value === value);

  return (
    <SelectPrimitive.Root {...rootProps}>
      <SelectPrimitive.Trigger
        aria-label={ariaLabel}
        className={cn(
          'inline-flex h-9 w-full items-center justify-between gap-2 rounded-[var(--radius)] border border-[var(--border)] bg-[var(--bg-elevated)] px-3 text-sm text-[var(--fg)] shadow-[var(--shadow-xs)]',
          'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--border-focus)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--bg)] disabled:opacity-50',
          className,
        )}
      >
        <span className="inline-flex min-w-0 items-center gap-2 truncate">
          {selectedOption?.badge}
          <SelectPrimitive.Value placeholder={placeholder} />
        </span>
        <SelectPrimitive.Icon asChild>
          <ChevronDown className="h-4 w-4 text-[var(--fg-muted)]" aria-hidden="true" />
        </SelectPrimitive.Icon>
      </SelectPrimitive.Trigger>
      <SelectPrimitive.Portal>
        <SelectPrimitive.Content
          position="popper"
          sideOffset={4}
          className="z-[var(--z-popover)] min-w-[var(--radix-select-trigger-width)] overflow-hidden rounded-[var(--radius)] border border-[var(--border)] bg-[var(--bg-elevated)] p-1 text-sm text-[var(--fg)] shadow-[var(--shadow-lg)]"
        >
          <SelectPrimitive.Viewport>
            {options.map((option) => (
              <SelectPrimitive.Item
                key={option.value}
                value={option.value}
                {...(option.disabled !== undefined ? { disabled: option.disabled } : {})}
                className="relative flex h-8 cursor-default select-none items-center rounded-[var(--radius-sm)] px-8 text-sm outline-none data-[highlighted]:bg-[var(--bg-subtle)] data-[disabled]:opacity-50"
                {...(option.depth
                  ? { style: { paddingLeft: `calc(2rem + ${option.depth * 0.75}rem)` } }
                  : {})}
              >
                <SelectPrimitive.ItemIndicator className="absolute left-2 inline-flex items-center">
                  <Check className="h-4 w-4" aria-hidden="true" />
                </SelectPrimitive.ItemIndicator>
                {option.badge ? (
                  <span className="mr-2 inline-flex shrink-0">{option.badge}</span>
                ) : null}
                <SelectPrimitive.ItemText>{option.label}</SelectPrimitive.ItemText>
              </SelectPrimitive.Item>
            ))}
          </SelectPrimitive.Viewport>
        </SelectPrimitive.Content>
      </SelectPrimitive.Portal>
    </SelectPrimitive.Root>
  );
}
