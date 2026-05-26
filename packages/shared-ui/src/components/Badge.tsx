import { type HTMLAttributes } from 'react';
import { cva, type VariantProps } from 'class-variance-authority';
import { cn } from '../lib/cn';

const badgeVariants = cva(
  'inline-flex items-center rounded-[var(--radius-sm)] border px-2 py-0.5 text-xs font-medium leading-5',
  {
    variants: {
      tone: {
        neutral: 'border-[var(--border)] bg-[var(--bg-subtle)] text-[var(--fg)]',
        success: 'border-transparent bg-[color-mix(in_oklab,var(--success)_14%,transparent)] text-[var(--success)]',
        warning: 'border-transparent bg-[color-mix(in_oklab,var(--warning)_16%,transparent)] text-[var(--warning)]',
        danger: 'border-transparent bg-[color-mix(in_oklab,var(--danger)_14%,transparent)] text-[var(--danger)]',
        info: 'border-transparent bg-[color-mix(in_oklab,var(--info)_14%,transparent)] text-[var(--info)]',
      },
    },
    defaultVariants: { tone: 'neutral' },
  },
);

export interface BadgeProps extends HTMLAttributes<HTMLSpanElement>, VariantProps<typeof badgeVariants> {}

export function Badge({ tone, className, ...props }: BadgeProps) {
  return <span className={cn(badgeVariants({ tone }), className)} {...props} />;
}
