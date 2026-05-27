import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { AuthCardHeader, AuthShell } from './AuthShell';

describe('AuthShell', () => {
  it('renders the shared product auth frame', () => {
    render(
      <AuthShell>
        <form className="auth-card" aria-label="Login">
          <AuthCardHeader eyebrow="Chat" title="Iniciar sesion" />
        </form>
      </AuthShell>,
    );

    expect(screen.getByLabelText('Advanced RAG')).toBeInTheDocument();
    expect(screen.getByText('Sesiones seguras con cookies HttpOnly y control CSRF.')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Iniciar sesion' }).closest('main')).toHaveClass('auth-shell');
  });
});
