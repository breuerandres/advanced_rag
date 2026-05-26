import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { EmptyState } from './EmptyState';

describe('EmptyState', () => {
  it('renders title and action', () => {
    render(<EmptyState title="Sin documentos" action={<button>Crear</button>} />);

    expect(screen.getByRole('heading', { name: 'Sin documentos' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Crear' })).toBeInTheDocument();
  });
});
