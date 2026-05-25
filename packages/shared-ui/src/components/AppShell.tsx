import { type ReactNode } from 'react';
import { cn } from '../lib/cn';

interface AppShellProps {
  /** Top header. Use `<Header>`. */
  header?: ReactNode;
  /** Left sidebar. Use `<Sidebar>`. Optional. */
  sidebar?: ReactNode;
  /** Right panel (e.g. citations in chat). Optional, collapsible by the consumer. */
  rightPanel?: ReactNode;
  /** Bottom strip (e.g. metrics in chat). Optional. */
  footer?: ReactNode;
  /** Main content area. */
  children: ReactNode;
  /** Extra className for the main area. */
  className?: string;
}

/**
 * Application shell layout. Linear-style: header + (sidebar / main / right) + footer.
 *
 * Pure layout component — keeps no state. Sidebar collapse, right-panel open/close,
 * and footer visibility are managed by the consumer.
 */
export function AppShell({
  header,
  sidebar,
  rightPanel,
  footer,
  children,
  className,
}: AppShellProps) {
  return (
    <div className="flex min-h-screen flex-col bg-[var(--bg)] text-[var(--fg)]">
      {header && (
        <header
          className="sticky top-0 z-[var(--z-sticky)] h-[var(--header-height)] flex-shrink-0 border-b border-[var(--border)] bg-[var(--bg-elevated)]"
        >
          {header}
        </header>
      )}
      <div className="flex flex-1 overflow-hidden">
        {sidebar && (
          <aside
            className="hidden lg:flex w-[var(--sidebar-width)] flex-shrink-0 flex-col border-r border-[var(--border)] bg-[var(--bg-elevated)]"
          >
            {sidebar}
          </aside>
        )}
        <main
          className={cn(
            'flex flex-1 flex-col overflow-auto',
            className,
          )}
        >
          {children}
        </main>
        {rightPanel && (
          <aside
            className="hidden xl:flex w-96 flex-shrink-0 flex-col border-l border-[var(--border)] bg-[var(--bg-elevated)]"
          >
            {rightPanel}
          </aside>
        )}
      </div>
      {footer && (
        <footer
          className="h-[var(--footer-height)] flex-shrink-0 border-t border-[var(--border)] bg-[var(--bg-elevated)]"
        >
          {footer}
        </footer>
      )}
    </div>
  );
}
