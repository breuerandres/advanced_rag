import { type HTMLAttributes, type ReactNode } from 'react';
import { cn } from '../lib/cn';

export interface EmptyStateProps extends HTMLAttributes<HTMLElement> {
  title: string;
  description?: string;
  icon?: ReactNode;
  action?: ReactNode;
}

export function EmptyState({ title, description, icon, action, className, ...props }: EmptyStateProps) {
  return (
    <section
      className={cn('grid justify-items-center gap-3 rounded-[var(--radius)] border border-dashed border-[var(--border)] bg-[var(--bg-elevated)] p-8 text-center', className)}
      {...props}
    >
      {icon && <div className="text-[var(--fg-muted)]">{icon}</div>}
      <div>
        <h2 className="text-base font-semibold text-[var(--fg)]">{title}</h2>
        {description && <p className="mt-1 text-sm text-[var(--fg-muted)]">{description}</p>}
      </div>
      {action}
    </section>
  );
}
