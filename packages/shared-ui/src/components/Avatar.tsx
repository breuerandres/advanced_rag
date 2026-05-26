import { type HTMLAttributes } from 'react';
import { cn } from '../lib/cn';

export interface AvatarProps extends HTMLAttributes<HTMLDivElement> {
  name: string;
  src?: string;
}

function initials(name: string): string {
  return name
    .trim()
    .split(/\s+/)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() ?? '')
    .join('');
}

export function Avatar({ name, src, className, ...props }: AvatarProps) {
  return (
    <div
      aria-label={name}
      className={cn(
        'inline-flex h-8 w-8 items-center justify-center overflow-hidden rounded-[var(--radius-full)] bg-[var(--bg-muted)] text-xs font-semibold text-[var(--fg)]',
        className,
      )}
      {...props}
    >
      {src ? <img src={src} alt="" className="h-full w-full object-cover" /> : initials(name)}
    </div>
  );
}
