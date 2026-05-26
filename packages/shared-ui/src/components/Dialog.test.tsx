import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { Dialog } from './Dialog';

describe('Dialog', () => {
  it('renders open modal content with a title', () => {
    render(<Dialog open title="Confirmar">Contenido</Dialog>);

    expect(screen.getByRole('dialog', { name: 'Confirmar' })).toBeInTheDocument();
  });
});
