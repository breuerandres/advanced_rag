import { type InputHTMLAttributes, forwardRef } from 'react';
import { cn } from '../lib/cn';

export interface InputProps extends InputHTMLAttributes<HTMLInputElement> {
  invalid?: boolean;
}

export const Input = forwardRef<HTMLInputElement, InputProps>(
  ({ className, invalid, 'aria-invalid': ariaInvalid, ...props }, ref) => (
    <input
      ref={ref}
      aria-invalid={ariaInvalid ?? invalid ?? undefined}
      className={cn(
        'flex h-9 w-full rounded-[var(--radius)] border border-[var(--border)] bg-[var(--bg-elevated)] px-3 py-2 text-sm text-[var(--fg)] shadow-[var(--shadow-xs)] transition-colors duration-[var(--duration-fast)] ease-[var(--ease)]',
        'placeholder:text-[var(--fg-subtle)]',
        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--border-focus)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--bg)]',
        'disabled:cursor-not-allowed disabled:opacity-50',
        'aria-[invalid=true]:border-[var(--danger)]',
        className,
      )}
      {...props}
    />
  ),
);

Input.displayName = 'Input';
