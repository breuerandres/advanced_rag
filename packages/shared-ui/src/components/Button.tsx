import { type ButtonHTMLAttributes, forwardRef } from 'react';
import { cva, type VariantProps } from 'class-variance-authority';
import { cn } from '../lib/cn';

const buttonVariants = cva(
  [
    'inline-flex items-center justify-center gap-2',
    'font-medium leading-none tracking-tight',
    'rounded-[var(--radius)]',
    'transition-colors duration-[var(--duration-fast)] ease-[var(--ease)]',
    'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--border-focus)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--bg)]',
    'disabled:pointer-events-none disabled:opacity-50',
    'whitespace-nowrap select-none',
  ].join(' '),
  {
    variants: {
      variant: {
        primary: [
          'bg-[var(--accent)] text-[var(--accent-fg)]',
          'hover:bg-[color-mix(in_oklab,var(--accent)_88%,black)]',
          'active:bg-[color-mix(in_oklab,var(--accent)_80%,black)]',
        ].join(' '),
        secondary: [
          'bg-[var(--bg-elevated)] text-[var(--fg)] border border-[var(--border)]',
          'hover:bg-[var(--bg-subtle)]',
          'active:bg-[var(--bg-muted)]',
        ].join(' '),
        ghost: [
          'bg-transparent text-[var(--fg)]',
          'hover:bg-[var(--bg-subtle)]',
          'active:bg-[var(--bg-muted)]',
        ].join(' '),
        danger: [
          'bg-[var(--danger)] text-[var(--danger-fg)]',
          'hover:bg-[color-mix(in_oklab,var(--danger)_88%,black)]',
          'active:bg-[color-mix(in_oklab,var(--danger)_80%,black)]',
        ].join(' '),
        link: [
          'bg-transparent text-[var(--accent)] underline-offset-2',
          'hover:underline',
        ].join(' '),
      },
      size: {
        xs: 'h-7 px-2 text-xs',
        sm: 'h-8 px-3 text-sm',
        md: 'h-9 px-4 text-sm',
        lg: 'h-10 px-5 text-base',
        icon: 'h-9 w-9 p-0',
      },
    },
    defaultVariants: {
      variant: 'primary',
      size: 'md',
    },
  },
);

export type ButtonVariant = NonNullable<VariantProps<typeof buttonVariants>['variant']>;
export type ButtonSize = NonNullable<VariantProps<typeof buttonVariants>['size']>;

export interface ButtonProps
  extends ButtonHTMLAttributes<HTMLButtonElement>,
    VariantProps<typeof buttonVariants> {
  loading?: boolean;
}

export const Button = forwardRef<HTMLButtonElement, ButtonProps>(
  ({ className, variant, size, loading, disabled, children, ...rest }, ref) => {
    return (
      <button
        ref={ref}
        className={cn(buttonVariants({ variant, size }), className)}
        disabled={disabled || loading}
        aria-busy={loading || undefined}
        {...rest}
      >
        {loading && (
          <span
            className="inline-block h-3 w-3 animate-spin rounded-full border-2 border-current border-r-transparent"
            aria-hidden="true"
          />
        )}
        {children}
      </button>
    );
  },
);

Button.displayName = 'Button';
