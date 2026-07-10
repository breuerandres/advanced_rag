import { type ReactNode } from 'react';
import { ShieldCheck } from 'lucide-react';
import { DarkModeToggle } from './DarkModeToggle';
import { LanguageSelect, type LanguageSelectOption } from './LanguageSelect';

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

const defaultLanguageOptions: LanguageSelectOption[] = [
  { value: 'es-AR', label: 'ES' },
  { value: 'en-US', label: 'EN' },
];

export interface AuthFrameProps extends AuthShellProps {
  ariaLabel?: string;
  controls?: ReactNode;
}

export function AuthFrame({
  ariaLabel = 'Controles de acceso',
  children,
  controls,
  ...shellProps
}: AuthFrameProps) {
  return (
    <AuthShell {...shellProps}>
      <section className="auth-card-stack" aria-label={ariaLabel}>
        {controls}
        {children}
      </section>
    </AuthShell>
  );
}

export interface AuthSurfaceControlsProps {
  language: string;
  languageLabel: string;
  languageOptions?: LanguageSelectOption[];
  onLanguageChange: (value: string) => void;
  themeLabel: string;
}

export function AuthSurfaceControls({
  language,
  languageLabel,
  languageOptions = defaultLanguageOptions,
  onLanguageChange,
  themeLabel,
}: AuthSurfaceControlsProps) {
  return (
    <div className="auth-surface-controls">
      <LanguageSelect
        label={languageLabel}
        value={language}
        options={languageOptions}
        onChange={onLanguageChange}
      />
      <DarkModeToggle label={themeLabel} />
    </div>
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
      {detail ? <p className="auth-card-detail">{detail}</p> : null}
    </header>
  );
}
