import { type ReactNode } from 'react';
import { ShieldCheck } from 'lucide-react';

export interface AuthShellProps {
  children: ReactNode;
  brandName?: string;
  brandMark?: string;
  panelLabel?: string;
  proof?: string;
}

export function AuthShell({
  children,
  brandName = 'Advanced RAG',
  brandMark = 'AR',
  panelLabel = 'Advanced RAG',
  proof = 'Sesiones seguras con cookies HttpOnly y control CSRF.',
}: AuthShellProps) {
  return (
    <main className="auth-shell">
      <section className="auth-product-panel" aria-label={panelLabel}>
        <div className="auth-brand">
          <span className="brand-mark">{brandMark}</span>
          <span>{brandName}</span>
        </div>
        <div className="auth-proof">
          <ShieldCheck size={18} aria-hidden="true" />
          <span>{proof}</span>
        </div>
      </section>
      {children}
    </main>
  );
}

export interface AuthCardHeaderProps {
  eyebrow: string;
  title: string;
  detail?: string;
}

export function AuthCardHeader({ eyebrow, title, detail }: AuthCardHeaderProps) {
  return (
    <header className="auth-card-header">
      <p className="eyebrow">{eyebrow}</p>
      <h1>{title}</h1>
      {detail ? <p>{detail}</p> : null}
    </header>
  );
}
