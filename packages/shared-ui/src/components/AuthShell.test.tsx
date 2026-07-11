import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { AuthCardHeader, AuthFrame, AuthSurfaceControls } from './AuthShell';

describe('AuthFrame', () => {
  it('renders the shared product auth frame', () => {
    const onLanguageChange = vi.fn();

    render(
      <AuthFrame
        ariaLabel="Access controls"
        controls={
          <AuthSurfaceControls
            languageLabel="Language"
            language="es-AR"
            onLanguageChange={onLanguageChange}
            themeLabel="Change theme"
          />
        }
      >
        <form className="auth-card" aria-label="Login">
          <AuthCardHeader eyebrow="Chat" title="Sign in" detail="Use your account to continue." />
        </form>
      </AuthFrame>,
    );

    expect(screen.getByLabelText('ReferentIA')).toBeInTheDocument();
    expect(screen.getByText('Sesiones seguras con cookies HttpOnly y control CSRF.')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Sign in' }).closest('main')).toHaveClass('auth-shell');
    expect(screen.getByRole('heading', { name: 'Sign in' }).closest('.auth-card-stack')).toHaveAttribute(
      'aria-label',
      'Access controls',
    );
    expect(screen.getByText('Use your account to continue.')).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText('Language'), { target: { value: 'en-US' } });
    expect(onLanguageChange).toHaveBeenCalledWith('en-US');
    expect(screen.getByRole('button', { name: 'Change theme' })).toBeInTheDocument();
  });
});
