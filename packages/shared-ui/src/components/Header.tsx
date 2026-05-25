import { type ReactNode } from 'react';

interface HeaderProps {
  /** Logo / brand area (left). */
  brand?: ReactNode;
  /** Centre slot (e.g. search bar). */
  centre?: ReactNode;
  /** Right-aligned slot (theme toggle + avatar typically). */
  actions?: ReactNode;
}

/**
 * Top header. 56px tall, sticky, opinionated layout. Place inside `<AppShell header={...}>`.
 */
export function Header({ brand, centre, actions }: HeaderProps) {
  return (
    <div className="flex h-full items-center gap-4 px-4">
      <div className="flex flex-shrink-0 items-center gap-3">{brand}</div>
      {centre && <div className="flex-1 min-w-0 px-2">{centre}</div>}
      {!centre && <div className="flex-1" />}
      {actions && <div className="flex flex-shrink-0 items-center gap-2">{actions}</div>}
    </div>
  );
}
