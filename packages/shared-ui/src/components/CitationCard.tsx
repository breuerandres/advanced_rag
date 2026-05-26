import { type ReactNode } from 'react';
import { Badge } from './Badge';
import { cn } from '../lib/cn';

export interface CitationCardProps {
  title: string;
  headingPath?: string[];
  meta?: ReactNode;
  href?: string;
  className?: string;
}

export function CitationCard({ title, headingPath = [], meta, href, className }: CitationCardProps) {
  const content = (
    <div className={cn('grid gap-1 rounded-[var(--radius)] border border-[var(--border)] bg-[var(--bg-elevated)] p-3 text-sm shadow-[var(--shadow-xs)]', className)}>
      <div className="font-medium text-[var(--fg)]">{title}</div>
      {headingPath.length > 0 && <div className="text-xs text-[var(--fg-muted)]">{headingPath.join(' / ')}</div>}
      {meta && <Badge tone="neutral">{meta}</Badge>}
    </div>
  );

  if (href) {
    return (
      <a href={href} className="block no-underline hover:no-underline">
        {content}
      </a>
    );
  }

  return content;
}
