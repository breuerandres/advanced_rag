import { type HTMLAttributes } from 'react';
import { cn } from '../lib/cn';

export interface SkeletonProps extends HTMLAttributes<HTMLDivElement> {
  ariaLabel?: string;
}

export function Skeleton({ ariaLabel = 'Cargando', className, ...props }: SkeletonProps) {
  return (
    <div
      role="status"
      aria-label={ariaLabel}
      className={cn('h-4 animate-pulse rounded-[var(--radius-sm)] bg-[var(--bg-muted)]', className)}
      {...props}
    />
  );
}
