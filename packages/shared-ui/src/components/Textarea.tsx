import { type TextareaHTMLAttributes, forwardRef } from 'react';
import { cn } from '../lib/cn';

export interface TextareaProps extends TextareaHTMLAttributes<HTMLTextAreaElement> {
  invalid?: boolean;
}

export const Textarea = forwardRef<HTMLTextAreaElement, TextareaProps>(
  ({ className, invalid, 'aria-invalid': ariaInvalid, ...props }, ref) => (
    <textarea
      ref={ref}
      aria-invalid={ariaInvalid ?? invalid ?? undefined}
      className={cn(
        'min-h-24 w-full resize-y rounded-[var(--radius)] border border-[var(--border)] bg-[var(--bg-elevated)] px-3 py-2 text-sm text-[var(--fg)] shadow-[var(--shadow-xs)] transition-colors duration-[var(--duration-fast)] ease-[var(--ease)]',
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

Textarea.displayName = 'Textarea';
