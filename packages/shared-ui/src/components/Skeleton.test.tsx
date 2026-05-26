import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { Skeleton } from './Skeleton';

describe('Skeleton', () => {
  it('exposes loading status', () => {
    render(<Skeleton ariaLabel="Cargando usuarios" />);

    expect(screen.getByRole('status', { name: 'Cargando usuarios' })).toBeInTheDocument();
  });
});
