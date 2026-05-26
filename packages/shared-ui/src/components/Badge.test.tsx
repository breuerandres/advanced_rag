import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { Badge } from './Badge';

describe('Badge', () => {
  it('renders status text', () => {
    render(<Badge tone="success">Publicado</Badge>);

    expect(screen.getByText('Publicado')).toBeInTheDocument();
  });
});
