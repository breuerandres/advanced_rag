import { type ReactNode } from 'react';
import { cn } from '../lib/cn';

interface SidebarProps {
  /** Top content of the sidebar (e.g. "New conversation" button). */
  top?: ReactNode;
  /** Main scrollable content (e.g. nav links or conversation list). */
  children: ReactNode;
  /** Bottom content of the sidebar (e.g. user identity + logout). */
  bottom?: ReactNode;
  className?: string;
}

/**
 * Sidebar layout — header / scrollable content / footer.
 * Place inside `<AppShell sidebar={...}>`.
 */
export function Sidebar({ top, bottom, children, className }: SidebarProps) {
  return (
    <div className={cn('flex h-full flex-col', className)}>
      {top && (
        <div className="flex-shrink-0 border-b border-[var(--border)] p-3">{top}</div>
      )}
      <div className="flex-1 overflow-y-auto p-2">{children}</div>
      {bottom && (
        <div className="flex-shrink-0 border-t border-[var(--border)] p-3">{bottom}</div>
      )}
    </div>
  );
}
